using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onthesys.ExeBuild
{
    [System.Serializable]
    public class AlarmLogModel
    {
        public int alaidx;
        public string aladt;
        public string obsnm;
        public string areanm;
        public int hnsidx;
        public int obsidx;
        public int boardidx;
        public float? alahival;
        public float? alahihival;
        public float? currval;
        public string hnsnm;
        public string turnoff_flag;
        public string turnoff_dt;
        public int alacode;
    }
}
