using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemorySample.PointerStructs
{
    public class LiteralStructs
    {
        public struct MCC
        {
            public static int[]? MenuInd;
            public static int[]? StateInd;
        }

        public struct General
        {
            public static int[]? Checkpoint;
            public static int[]? Coreload;
            public static int[]? Coresave;
            public static int[]? CPMessageCall;
            public static int[]? Medusa;
            public static int[]? MessageTC;
            public static int[]? Revert;
            public static int[]? Tickcount;
            public static int[]? LevelName;
        }

        public struct Breakpoints
        {
            public static int[]? PlayerAddy;
            public static int[]? Health;
            public static int[]? ShieldBreak;
            public static int[]? ShieldChip;
        }

        public struct BoolMode
        {
            public static int[]? ScriptState;
        }
    }
}
