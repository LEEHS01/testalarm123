using Newtonsoft.Json;
using Onthesys.ExeBuild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HNS_Alarm.Module.Manager
{
    class DbManager
    {
        private readonly Action<string> msgInvoker;
        string url = "http://192.168.1.20:1933";

        const bool SHOW_QUERY_LOG = false; // 쿼리 로그 출력 여부;

        public DbManager(Action<string> msgInvoker, string dbUrl)
        {
            this.msgInvoker = msgInvoker;
            this.url = dbUrl;
        }

        /// <summary>
        /// 관측소 목록 조회
        /// </summary>
        /// <returns></returns>
        public async Task<List<ObservatoryModel>> GetObss()
        {
            var query = "EXEC GET_OBS;";
            try
            {
                var json = await ResponseAPIString("SELECT", query);
                return JsonConvert.DeserializeObject<List<ObservatoryModel>>(json)
                       ?? new List<ObservatoryModel>();
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message+"\n");
                return new List<ObservatoryModel>();
            }
        }

        /// <summary>
        /// 지역 목록 조회
        /// </summary>
        /// <returns></returns>
        public async Task<List<AreaDataModel>> GetAreas()
        {
            var query = $"SELECT * FROM TB_AREA;";
            try
            {
                var json = await ResponseAPIString("SELECT", query);
                return JsonConvert.DeserializeObject<List<AreaDataModel>>(json)
                       ?? new List<AreaDataModel>();
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
                return new List<AreaDataModel>();
            }
        }
        
        /// <summary>
        /// 관측소 1개에 대한 센서 제원 조회
        /// </summary>
        /// <param name="obsId">관측소 ID</param>
        /// <returns></returns>
        public async Task<List<HnsResourceModel>> GetHnsByObsId(int obsId)
        {
            var query = $"EXEC GET_SETTING @obsidx = {obsId};";
            try
            {
                var json = await ResponseAPIString("SELECT", query);
                return JsonConvert.DeserializeObject<List<HnsResourceModel>>(json)
                       ?? new List<HnsResourceModel>();
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
                return new List<HnsResourceModel>();
            }
        }
        
        /// <summary>
        /// 관측소 1개에 대한 최근 계측값 조회
        /// </summary>
        /// <param name="obsId">관측소 ID</param>
        /// <returns></returns>
        public async Task<List<CurrentDataModel>> GetLateValueByObsId(int obsId)
        {
            var query = $"EXEC GET_CURRENT_TOXI @obsidx = {obsId};";
            try
            {
                var json = await ResponseAPIString("SELECT", query);


                return JsonConvert.DeserializeObject<List<CurrentDataModel>>(json)
                       ?? new List<CurrentDataModel>();
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
                return new List<CurrentDataModel>();
            }

        }

        /// <summary>
        /// 현재 활성화된 알람 로그 조회
        /// </summary>
        /// <returns></returns>
        public async Task<List<AlarmLogModel>> GetAlarmActivated()
        {
            var query = $"EXEC GET_CURRENT_ALARM_LOG";
            try
            {
                var json = await ResponseAPIString("SELECT", query);
                return JsonConvert.DeserializeObject<List<AlarmLogModel>>(json)
                       ?? new List<AlarmLogModel>();
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
                return new List<AlarmLogModel>();
            }
        }

        /// <summary>
        /// 보드 상태 조회
        /// </summary>
        /// <returns></returns>
        public async Task<List<BoardStateModel>> GetBoardStates(int obsId)
        {
            var query = $"SELECT TOP 1 * FROM TB_BOARD_STATE_DATA WHERE obsidx = {obsId} ORDER BY obsdt DESC";
            try
            {
                //Console.WriteLine("[QUERY] : " + query);
                //var qquery = $@"EXEC sp_spaceused 'dbo.TB_BOARD_STATE_DATA';";
                //Console.WriteLine("[QUERY] : " + qquery);
                //var jjson = await ResponseAPIString("SELECT", qquery);
                //Console.WriteLine("[QUERY] : " + qquery + "\n[RECEIVED] : " + jjson);

                var json = await ResponseAPIString("SELECT", query);

                //Console.WriteLine("[QUERY] : " + query + "\n[RECEIVED] : " + json);

                return JsonConvert.DeserializeObject<List<BoardStateModel>>(json)
                       ?? new List<BoardStateModel>();
            }
            catch (Exception ex)
            {
                //Console.WriteLine("[QUERY] : " + query + "\n[ex] : " + ex);
                msgInvoker("Error: " + ex.Message + "\n");
                return new List<BoardStateModel>();
            }

        }


        //보드 조회
        [Obsolete("아직 구현되지 않음")]
        public async Task SetBoardFixing(int obsId, int boardId, bool isFixing)
        {
            string query = $@"EXEC SET_BOARD_ISFIXING @obsIdx = {obsId}, @boardIdx = {boardId}, @isFixing = {isFixing};";

            try
            {
                var json = await ResponseAPIString("SELECT", query);
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
            }
        }

        [Obsolete("아직 구현되지 않음")]
        public async Task<bool> GetBoardFixing(int obsId, int boardId)
        {
            var query = $@"DECLARE @result BIT;
                EXEC GET_BOARD_ISFIXING @obsIdx = {obsId}, @boardIdx = {boardId}, @isFixing = @result OUTPUT;
                SELECT isFixing = @result;";

            try
            {
                var json = await ResponseAPIString("SELECT", query);
                var result = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                if (result != null && result.Count > 0)
                {
                    bool isFixing = Convert.ToBoolean(result[0]["isFixing"]);
                    return isFixing;
                }
                else
                {
                    msgInvoker("DbManager - GetBoardFixing : 응답 데이터 없음" + "\n");
                    return false;
                }
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
                return false;
            }
        }

        public async Task OccureAlarm(int hnsId, int boardId, int obsId, float? alaHiVal, float? alaHiHiVal, float? currVal, int alaCode)
        {
            var query = $@"INSERT INTO TB_ALARM_DATA
                (HNSIDX, OBSIDX, BOARDIDX, ALAHIVAL, ALAHIHIVAL, CURRVAL, ALACODE, ALADT, TURNOFF_FLAG, TURNOFF_DT)
            VALUES
                ({hnsId}, {obsId}, {boardId}, 
                {(alaHiVal.HasValue ? alaHiVal.Value.ToString() : "NULL")}, 
                {(alaHiHiVal.HasValue ? alaHiHiVal.Value.ToString() : "NULL")},
                {(currVal.HasValue ? currVal.Value.ToString() : "NULL")},
                {alaCode}, GETDATE(), NULL, NULL);
            ";

            try
            {
                //Console.WriteLine("[QUERY] : " + query + "\n");
                var json = await ResponseAPIString("SELECT", query);
                //Console.WriteLine("\n[RECEIVED] : " + json);
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
            }
        }

        public async Task SolveAlarm(int alaIdx)
        {
            var query = $@"
                UPDATE TB_ALARM_DATA
                SET 
                    TURNOFF_FLAG = 'Y',
                    TURNOFF_DT = GETDATE()
                WHERE ALAIDX = (
                    SELECT TOP 1 ALAIDX={alaIdx}
                    FROM TB_ALARM_DATA
                    ORDER BY ALADT DESC
                );
            ";

            try
            {
                var json = await ResponseAPIString("SELECT", query);
            }
            catch (Exception ex)
            {
                msgInvoker("Error: " + ex.Message + "\n");
            }
        }



        #region 기본 통신 코드
        async Task<string> ResponseAPIString(string type, string query)
        {
            var data = new QueryPayload
            {
                SQLType = type,
                SQLquery = query
            };

            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                try
                {
                    string url = this.url ?? "192.168.1.20:2000" + "/query/";
                    var response = await client.PostAsync(url, content);
                    response.EnsureSuccessStatusCode(); // 예외 throw if not 2xx

                    string responseBody = await response.Content.ReadAsStringAsync();
                    
                    if(SHOW_QUERY_LOG)
                        Console.WriteLine("[QUERY] : " + query + "\n[RECEIVED] : " + responseBody);
                    
                    return responseBody;
                }
                catch (HttpRequestException e)
                {
                    if (SHOW_QUERY_LOG)
                        Console.WriteLine("[QUERY] : " + query + "\n[Error] : " + e);
                    return $"Error: {e.Message}";
                }
            }
        }
        class QueryPayload
        {
            public string SQLType { get; set; }
            public string SQLquery { get; set; }
        }
        #endregion
    }
}
