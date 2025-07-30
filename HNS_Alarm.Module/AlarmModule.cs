using HNS_Alarm.Module.Manager;
using System;
using System.Runtime.CompilerServices;

namespace HNS_Alarm.Module
{
    public class AlarmModule : IDisposable
    {
        private CancellationTokenSource cts;
        private Task? moduleTask;
        private float intervalSec;
        private bool disposedValue;
        private DbManager db;
        private ModelManager model;
        private LogicManager logic;
        private ulong loopCount = 0;

        /// <summary>
        /// AlarmModule 생성자입니다. 생성 직후, 알람 모듈은 비활성화 상태이며
        /// bool Start() 메서드를 호출하여 활성화할 수 있습니다.
        /// </summary>
        /// <param name="parameter">알람 모듈의 파라미터를 정의하는 레코드입니다.</param>
        public AlarmModule(Parameter parameter)
        {
            db = new DbManager(InvokeInternalMessage, parameter.dbUrl);
            model = new ModelManager(InvokeInternalMessage, db);
            logic = new LogicManager(InvokeInternalMessage, model);

            cts = new CancellationTokenSource();
            moduleTask = null;
            intervalSec = parameter.intervalSec;
        }


        /// <summary>
        /// AlarmModule을 시작합니다.
        /// 활성화된 알람 모듈은 주기적으로 데이터 수집, 알람 판단, 알람 생성 및 해제 작업을 수행합니다.
        /// bool Quit(out string msg) 메서드를 호출하여 비활성화할 수 있습니다.
        /// </summary>
        /// <param name="msg">활성화 과정에서 실패시, 실패 이유에 대해 반환합니다.</param>
        /// <returns>활성화 성패 여부입니다.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool Start() 
        {
            if (moduleTask != null) 
            {
                InvokeInternalMessage("Failed to Start AlarmModule! It has already moduleTask in work!\n");
                return false;
            }
            cts = new CancellationTokenSource();
            moduleTask = Task.Run(() => Run(cts.Token));

            InvokeInternalMessage("Succeed to Start AlarmModule!\n");
            return true;
        }

        /// <summary>
        /// AlarmModule을 중지합니다.
        /// 알람 모듈의 데이터 수집, 알람 판단, 알람 생성 및 해제 작업이 모두 중지되며,
        /// 내부 타이머 또한 모두 초기화됩니다. 비활성화된 모듈은 bool Start() 메서드를
        /// 통하여 다시 활성화시킬 수있습니다.
        /// </summary>
        /// <returns>비활성화 성패 여부입니다.</returns>
        public bool Quit()
        {
            if (moduleTask == null)
            {
                InvokeInternalMessage("Failed to Stop AlarmModule! It has no moduleTask in work!\n");
                return false;
            }

            cts.Cancel();

            try
            {
                moduleTask.Wait();
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                // 정상적인 취소이므로 무시
            }

            InvokeInternalMessage("Succeed to Stop AlarmModule!\n");
            moduleTask = null;
            cts = null;
            return true;
        }
        
        private async Task Run(CancellationToken token)
        {

            while (!token.IsCancellationRequested)
            {
                // 실제 작업 로직
                InvokeInternalMessage($"---------[{loopCount++}번째 실행 : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]---------\n");


                if (model.IsSetupped())
                {
                    if(loopCount!= 1)
                        InvokeInternalMessage($"It is not first step when setuping ModelManager. it looks has \n");
                    model.Setup();
                }
                else
                    await model.Refresh();


                await Task.Delay((int)Math.Ceiling(intervalSec) * 1000, token); // 취소 가능하게 Delay
            }

            InvokeInternalMessage($"---------[프로그램 종료 : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}]---------\n");
        }

        /// <summary>
        /// 알람 모듈에서 내부 메시지를 전달하기 위한 이벤트입니다.
        /// 해당 이벤트에 이벤트를 등록하면 디버그용 내부 로그들을 받을 수 있습니다.
        /// </summary>
        public event Action<string>? OnInternalMessage;
        public void InvokeInternalMessage(string msg)
        {
            OnInternalMessage?.Invoke(msg);
        }


        /// <summary>
        /// 알람 모듈의 파라미터를 정의하는 레코드입니다.
        /// </summary>
        public record Parameter 
        {
            /// <summary>
            /// 데이터베이스 URL
            /// </summary>
            public string? dbUrl;
            /// <summary>
            /// 데이터 수집 주기 (초 단위)
            /// </summary>
            public float intervalSec;
        }


        [Obsolete("아직 구현되지 않음")]
        public Statement statement = Statement.INACITIVE;

        [Obsolete("아직 구현되지 않음")]
        public enum Statement 
        {
            INACITIVE,
            HEALTHY,
            ERROR,
        }



        #region Dispose()

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue) return;

            if (disposing)
            {
                cts.Cancel();

                try
                {
                    moduleTask.Wait(); // 작업 완료까지 대기
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
                {
                    // 정상적인 취소이므로 무시
                }

                cts.Dispose();
            }

            disposedValue = true;
        }

        ~AlarmModule()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
