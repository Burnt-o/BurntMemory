using System.Diagnostics;
using System.Runtime.InteropServices;
using Console = System.Diagnostics.Debug;

namespace BurntMemory
{
    public class Debugger
    {
        public readonly Thread _DebugThread;

        // Singleton pattern
        private static readonly Debugger instance = new();

        private static List<Breakpoint> _BreakpointList = new();

        private static bool _ApplicationClosing = false;

        public static bool _MonitorReloads = false;

        private static bool _StartDebugging = false;

        private Debugger()
        {
            // constructor
            Console.WriteLine("Running debugger constructor");
            this._DebugThread = new Thread(new ThreadStart(DebugOuterLoop));
            this._DebugThread.Start();
        }
        // TODO: do I need this?
        public static AttachState AttachState
        {
            get { return BurntMemory.AttachState.Instance; }
        }

        public static Debugger Instance
        {
            get { return instance; }
        }
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
        }

        public void RemoveBreakpoint(string BreakpointName)
        {
            Debug.WriteLine("removing breakpoint");
            foreach (Breakpoint bp in _BreakpointList.ToList())
            {
                if (bp.BreakpointName == BreakpointName)
                {
                    if (AttachState.Attached)
                    {
                        ReadWrite.WriteBytes(bp.Pointer, bp.originalCode, true);
                    }
                    _BreakpointList.Remove(bp);
                }
            }
        }

        public bool SetBreakpoint(string BreakpointName, ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint)
        {
            if (AttachState.Attached)
            {
                RemoveBreakpoint(BreakpointName); //remove breakpoint if it was set before, we'll redo it here
                byte[]? originalCode = ReadWrite.ReadBytes(ptr); //get the original assembly byte at the instruction of the breakpoint - we'l need this for removing the breakpoint later
                Debug.WriteLine("originalCode for bp: " + ptr.ToString() + ", oc: " + originalCode?[0].ToString());

                if (originalCode == null)
                {
                    Debug.WriteLine("Tried to SetBreakpoint but could't read original bytes of instruction.");
                    return false;
                }

                // create a new breakpoint and put it in the breakpoint list;
                _BreakpointList.Add(new Breakpoint(BreakpointName, ptr, onBreakpoint, originalCode));
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



        private static void DebugOuterLoop()
        {
            while (!_ApplicationClosing)
            {

                if (needToStartDebugging)
                {
                    Debug.WriteLine("Processing needToStartDebugging");
                    processIsDebugged = false;
                    try
                    {
                        PInvokes.DebugActiveProcessStop((uint)AttachState.OldProcessID); //remove debugger from old process
                    }
                    catch { }
                    bool success = PInvokes.DebugActiveProcess((uint)AttachState.ProcessID); //setup debugger for new process
                    if (success)
                    {
                        PInvokes.DebugSetProcessKillOnExit(false); //we don't want debugged process to die when we close our debugger
                        processIsDebugged = true;
                        needToStartDebugging = false;
                    }
                    else
                    {
                        Thread.Sleep(100); //wait a bit before retrying
                    }
                }

                //TODO redo the logic here. we'll want it to only DebugActiveProcess if (_MonitorReloads || (_BreakpointList.Count > 0)) (and the process isn't already debugged).
                //and when those conditions become untrue, DebugActiveStop (if process is already debugged, and process isn't closed)
                if (!(processIsDebugged))
                {
                    Debug.WriteLine("sleeping debugger");
                    Debug.WriteLine("processIsDebugged, " + processIsDebugged.ToString());
                    Debug.WriteLine("_MonitorReloads, " + _MonitorReloads.ToString());
                    Debug.WriteLine("_BreakpointList.Count, " + _BreakpointList.Count.ToString());
                    Thread.Sleep(100); //let's not hog the cpu while waiting for debugging to start
                }
                else //Main loop where all the action happens!
                {
                    if (resetBreakpoints) //but first, if we've been told to resetBreakpoints then set CCwritten to false on all breakpoints that don't have CC written to it (interrupt set)
                    {
                        Debug.WriteLine("Processing resetBreakpoints");
                        resetBreakpoints = false;
                        for (int i = 0; i < _BreakpointList.Count; i++)
                        {
                            if (ReadWrite.ReadBytes(_BreakpointList[i].Pointer)?[0] != 0xCC)
                            {
                                Breakpoint temp = new Breakpoint(_BreakpointList[i].BreakpointName, _BreakpointList[i].Pointer, _BreakpointList[i].onBreakpoint, _BreakpointList[i].originalCode, false);
                                _BreakpointList[i] = temp;
                            }
                        }
                    }

                    if (newBreakpoints) // and second, set interrupts on any new breakpoints that have come in since the last loop (or all of them if resetBreakpoints was run)
                    {
                        Debug.WriteLine("Processing newBreakpoints");
                        newBreakpoints = false;
                        //foreach (Breakpoint bp in _BreakpointList)
                        for (int i = 0; i < _BreakpointList.Count; i++)
                        {
                            if (_BreakpointList[i].CCwritten == false)
                            {
                                if (ReadWrite.WriteBytes(_BreakpointList[i].Pointer, new byte[] { 0xCC }, true))
                                {
                                    Breakpoint temp = new Breakpoint(_BreakpointList[i].BreakpointName, _BreakpointList[i].Pointer, _BreakpointList[i].onBreakpoint, _BreakpointList[i].originalCode, true);
                                    _BreakpointList[i] = temp;
                                }
                            }
                        }
                    }

                    //finally we can go to our main inner loop where the breakpoint catching happens
                    Debug.WriteLine("running inner loop");
                DebugInnerLoop();
                }

                if (needToStopDebugging)
                {
                    needToStopDebugging = false;
                    PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
                    
                }


            }
            //application that uses BurntMemory is closing! time to shutdown the thread
            if (AttachState.Attached)
            {
                try
                {
                    PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
                }
                catch (Exception ex)
                { 
                Debug.Write("ProcessID was null when running DebugActiveProcessStop, " + ex.ToString());
                }
                    //need to remove any 0xCC's in the attached process - if we're still attached.
                    //TODO
            }

            

        }


        private static void DebugInnerLoop()
        {
            IntPtr? hThread = null;
            bool bb = false;

            int? lastbreakpointhit = null;
            IntPtr lpBaseOfDllLoad = IntPtr.Zero;

            // we don't want _BreakpointList to get modified while the loop is running, so make a copy
            List<Breakpoint> _BreakpointListTemp = _BreakpointList.ConvertAll(s => new Breakpoint { Pointer = s.Pointer, onBreakpoint = s.onBreakpoint, originalCode = s.originalCode }).ToList();

            IntPtr debugEventPtr = Marshal.AllocHGlobal(188);
            bb = PInvokes.WaitForDebugEvent(debugEventPtr, 100);
            UInt32 dwContinueDebugEvent = PInvokes.DBG_EXCEPTION_NOT_HANDLED;
            if (bb)
            {
                PInvokes.DEBUG_EVENT DebugEvent = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                IntPtr debugInfoPtr = GetIntPtrFromByteArray(DebugEvent.u);

                if (DebugEvent.dwDebugEventCode == PInvokes.EXCEPTION_DEBUG_EVENT && _BreakpointListTemp.Count > 0)
                {
                    PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                    string exceptionDebugStr = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                        (ulong)ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo.ExceptionRecord.ExceptionCode);

                    switch (ExceptionDebugInfo.ExceptionRecord.ExceptionCode)
                    {
                        case PInvokes.EXCEPTION_SINGLE_STEP:
                            // Reset the instruction before this one to have 0xCC at the start
                            // is there a way to do this generically without the lastbreakpointhitvar? If I could read the asm..
                            if (lastbreakpointhit != null)
                            {
                                // TODO: this will totally break if breakpoint list modified during singlestep
                                // YEP it breaks
                                ReadWrite.WriteBytes(_BreakpointListTemp[(int)lastbreakpointhit].Pointer, new byte[] { 0xCC }, true);
                            }

                            PInvokes.FlushInstructionCache((IntPtr)AttachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);
                            dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                            break;

                        case PInvokes.EXCEPTION_BREAKPOINT:
                            // Debug.WriteLine("checking breakpointlist");
                            int BreakpointHit = BreakpointListContains(ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, _BreakpointListTemp);
                            if (BreakpointHit >= 0)
                            {
                                dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                // Debug.WriteLine("breakpoint hit! @ " + BreakpointHit.ToString());
                                lastbreakpointhit = BreakpointHit;
                                PInvokes.CONTEXT64 context64 = new()
                                {
                                    ContextFlags = PInvokes.CONTEXT_FLAGS.CONTEXT_ALL
                                };

                                // IntPtr hThread = Debugger.Instance._DebugThread.;
                                // IntPtr hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT | PInvokes.SET_CONTEXT, false, Debugger.Instance._DebugThread.ManagedThreadId);
                                hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT | PInvokes.SET_CONTEXT, false, DebugEvent.dwThreadId);

                                if (PInvokes.GetThreadContext((IntPtr)hThread, ref context64))
                                {
                                    // do custom function things
                                    context64 = _BreakpointListTemp[BreakpointHit].onBreakpoint(context64);

                                    ReadWrite.WriteBytes(_BreakpointListTemp[BreakpointHit].Pointer, _BreakpointListTemp[BreakpointHit].originalCode, true); // TODO: how to handle this failing?
                                    context64.Rip--; // go back an instruction to execute original code
                                    context64.EFlags |= 0x100; // Set trap flag, to raise single-step exception
                                    PInvokes.SetThreadContext((IntPtr)hThread, ref context64); // TODO: how to handle this failing?
                                }
                                PInvokes.FlushInstructionCache((IntPtr)AttachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);

                                // actually I don't think we need our own threads here.
                                // PInvokes.ResumeThread(hThread);
                                PInvokes.CloseHandle((IntPtr)hThread);
                                hThread = null;
                            }
                            else
                            {
                                Debug.WriteLine("Unhandled breakpoint at addy: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
                            }
                            break;

                        default:
                            break;

                        case PInvokes.EXCEPTION_ACCESS_VIOLATION:
                            // Console.WriteLine("EXCEPTION_ACCESS_VIOLATION");
                            break;
                    }
                }
                else if (DebugEvent.dwDebugEventCode == PInvokes.LOAD_DLL_DEBUG_EVENT)
                {
                    // mandatory that we close the handle
                    PInvokes.LOAD_DLL_DEBUG_INFO LoadDLLDebugInfo = (PInvokes.LOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.LOAD_DLL_DEBUG_INFO));
                    PInvokes.CloseHandle(LoadDLLDebugInfo.hFile);

                    //but while we're at it we'll pop an event saying a DLL was loaded (and info telling which one). AttachState will sub to this and revaluate all module addresses.
                    Debugger.Instance.DLL_LOAD_DEBUG_EVENT(EventArgs.Empty);

                }
                else if (DebugEvent.dwDebugEventCode == PInvokes.UNLOAD_DLL_DEBUG_EVENT)
                {
                    Debugger.Instance.DLL_UNLOAD_DEBUG_EVENT(EventArgs.Empty);
                }
                else if (DebugEvent.dwDebugEventCode == PInvokes.CREATE_THREAD_DEBUG_EVENT)
                {
                    Debugger.Instance.THREAD_LOAD_DEBUG_EVENT(EventArgs.Empty);
                    Debug.WriteLine("CREATE_THREAD_DEBUG_EVENT");
                }
                else if (DebugEvent.dwDebugEventCode == PInvokes.EXIT_THREAD_DEBUG_EVENT)
                {
                    Debugger.Instance.THREAD_UNLOAD_DEBUG_EVENT(EventArgs.Empty);
                    Debug.WriteLine("EXIT_THREAD_DEBUG_EVENT");
                }


                // Debug.WriteLine("Exception: " + DebugEvent.dwDebugEventCode.ToString());
                PInvokes.DEBUG_EVENT DebugEvent2 = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                IntPtr debugInfoPtr2 = GetIntPtrFromByteArray(DebugEvent.u);
                PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo2 = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                string exceptionDebugStr2 = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                    (ulong)ExceptionDebugInfo2.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo2.ExceptionRecord.ExceptionCode);
                // Debug.WriteLine("debuginfo: " + exceptionDebugStr2);

                // Resume executing the thread that reported the debugging event.
                bool bb1 = PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
                            (uint)DebugEvent.dwThreadId,
                            dwContinueDebugEvent);
            }
            

        }

        public event EventHandler DLL_LOAD_EVENT;
        protected virtual void DLL_LOAD_DEBUG_EVENT(EventArgs e)
        {
            EventHandler handler = DLL_LOAD_EVENT;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler DLL_UNLOAD_EVENT;
        protected virtual void DLL_UNLOAD_DEBUG_EVENT(EventArgs e)
        {
            EventHandler handler = DLL_UNLOAD_EVENT;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        public event EventHandler THREAD_LOAD_EVENT;
        protected virtual void THREAD_LOAD_DEBUG_EVENT(EventArgs e)
        {
            EventHandler handler = THREAD_LOAD_EVENT;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler THREAD_UNLOAD_EVENT;
        protected virtual void THREAD_UNLOAD_DEBUG_EVENT(EventArgs e)
        {
            EventHandler handler = THREAD_UNLOAD_EVENT;
            if (handler != null)
            {
                handler(this, e);
            }
        }


        private static IntPtr GetIntPtrFromByteArray(byte[] byteArray)
        {
            GCHandle pinnedArray = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            IntPtr intPtr = pinnedArray.AddrOfPinnedObject();
            pinnedArray.Free();
            return intPtr;
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