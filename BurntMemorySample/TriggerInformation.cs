using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemorySample
{
    public class TriggerInformation
    {

        public TriggerInformation(double a, double b, double c, double d, double e, double f, int g, string h, string i, UInt64 j)
        { 
        MinX = a;
            MaxX = b;
            MinY = c;
            MaxY = d;
            MinZ = e;
                MaxZ = f;
            ScriptIndex = g;
            TriggerName = h;
            ScriptName = i;
            ProgressedExpressionIndex = j;
        }


        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public int ScriptIndex { get; set; }

        public string TriggerName { get; set; }

        public string ScriptName { get; set; }

        public UInt64 ProgressedExpressionIndex { get; set; }


    }
}
