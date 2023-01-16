using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BurntMemory
{
    public class DebugThread
    {
        private AttachState _attachState;
        private ReadWrite _readWrite;
        private uint? _debuggedprocessID;
        private DebugManager _debugManager;

        public Thread Thread { get; private set; }

        public bool NeedToCloseThread
        {
            private get; set;
        }

        public bool ResetBreakpoints
        { private get; set; }

        public bool NewBreakpoints
        { private get; set; }

        private bool _needToInit = true;

        public DebugThread(AttachState attachState, ReadWrite readWrite, DebugManager debugManager)
        {
            _attachState = attachState;
            _readWrite = readWrite;
            _debuggedprocessID = attachState.ProcessID;
            _debugManager = debugManager;
            Thread = new Thread(DebugOuterLoop);
            Thread.Start();

            ResetBreakpoints = false;
            NewBreakpoints = false;
            Trace.WriteLine("DebugThread object created");
        }

        private uint _debuggedProcessID;

        public void DebugOuterLoop()
        {
            while (_needToInit)
            {
                if (_attachState.Attached && PInvokes.DebugActiveProcess((uint)_attachState.ProcessID))//setup debugger for new process
                {
                    _debuggedProcessID = (uint)_attachState.ProcessID;
                    PInvokes.DebugSetProcessKillOnExit(false); //we don't want debugged process to die when we close our debugger
                    _needToInit = false;
                }
                else
                {
                    Trace.WriteLine("retrying: this should never show up?");
                    Thread.Sleep(100); //wait a bit before retrying
                }
            }

            while (!NeedToCloseThread)
            {
                if (ResetBreakpoints) //but first, if we've been told to resetBreakpoints then set CCwritten to false on all breakpoints that don't have CC written to it (interrupt set)
                {
                    Trace.WriteLine("Processing resetBreakpoints");
                    ResetBreakpoints = false;
                    foreach (var item in _debugManager.BreakpointList)
                    {
                        if (_readWrite.ReadBytes(new ReadWrite.Pointer(item.Key))?[0] != 0xCC)
                        {
                            var newitem = item.Value;
                            newitem.CCwritten = false;
                            _debugManager.BreakpointList[item.Key] = newitem;
                        }
                    }
                }

                if (NewBreakpoints) // and second, set interrupts on any new breakpoints that have come in since the last loop (or all of them if resetBreakpoints was run)
                {
                    Trace.WriteLine("Processing newBreakpoints");
                    NewBreakpoints = false;
                    //foreach (Breakpoint bp in _BreakpointList)
                    foreach (var item in _debugManager.BreakpointList)
                    {
                        if (item.Value.CCwritten == false && _readWrite.WriteByte(new ReadWrite.Pointer(item.Key), (byte)0xCC, true))
                        {
                            var newitem = item.Value;
                            newitem.CCwritten = true;
                            _debugManager.BreakpointList[item.Key] = newitem;
                        }
                    }
                }

                //finally we can go to our main inner loop where the breakpoint catching happens
                DebugInnerLoop();
            }
            //application that uses BurntMemory is closing! time to shutdown the thread
            if (_attachState.Attached)
            {
                try
                {
                    PInvokes.DebugActiveProcessStop((uint)_debuggedProcessID);
                }
                catch (Exception ex)
                {
                    Debug.Write("ProcessID was null when running DebugActiveProcessStop, " + ex.ToString());
                }
                //need to remove any 0xCC's in the attached process - if we're still attached.
                //TODO
            }
        }

        private static IntPtr? lastbreakpointhit = null;

        private void DebugInnerLoop()
        {
            bool bb = false;

            IntPtr lpBaseOfDllLoad = IntPtr.Zero;

            IntPtr debugEventPtr = Marshal.AllocHGlobal(188);
            bb = PInvokes.WaitForDebugEvent(debugEventPtr, 100);
            UInt32 dwContinueDebugEvent = PInvokes.DBG_EXCEPTION_NOT_HANDLED;
            if (bb)
            {
                PInvokes.DEBUG_EVENT DebugEvent = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                IntPtr debugInfoPtr = GetIntPtrFromByteArray(DebugEvent.u);

                switch (DebugEvent.dwDebugEventCode)
                {
                    case PInvokes.EXCEPTION_DEBUG_EVENT:
                        PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                        IntPtr ExceptionAddress = ExceptionDebugInfo.ExceptionRecord.ExceptionAddress;

                        switch (ExceptionDebugInfo.ExceptionRecord.ExceptionCode)
                        {
                            case PInvokes.EXCEPTION_SINGLE_STEP:
                                // Reset the instruction before this one to have 0xCC at the start
                                if (lastbreakpointhit != null)
                                {
                                    _readWrite.WriteBytes(new ReadWrite.Pointer(lastbreakpointhit), new byte[] { 0xCC }, true);
                                    PInvokes.FlushInstructionCache((IntPtr)_attachState.processHandle, (IntPtr)lastbreakpointhit, (UIntPtr)3);
                                }

                                dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                break;

                            case PInvokes.EXCEPTION_BREAKPOINT:
                                DebugManager.Breakpoint bp;
                                bool our_breakpoint = _debugManager.BreakpointList.TryGetValue(ExceptionAddress, out bp);
                                if (our_breakpoint)
                                {
                                    dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                    lastbreakpointhit = ExceptionAddress;
                                    PInvokes.CONTEXT64 context64 = new()
                                    {
                                        ContextFlags = PInvokes.CONTEXT_FLAGS.CONTEXT_ALL
                                    };

                                    IntPtr hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT | PInvokes.SET_CONTEXT, false, DebugEvent.dwThreadId);

                                    if (PInvokes.GetThreadContext((IntPtr)hThread, ref context64))
                                    {
                                        // do custom function things
                                        context64 = bp.onBreakpoint(context64);
                                        _readWrite.WriteBytes(new ReadWrite.Pointer(ExceptionAddress), bp.originalCode, true);
                                        context64.Rip--; // go back an instruction to execute original code
                                        context64.EFlags |= 0x100; // Set trap flag, to raise single-step exception
                                        PInvokes.SetThreadContext((IntPtr)hThread, ref context64);
                                        PInvokes.FlushInstructionCache((IntPtr)_attachState.processHandle, ExceptionAddress, (UIntPtr)3);
                                    }

                                    PInvokes.CloseHandle((IntPtr)hThread);
                                }
                                else
                                {
                                    Trace.WriteLine("Unhandled breakpoint at addy: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
                                }
                                break;

                            default:
                                break;
                        }
                        break;

                    case PInvokes.LOAD_DLL_DEBUG_EVENT:
                        // mandatory that we close the handle
                        PInvokes.LOAD_DLL_DEBUG_INFO LoadDLLDebugInfo = (PInvokes.LOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.LOAD_DLL_DEBUG_INFO));
                        PInvokes.CloseHandle(LoadDLLDebugInfo.hFile);

                        //but while we're at it we'll pop an event saying a DLL was loaded.
                        Events.DLL_LOAD_EVENT_INVOKE(this, EventArgs.Empty);
                        break;

                    case PInvokes.UNLOAD_DLL_DEBUG_EVENT:
                        //and likewise
                        Events.DLL_UNLOAD_EVENT_INVOKE(this, EventArgs.Empty);
                        break;

                    case PInvokes.CREATE_THREAD_DEBUG_EVENT:
                        Events.THREAD_LOAD_EVENT_INVOKE(this, EventArgs.Empty);
                        break;

                    case PInvokes.EXIT_THREAD_DEBUG_EVENT:
                        Events.THREAD_UNLOAD_EVENT_INVOKE(this, EventArgs.Empty);
                        break;

                    default:
                        break;
                }
                // Resume executing the thread that reported the debugging event.
                PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
                            (uint)DebugEvent.dwThreadId,
                            dwContinueDebugEvent);
            }
        }

        private static IntPtr GetIntPtrFromByteArray(byte[] byteArray)
        {
            GCHandle pinnedArray = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            IntPtr intPtr = pinnedArray.AddrOfPinnedObject();
            pinnedArray.Free();
            return intPtr;
        }
    }
}