using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BurntMemory;

namespace BurntMemorySample.PointerStructs
{
    public class PointerStructs
    {
        public struct MCC
        {
            public static ReadWrite.Pointer? MenuInd;
            public static ReadWrite.Pointer? StateInd;
        }

        public struct General
        {
            public static ReadWrite.Pointer? Checkpoint;
            public static ReadWrite.Pointer? Coreload;
            public static ReadWrite.Pointer? Coresave;
            public static ReadWrite.Pointer? CPMessageCall;
            public static ReadWrite.Pointer? Medusa;
            public static ReadWrite.Pointer? MessageTC;
            public static ReadWrite.Pointer? Revert;
            public static ReadWrite.Pointer? Tickcount;
            public static ReadWrite.Pointer? LevelName;
        }

        public struct Breakpoints
        {
            public static ReadWrite.Pointer? PlayerAddy;
            public static ReadWrite.Pointer? Health;
            public static ReadWrite.Pointer? ShieldBreak;
            public static ReadWrite.Pointer? ShieldChip;
        }

        public struct BoolMode
        {
            public static ReadWrite.Pointer? ScriptState;
        }



        
       

    }
}
