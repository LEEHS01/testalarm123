 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onthesys.ExeBuild
{
    [System.Serializable]
    public class ChartDataModel
    {
        public string obsdt;
        public int? hnsidx;
        public int? obsidx;
        public int? boardidx;

        public float val;
        public float aival;
    }
}
