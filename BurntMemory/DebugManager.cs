using System.Diagnostics;
using System.Runtime.InteropServices;
using Console = System.Diagnostics.Debug;

namespace BurntMemory
{
    public partial class AttachState
    {
        public readonly Thread _DebugThread;

        // Singleton pattern

        private static List<Breakpoint> _BreakpointList = new();

        private static bool _ApplicationClosing = false;

        public static bool _MonitorReloads = false;

        private static bool _StartDebugging = false;


        // TODO: do I need this?



        public bool ApplicationClosing
        {
            get { return _ApplicationClosing; }
            set { _ApplicationClosing = value; }
        }

        public void ClearBreakpoints()
        {
            foreach (Breakpoint bp in _BreakpointList)
            {
                if (ReadWrite.ReadBytes(bp.Pointer, 1)?[0] == 0xCC)
                {
                    ReadWrite.WriteBytes(bp.Pointer, bp.originalCode, true);
                }
            }
            _BreakpointList.Clear();
            debuggerNeedsToBeOn = ShouldDebuggerBeOn();
        }

        public void RemoveBreakpoint(string BreakpointName)
        {
            Trace.WriteLine("removing breakpoint");
            foreach (Breakpoint bp in _BreakpointList.ToList())
            {
                if (bp.BreakpointName == BreakpointName)
                {
                    if (Attached)
                    {
                        ReadWrite.WriteBytes(bp.Pointer, bp.originalCode, true);
                    }
                    _BreakpointList.Remove(bp);
                }
            }
            debuggerNeedsToBeOn = ShouldDebuggerBeOn();
        }

        public bool SetBreakpoint(string BreakpointName, ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint)
        {
            if (Attached)
            {
                RemoveBreakpoint(BreakpointName); //remove breakpoint if it was set before, we'll redo it here
                byte[]? originalCode = ReadWrite.ReadBytes(ptr); //get the original assembly byte at the instruction of the breakpoint - we'l need this for removing the breakpoint later
                Trace.WriteLine("originalCode for bp: " + ptr.ToString() + ", oc: " + originalCode?[0].ToString());

                if (originalCode == null)
                {
                    Trace.WriteLine("Tried to SetBreakpoint but could't read original bytes of instruction.");
                    return false;
                }

                // create a new breakpoint and put it in the breakpoint list;
                _BreakpointList.Add(new Breakpoint(BreakpointName, ptr, onBreakpoint, originalCode));
                debuggerNeedsToBeOn = ShouldDebuggerBeOn();
                newBreakpoints = true;
                return true;

            }
            return false;
        }

        private static int BreakpointListContains(IntPtr addy, List<Breakpoint> BPList)
        {
            if (BPList == null)
            {
                return -1;
            }

            if (BPList.Count == 0)
            {
                return -2;
            }

            for (int i = 0; i < BPList.Count; i++)
            {
                if (ReadWrite.ResolvePointer(BPList[i].Pointer) == addy)
                {
                    return i;
                }
            }

            return -3;
        }


        //debug loop control vars
        public static bool needToStartDebugging = false;
        public static bool processIsDebugged = false;
        public static bool needToStopDebugging = false;
        public static bool newBreakpoints = false;
        public static bool resetBreakpoints = false;

        public static bool debuggerIsOn = false;
        public static bool debuggerNeedsToBeOn = false;

        private static bool ShouldDebuggerBeOn()
        {
            return (_MonitorReloads || (_BreakpointList.Count > 0));
        }




    


        private static IntPtr GetIntPtrFromByteArray(byte[] byteArray)
        {
            GCHandle pinnedArray = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            IntPtr intPtr = pinnedArray.AddrOfPinnedObject();
            pinnedArray.Free();
            return intPtr;
        }

        public void GracefullyCloseDebugger()
        {
            if (Attached)
            {
                BurntMemory.AttachState.Instance.ClearBreakpoints();
            }

            this.ApplicationClosing = true; //a flag to tell the DebugThread to stop what it's doing after it's current loop
            if (!this._DebugThread.Join(1000)) // wait for thread to finish executing, or 1s
            {
                Trace.WriteLine("DebugThread FAILED to shut down :(");
            }
            else
            {
                Trace.WriteLine("DebugThread successfully shut down"); //this should always happen
            }

        }



        public struct Breakpoint
        {
            public string BreakpointName;
            public Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;
            public byte[] originalCode;
            public ReadWrite.Pointer? Pointer;
            public bool CCwritten;
            public Breakpoint(string BreakpointName, ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint, byte[] originalCode, bool CCwritten = false)
            {
                this.BreakpointName = BreakpointName;
                this.Pointer = ptr;
                this.onBreakpoint = onBreakpoint;
                this.originalCode = originalCode;
                this.CCwritten = CCwritten;
            }
        }
        /*
        I want a singleton of the Debugger instance. This is to always exist whether we're actually debugging or not; we'll instantiate it when AttachState is instantiated.
        A seperate thread will be created on this instantiantion too - this DebugThread is where the actual catching will happen, when "KeepDebugging" flag is true.

        We'll have some sort of breakpoint struct (containning address, original instruction, function to run).
        A DebugSetbreakpoint function handles adding values to that struct; if the struct was previously empty, then we know to set KeepDebugging true.
        Likewise a DebugRemovebreakpoint removes values from the stuct; if it is now empty, we can set KeepDebugging false.

        RE the debug thread right away. If while(keepdebugging) is false, we can just while(sleep(1000)) til it isn't.
        at the same time as passing it keepdebugging, we'll chuck it the process ID to use. or well let it access it from attachstate. and access the breakpoint list from here.
        yeah that may be a MUCH simplier approach. Thread is always going.

        */
    }
}