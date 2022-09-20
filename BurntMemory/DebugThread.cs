using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                while (!NeedToCloseThread)
                {


                    if (_needToInit)
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
                    else //Main loop where all the action happens!
                    {
                        if (ResetBreakpoints) //but first, if we've been told to resetBreakpoints then set CCwritten to false on all breakpoints that don't have CC written to it (interrupt set)
                        {
                            Trace.WriteLine("Processing resetBreakpoints");
                            ResetBreakpoints = false;
                            for (int i = 0; i < _debugManager.BreakpointList.Count; i++)
                            {
                                if (_readWrite.ReadBytes(_debugManager.BreakpointList[i].Pointer)?[0] != 0xCC)
                                {
                                    DebugManager.Breakpoint temp = new (_debugManager.BreakpointList[i].BreakpointName, _debugManager.BreakpointList[i].Pointer, _debugManager.BreakpointList[i].onBreakpoint, _debugManager.BreakpointList[i].originalCode, false);
                                _debugManager.BreakpointList[i] = temp;
                                }
                            }
                        }

                        if (NewBreakpoints) // and second, set interrupts on any new breakpoints that have come in since the last loop (or all of them if resetBreakpoints was run)
                        {
                            Trace.WriteLine("Processing newBreakpoints");
                            NewBreakpoints = false;
                            //foreach (Breakpoint bp in _BreakpointList)
                            for (int i = 0; i < _debugManager.BreakpointList.Count; i++)
                            {
                                if (_debugManager.BreakpointList[i].CCwritten == false)
                                {
                                    if (_readWrite.WriteBytes(_debugManager.BreakpointList[i].Pointer, new byte[] { 0xCC }, true))
                                    {
                                        DebugManager.Breakpoint temp = new (_debugManager.BreakpointList[i].BreakpointName, _debugManager.BreakpointList[i].Pointer, _debugManager.BreakpointList[i].onBreakpoint, _debugManager.BreakpointList[i].originalCode, true);
                                    _debugManager.BreakpointList[i] = temp;
                                    }
                                }
                            }
                        }

                        //finally we can go to our main inner loop where the breakpoint catching happens
                        DebugInnerLoop();
                    }



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
                IntPtr? hThread = null;
                bool bb = false;

                IntPtr lpBaseOfDllLoad = IntPtr.Zero;

            // we don't want _BreakpointList to get modified while the loop is running, so make a copy
            List<DebugManager.Breakpoint> _BreakpointListTemp;
            if (_debugManager.BreakpointList.Count > 0)
            {
                //timing/thread-safety issue here I need to fix
                _BreakpointListTemp = _debugManager.BreakpointList.ConvertAll(s => new DebugManager.Breakpoint { BreakpointName = s.BreakpointName, Pointer = s.Pointer, onBreakpoint = s.onBreakpoint, originalCode = s.originalCode }).ToList();
                
            }
            else
            {
                _BreakpointListTemp = new List<DebugManager.Breakpoint>();
            }
                

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
                                    //Trace.WriteLine("rip of singlestep exception: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
                                    _readWrite.WriteBytes(new ReadWrite.Pointer(lastbreakpointhit), new byte[] { 0xCC }, true);
                                }

                                PInvokes.FlushInstructionCache((IntPtr)_attachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);
                                dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                break;

                            case PInvokes.EXCEPTION_BREAKPOINT:
                                // Trace.WriteLine("checking breakpointlist");
                                int BreakpointHit = _debugManager.BreakpointListContains(ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, _BreakpointListTemp);
                                if (BreakpointHit >= 0)
                                {
                                    dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                    // Trace.WriteLine("breakpoint hit! @ " + BreakpointHit.ToString());
                                    lastbreakpointhit = ExceptionDebugInfo.ExceptionRecord.ExceptionAddress;
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

                                        _readWrite.WriteBytes(_BreakpointListTemp[BreakpointHit].Pointer, _BreakpointListTemp[BreakpointHit].originalCode, true); // TODO: how to handle this failing?
                                        context64.Rip--; // go back an instruction to execute original code
                                        context64.EFlags |= 0x100; // Set trap flag, to raise single-step exception
                                        PInvokes.SetThreadContext((IntPtr)hThread, ref context64); // TODO: how to handle this failing?
                                    }
                                    PInvokes.FlushInstructionCache((IntPtr)_attachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)60);
                                    // PInvokes.FlushInstructionCache((IntPtr)processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)60);

                                    // actually I don't think we need our own threads here.
                                    // PInvokes.ResumeThread(hThread);
                                    PInvokes.CloseHandle((IntPtr)hThread);
                                    hThread = null;
                                }
                                else
                                {
                                    Trace.WriteLine("Unhandled breakpoint at addy: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
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

                        //but while we're at it we'll pop an event saying a DLL was loaded (and info telling which one). AttachStateInstance will sub to this and revaluate all module addresses.
                        Events.DLL_LOAD_EVENT_INVOKE(this, EventArgs.Empty);

                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.UNLOAD_DLL_DEBUG_EVENT)
                    {
                        Events.DLL_UNLOAD_EVENT_INVOKE(this, EventArgs.Empty);
                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.CREATE_THREAD_DEBUG_EVENT)
                    {
                        Events.THREAD_LOAD_EVENT_INVOKE(this, EventArgs.Empty);
                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.EXIT_THREAD_DEBUG_EVENT)
                    {
                        Events.THREAD_UNLOAD_EVENT_INVOKE(this, EventArgs.Empty);

                    }


                    // Trace.WriteLine("Exception: " + DebugEvent.dwDebugEventCode.ToString());
                    PInvokes.DEBUG_EVENT DebugEvent2 = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                    IntPtr debugInfoPtr2 = GetIntPtrFromByteArray(DebugEvent.u);
                    PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo2 = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                    string exceptionDebugStr2 = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                        (ulong)ExceptionDebugInfo2.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo2.ExceptionRecord.ExceptionCode);
                    // Trace.WriteLine("debuginfo: " + exceptionDebugStr2);

                    // Resume executing the thread that reported the debugging event.
                    bool bb1 = PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
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
