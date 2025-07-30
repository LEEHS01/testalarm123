using Newtonsoft.Json.Linq;
using Onthesys.ExeBuild;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace HNS_Alarm.Module.Manager
{
    class ModelManager
    {
        private readonly Action<string> msgInvoker;
        private readonly DbManager db;

        public ModelManager(Action<string> msgInvoker, DbManager db)
        {
            this.msgInvoker = msgInvoker;
            this.db = db;
        }

        const bool reportEnabled = true;
        string reportBuffer = "";


        #region [DataStruct]

        public List<AreaDataModel> areas = new ();
        public List<ObservatoryModel> obss = new ();
        public List<AlarmLogModel> alarms = new ();

        public Dictionary<ObservatoryModel, List<BoardStateModel>> boards = new();
        public Dictionary<ObservatoryModel, List<HnsResourceModel>> sensors = new();
        public Dictionary<HnsResourceModel, CurrentDataModel?> values = new();
        #endregion

        /// <summary>
        /// 프로그램의 초기화 작업이 정상적으로 수행됐는지, 아닌지
        /// </summary>
        /// <returns></returns>
        public bool IsSetupped()
        {
            //Debug용 메시지 출력
            if (false)
            {
                msgInvoker($"\narea : {areas.Count} \n obss : {obss.Count} \n boards : {boards.Count} \nsensors : {sensors.Count} \nvalues : {values.Count} \n");
                areas.ForEach(area => msgInvoker(area.areaNm + "\n"));
                obss.ForEach(obs => msgInvoker(obs.obsnm + "\n"));
                boards.Values.AsEnumerable().ToList().ForEach(boards => boards.ForEach(board => msgInvoker($"{board.obsidx}/{board.boardidx} : " + board.brd_state + "\n")));
                sensors.Values.AsEnumerable().ToList().ForEach(sensors => sensors.ForEach(sensor => msgInvoker($"{sensor.obsidx}/{sensor.boardidx}/{sensor.hnsidx}({sensor.hnsnm}) : " + sensor.alahihival + " - " + sensor.alahival + "\n")));
                foreach (KeyValuePair<HnsResourceModel, CurrentDataModel?> item in values) msgInvoker($"{item.Key.obsidx}/{item.Key.boardidx}/{item.Key.hnsidx}({item.Key.hnsnm}) : " + (item.Value.val.ToString() ?? "null") + "\n");
            }

            return areas.Count == 0 ||
                    obss.Count == 0 ||
                    boards.Count == 0 ||
                    sensors.Count == 0 ||
                    values.Count == 0;
        }

        /// <summary>
        /// 프로그램의 초기화 작업 수행
        /// </summary>
        public async void Setup()
        {
            //로딩 작업을 위한 간단 UI
            int obsSucced = 0, currentFound = 0, hnsFound = 0;
            Action PrintProcess = () => msgInvoker($"\rModel Manager is Loading Datas from DB... " +
                $"(Obs : {obsSucced}/{obss.Count} " +
                $"boards : ({boards.Values.AsEnumerable().Sum(boards => boards.Count)}) " +
                $"hns : {currentFound}/{hnsFound} " +
                $"alarm : {alarms.Count})");

            //혹시 모르니 초기화
            Drop(true);
            //테스크들의 종료 감시용
            List<Task> loadingTasks = new();

            msgInvoker("Model Manager Started Model Load!\n");
            reportBuffer = "";
            await Task.Delay(100);
            PrintProcess();


            //DB 값들 모두 입력
            var task = db.GetAlarmActivated()
                .ContinueWith(task => { 
                    this.alarms.AddRange(task.Result);
                    PrintProcess();
                });
            loadingTasks.Add(task);

            task = db.GetAreas()
                .ContinueWith(task =>
                {
                    this.areas.AddRange(task.Result);
                    PrintProcess();
                });
            loadingTasks.Add(task);

            //obs 로드
            task = db.GetObss()
                .ContinueWith(task => {
                    var newObss = task.Result.Where(obs => obs.obsidx == 1).ToList(); //테스트 용으로 1번 관측소만 로드

                    this.obss.AddRange(newObss);
                    PrintProcess();
                })
                .ContinueWith(task =>
                //각 obs 별로 
                this.obss.ForEach(obs => {
                    //Task.Delay(100);
                    task = db.GetBoardStates(obs.obsidx).ContinueWith(task =>
                    {
                        List<BoardStateModel> newBoards = task.Result;

                        if (newBoards == null || newBoards.Count <= 0) return;
                        boards.Add(obs, newBoards);

                        PrintProcess();
                    });
                    loadingTasks.Add(task);

                    //Task.Delay(100);
                    task = db.GetHnsByObsId(obs.obsidx)
                        //hns(=센서) 로드
                        .ContinueWith(sensorTask =>
                        {
                            if (sensorTask.Result == null || sensorTask.Result.Count <= 0) return;

                            sensors.Add(obs, sensorTask.Result);
                            
                            sensorTask.Result.ForEach(sensor => values.Add(sensor, null));

                            hnsFound += sensorTask.Result.Count;
                            PrintProcess();
                        })
                        //각 obs 별로 현재값 로드
                        .ContinueWith(task => {
                            //Task.Delay(100);
                            task = db.GetLateValueByObsId(obs.obsidx)
                                .ContinueWith(valueTask =>
                                {
                                    List<CurrentDataModel> vals = valueTask.Result;
                                    if (vals == null || vals.Count <= 0) return;
                                    vals.ForEach(value =>
                                    {
                                        if (value == null) return;

                                        HnsResourceModel? sensor = sensors[obs].FirstOrDefault(s => s.hnsidx == value.hnsidx && s.boardidx == value.boardidx);

                                        if (sensor == null) return;

                                        if (values.ContainsKey(sensor)) values[sensor] = value;

                                        currentFound++;
                                        PrintProcess();
                                    });

                                });
                            loadingTasks.Add(task);
                        });
                    loadingTasks.Add(task);
                    obsSucced++;
                    PrintProcess();
                }));
            loadingTasks.Add(task);


            while (loadingTasks.Find(task => !task.IsCompleted) != null)
                await Task.Delay(100);
            PrintProcess();

            msgInvoker("\nModel Manager Loading Model Data!\n");
            msgInvoker("[Refresh Report]\n\n" + reportBuffer == "" ? "--nothing to report--" : reportBuffer);
        }

        /// <summary>
        /// 시간에 따라 변화하는 값에 대한 최신화
        /// </summary>
        public async Task Refresh()
        {
            //로딩 작업을 위한 간단 UI
            int obsSucced = 0, currentFound = 0, hnsFound = 0;
            Action PrintProcess = () => msgInvoker($"\rModel Manager is Refreshing Datas from DB... " +
                $"(Obs : {obsSucced}/{obss.Count} " +
                $"boards : ({boards.Values.AsEnumerable().Sum(boards => boards.Count)}) " +
                $"hns : {currentFound}/{hnsFound} " +
                $"alarm : {alarms.Count})");

            //혹시 모르니 초기화
            Drop(false);
            //테스크들의 종료 감시용
            List<Task> loadingTasks = new();

            msgInvoker("Model Manager Started Model Refresh!\n");
            reportBuffer = "";
            await Task.Delay(100);
            PrintProcess();


            var task = db.GetAlarmActivated()
                .ContinueWith(task => {
                    this.alarms.AddRange(task.Result);
                    PrintProcess();
                });
            loadingTasks.Add(task);

             this.obss.ForEach(obs => {
                //Task.Delay(100);
                task = db.GetBoardStates(obs.obsidx).ContinueWith(task =>
                {
                    List<BoardStateModel> newBoards = task.Result;

                    if (newBoards == null || newBoards.Count <= 0) return;
                    boards.Add(obs, newBoards);

                    PrintProcess();
                });

                //Task.Delay(100);
                task = db.GetHnsByObsId(obs.obsidx)
                    //hns(=센서) 로드
                    .ContinueWith(sensorTask =>
                    {
                        if (sensorTask.Result == null || sensorTask.Result.Count <= 0) return;

                        sensors.Add(obs, sensorTask.Result);

                        sensorTask.Result.ForEach(sensor => values.Add(sensor, null));

                        hnsFound += sensorTask.Result.Count;
                        PrintProcess();
                    })
                    //각 obs 별로 현재값 로드
                    .ContinueWith(task => {
                        //Task.Delay(100);
                        task = db.GetLateValueByObsId(obs.obsidx)
                            .ContinueWith(valueTask =>
                            {
                                List<CurrentDataModel> vals = valueTask.Result;
                                if (vals == null || vals.Count <= 0) return;
                                vals.ForEach(value =>
                                {
                                    if (value == null) return;

                                    HnsResourceModel? sensor = sensors[obs].FirstOrDefault(s => s.hnsidx == value.hnsidx && s.boardidx == value.boardidx);

                                    if (sensor == null) return;

                                    if (values.ContainsKey(sensor)) values[sensor] = value;

                                    currentFound++;
                                    PrintProcess();
                                });

                            });
                        loadingTasks.Add(task);
                    });

                 loadingTasks.Add(task);

                obsSucced++;
                PrintProcess();
            });


            while(loadingTasks.Find(task => !task.IsCompleted) != null)
                await Task.Delay(100);
            PrintProcess();


            //await Task.WhenAll(loadingTasks);
            await Task.Delay(100);
            msgInvoker("\nModel Manager Refreshed Model Data!\n");
            await Task.Delay(100);


            msgInvoker("\nAlarm Processing....\n");

            var occured = ExtractAlarmsOccured();
            var solved = ExtractAlarmsSolved();
            msgInvoker($"\nAlarm Getted! occured: {occured.Count} solved: {solved.Count}\n");
            
            await ApplyAlarmsAsync(occured, solved);

            msgInvoker("\nAlarm Processed!\n");
            msgInvoker("[Refresh Report]\n\n" + reportBuffer==""? "--nothing to report--": reportBuffer);
        }

        /// <summary>
        /// 모든 데이터를 초기화
        /// </summary>
        public void Drop(bool isHardDrop) 
        {
            if (isHardDrop)
            {
                areas.Clear();
                obss.Clear();
            }
            alarms.Clear();
            boards.Clear();
            sensors.Clear();
            values.Clear();

        }


        /// <summary>
        /// 새로고침된 정보를 토대로 해소할 알람들의 리스트 추출
        /// </summary>
        /// <returns>해소할 알람들의 리스트</returns>
        List<AlarmLogModel> ExtractAlarmsSolved()
        {
            List<AlarmLogModel> alarmSolved = new List<AlarmLogModel>();

            alarms.ForEach(alarm =>
            {
                try
                {
                    //if (alarm.obsidx != 1)
                    //{
                    //    alarmSolved.Add(alarm); //테스트 용으로 1번 관측소 외의 알람만 해소
                    //    return;
                    //}

                    if (obss.Find(obss => obss.obsidx == alarm.obsidx) is not ObservatoryModel obs)
                        throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - Failed to Find obs {alarm.obsidx}\n");

                    //경계(n == 1) 경보(n == 2)
                    if (new int[2] { 1, 2 }.Contains(alarm.alacode))
                    {
                        if (!sensors.ContainsKey(obs)) throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - no sensors for this obs {obs.obsidx}\n");

                        //센서 확인
                        if (sensors[obs].Find(s => s.hnsidx == alarm.hnsidx && s.boardidx == alarm.boardidx) is not HnsResourceModel sensor)
                            throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - Failed to Find sensor {alarm.obsidx}/{alarm.boardidx}/{alarm.hnsidx}");

                        //미사용중인 센서는 알람 해소하지 않음
                        bool isUsing = int.Parse(sensor.useyn) != 0;
                        if (!isUsing) return;

                        //수리중인 센서는 알람 해소하지 않음
                        bool isFixing = int.Parse(sensor.inspectionflag) != 0;
                        if (isFixing) return;

                        //보드가 존재하고, 그 보드가 설비이상이라면, 해소하지 않음.
                        if (boards[obs].Find(b => b.boardidx == alarm.boardidx && b.obsidx == alarm.obsidx) is BoardStateModel board)
                        {
                            bool isBoardMalfunction = int.Parse(board.stcd) != 0;
                            if (isBoardMalfunction) return;
                        }

                        //현재값의 존재 확인
                        if (values[sensor] is not CurrentDataModel value)
                            throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - Failed to Find value {alarm.obsidx}/{alarm.boardidx}/{alarm.hnsidx}");

                        //현재값이 유효한지?
                        if (!value.val.HasValue)
                            throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - Value doesn't have val. maybe it has not correct alacode {alarm.obsidx}/{alarm.boardidx}/{alarm.hnsidx}");

                        //현재값 수령
                        float currVal = value.val.Value;

                        if (alarm.alacode == 1)
                        {
                            //경계 알람은 현재값이 센서의 경계값보다 낮으면 해결됨
                            if (currVal < sensor.alahival) alarmSolved.Add(alarm);
                        }
                        else if (alarm.alacode == 2)
                        {
                            //경보 알람은 현재값이 센서의 경계값보다 낮으면 해결됨
                            if (currVal < sensor.alahihival) alarmSolved.Add(alarm);
                        }
                    }
                    else //설비이상(0 <= n || n <= 3)
                    {
                        if (!boards.ContainsKey(obs)) throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - no boards for this obs {obs.obsidx}\n");

                        //보드 확인
                        if (boards[obs].Find(b => b.boardidx == alarm.boardidx && b.obsidx == alarm.obsidx) is not BoardStateModel board)
                            throw new Exception($"ExtractAlarmsSolved({alarm.alacode}) - Failed to Find board  {alarm.obsidx}/{alarm.boardidx}");

                        int stcd = int.Parse(board.stcd);

                        //설비이상은 STCD가 0과 같으면 해결
                        if (stcd == 0) alarmSolved.Add(alarm);
                    }

                }
                catch (Exception ex)
                {
                    reportBuffer += ($"{ex.Message}");
                }
            });

            return alarmSolved;
        }

        /// <summary>
        /// 새로고침된 정보를 토대로 발생할 알람들의 리스트 추출
        /// </summary>
        /// <returns>발생할 알람들의 리스트</returns>
        List<AlarmLogModel> ExtractAlarmsOccured()
        {
            List<AlarmLogModel> alarmOccured = new List<AlarmLogModel>();

            //!1 && !2 : 설비이상
            {
                List<BoardStateModel> boardList = new();
                boards.Values.AsEnumerable().ToList().ForEach(boardList.AddRange);

                boardList.ForEach(board =>
                {
                    try
                    {
                        //STCD가 0과 같다면 정상
                        int stcd = int.Parse(board.stcd);
                        if (stcd == 0) return;

                        //이전에 설비이상이 발생했는지 확인
                        if (alarms.Find(a => a.obsidx == board.obsidx && a.boardidx == board.boardidx && a.alacode != 1 && a.alacode != 2) is AlarmLogModel alarm) return;


                        if (obss.Find(obss => obss.obsidx == board.obsidx) is not ObservatoryModel obs)
                            throw new Exception($"ExractAlarmsOccured({board.obsidx}/{board.boardidx}) - Failed to Find obs {board.obsidx}\n");

                        alarmOccured.Add(new AlarmLogModel()
                        {
                            alacode = 0, //설비이상
                            aladt = DateTime.Now.ToString("yyyyMMddHHmmss"),
                            obsidx = board.obsidx,
                            boardidx = board.boardidx,
                            hnsidx = 0, //설비이상은 hnsidx가 없음
                            alahival = null,
                            alahihival = null,
                            currval = null,
                            hnsnm = null,
                            turnoff_flag = null, //해제되지 않은 상태
                            turnoff_dt = null,
                            areanm = obs.areanm ?? "Unknown Area",
                            obsnm = obs.obsnm ?? "Unknown Observatory",
                            alaidx = 0 //DB에 저장 후 자동으로 증가
                        });
                    }
                    catch (Exception ex)
                    {
                        reportBuffer += ($"{ex.Message}\n");
                    }
                });
            }

            List<HnsResourceModel> sensorList = new();
            sensors.Values.AsEnumerable().ToList().ForEach(sensorSubList => sensorList.AddRange(sensorSubList));
            sensorList.ForEach(sensor =>
            {

                if (obss.Find(obss => obss.obsidx == sensor.obsidx) is not ObservatoryModel obs)
                    throw new Exception($"ExractAlarmsOccured({sensor.obsidx}/{sensor.boardidx}/{sensor.hnsidx}) - Failed to Find obs\n");

                try
                {
                    List<BoardStateModel> boardList = new();
                    boards.Values.AsEnumerable().ToList().ForEach(boardSubList => boardList.AddRange(boardSubList));

                    //미사용중인 센서는 알람 해소하지 않음
                    bool isUsing = int.Parse(sensor.useyn) != 0;
                    if (!isUsing) return;

                    //수리중인 센서는 알람 발생하지 않음
                    bool isFixing = int.Parse(sensor.inspectionflag) != 0;
                    if (isFixing) return;

                    //보드가 존재하고, 그 보드가 설비이상이라면, 해소하지 않음.
                    if (boards[obs].Find(b => b.boardidx == sensor.boardidx && b.obsidx == sensor.obsidx) is BoardStateModel board)
                    {
                        bool isBoardMalfunction = int.Parse(board.stcd) != 0;
                        if (isBoardMalfunction) return;
                    }

                    //센서가 속한 보드가 보드이상인지 확인
                    if (alarms.Find(a => a.obsidx == sensor.obsidx && a.boardidx == sensor.boardidx && (a.alacode != 1 && a.alacode != 2)) is AlarmLogModel) return;

                    //현재값의 존재 확인
                    if (values[sensor] is not CurrentDataModel value)
                        throw new Exception($"ExractAlarmsOccured({sensor.obsidx}/{sensor.boardidx}/{sensor.hnsidx}) - Failed to Find value\n");

                    //현재값이 유효한지?
                    if (!value.val.HasValue)
                        throw new Exception($"ExractAlarmsOccured({sensor.obsidx}/{sensor.boardidx}/{sensor.hnsidx}) - Value doesn't have val. maybe it has not correct alacode\n");
                    //현재값 수령
                    float currVal = value.val.Value;

                    //1 : 경계
                    {
                        //이전에 경계 알람이 발생했는지 확인
                        if (alarms.Find(a => a.obsidx == sensor.obsidx && a.boardidx == sensor.boardidx && a.hnsidx == sensor.hnsidx && a.alacode == 1) is AlarmLogModel)
                        { }
                        //경계 알람은 현재값이 센서의 경계값보다 높으면 발생
                        else if (currVal >= sensor.alahival)
                        {
                            alarmOccured.Add(new AlarmLogModel()
                            {
                                alacode = 1, //경계
                                aladt = DateTime.Now.ToString("yyyyMMddHHmmss"),
                                obsidx = sensor.obsidx,
                                boardidx = sensor.boardidx,
                                hnsidx = sensor.hnsidx,
                                alahival = sensor.alahival,
                                alahihival = sensor.alahihival,
                                currval = currVal,
                                hnsnm = sensor.hnsnm,
                                turnoff_flag = null, //해제되지 않은 상태
                                turnoff_dt = null,
                                areanm = obs.areanm ?? "Unknown Area",
                                obsnm = obs.obsnm ?? "Unknown Observatory",
                                alaidx = 0 //DB에 저장 후 자동으로 증가
                            });
                        }
                    }

                    //2 : 경보
                    {
                        //이전에 경보 알람이 발생했는지 확인
                        if (alarms.Find(a => a.obsidx == sensor.obsidx && a.boardidx == sensor.boardidx && a.hnsidx == sensor.hnsidx && a.alacode == 2) is AlarmLogModel) 
                        { }
                        //경보 알람은 현재값이 센서의 경계값보다 높으면 발생
                        else if (currVal >= sensor.alahihival)
                        {
                            alarmOccured.Add(new AlarmLogModel()
                            {
                                alacode = 2, //경보
                                aladt = DateTime.Now.ToString("yyyyMMddHHmmss"),
                                obsidx = sensor.obsidx,
                                boardidx = sensor.boardidx,
                                hnsidx = sensor.hnsidx,
                                alahival = sensor.alahival,
                                alahihival = sensor.alahihival,
                                currval = currVal,
                                hnsnm = sensor.hnsnm,
                                turnoff_flag = null, //해제되지 않은 상태
                                turnoff_dt = null,
                                areanm = obs.areanm ?? "Unknown Area",
                                obsnm = obs.obsnm ?? "Unknown Observatory",
                                alaidx = 0 //DB에 저장 후 자동으로 증가
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    reportBuffer += ($"{ex.Message}\n");
                }
            });
            
            return alarmOccured;
        }

        /// <summary>
        /// 발생, 해소할 알람들의 리스트를 받아 이를 프로그램, DB에 각각 적용함.
        /// </summary>
        /// <param name="occuredList">발생할 알람 리스트</param>
        /// <param name="solvedList">해소할 알람 리스트</param>
        /// <returns></returns>
        public async Task ApplyAlarmsAsync(List<AlarmLogModel> occuredList, List<AlarmLogModel> solvedList) 
        {
            try
            {
                List<Task> loadingTasks = new();

                alarms.RemoveAll(alarms => solvedList.Any(solved => solved.alaidx == alarms.alaidx));
                alarms.AddRange(occuredList);

                occuredList.ForEach(occured => {
                    var task = db.OccureAlarm(occured.hnsidx, occured.boardidx, occured.obsidx, occured.alahival, occured.alahihival, occured.currval, occured.alacode);
                    loadingTasks.Add(task);
                    });

                solvedList.ForEach(solved => {
                    var task = db.SolveAlarm(solved.alaidx);
                    loadingTasks.Add(task);
                });

                await Task.WhenAll(loadingTasks);

            }
            catch (Exception ex)
            {
                reportBuffer += ($"ApplyAlarms Error : {ex.Message}");
            }
        }
    }
}
