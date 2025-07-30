using Onthesys.ExeBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HNS_Alarm.Module.Manager
{
    class LogicManager
    {
        private readonly ModelManager md;
        private readonly Action<string> msgInvoker;


        public LogicManager(Action<string> msgInvoker, ModelManager model)
        {
            this.msgInvoker = msgInvoker;
        }



        










    }
}
