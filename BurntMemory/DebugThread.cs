using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BurntMemory
{
    public partial class DebugManager
    {
        private class DebugThread
        {

            public static void DebugOuterLoop()
            {
                while (!_ApplicationClosing)
                {


                    if (debuggerIsOn != debuggerNeedsToBeOn)
                    {
                        if (debuggerIsOn) //need to turn debugger off
                        {
                            try
                            {
                                PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID); //remove debugger from process
                            }
                            catch { }
                            debuggerIsOn = false;
                        }
                        else //need to turn debugger on
                        {
                            try
                            {
                                PInvokes.DebugActiveProcessStop((uint)AttachState.OldProcessID); //remove debugger from old process
                            }
                            catch { }

                            bool success = PInvokes.DebugActiveProcess((uint)AttachState.ProcessID); //setup debugger for new process
                            if (success)
                            {
                                PInvokes.DebugSetProcessKillOnExit(false); //we don't want debugged process to die when we close our debugger
                                debuggerIsOn = true;
                            }
                            else
                            {
                                Thread.Sleep(100); //wait a bit before retrying
                            }

                        }

                    }

                    if (!(debuggerIsOn))
                    {
                        Thread.Sleep(100); //let's not hog the cpu while waiting for debugging to start
                    }
                    else //Main loop where all the action happens!
                    {
                        if (resetBreakpoints) //but first, if we've been told to resetBreakpoints then set CCwritten to false on all breakpoints that don't have CC written to it (interrupt set)
                        {
                            Trace.WriteLine("Processing resetBreakpoints");
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
                            Trace.WriteLine("Processing newBreakpoints");
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
                        DebugInnerLoop();
                    }

                    if (needToStopDebugging)
                    {
                        needToStopDebugging = false;
                        try
                        {
                            PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
                        }
                        catch { Trace.WriteLine("tried to run DebugActiveProcessStop but failed - process was probably closed"); }

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


            private static IntPtr? lastbreakpointhit = null;
            private static void DebugInnerLoop()
            {
                IntPtr? hThread = null;
                bool bb = false;

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
                                    //Trace.WriteLine("rip of singlestep exception: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
                                    ReadWrite.WriteBytes(new ReadWrite.Pointer(lastbreakpointhit), new byte[] { 0xCC }, true);
                                }

                                PInvokes.FlushInstructionCache((IntPtr)AttachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);
                                dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                break;

                            case PInvokes.EXCEPTION_BREAKPOINT:
                                // Trace.WriteLine("checking breakpointlist");
                                int BreakpointHit = BreakpointListContains(ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, _BreakpointListTemp);
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

                                        ReadWrite.WriteBytes(_BreakpointListTemp[BreakpointHit].Pointer, _BreakpointListTemp[BreakpointHit].originalCode, true); // TODO: how to handle this failing?
                                        context64.Rip--; // go back an instruction to execute original code
                                        context64.EFlags |= 0x100; // Set trap flag, to raise single-step exception
                                        PInvokes.SetThreadContext((IntPtr)hThread, ref context64); // TODO: how to handle this failing?
                                    }
                                    PInvokes.FlushInstructionCache((IntPtr)AttachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)60);
                                    // PInvokes.FlushInstructionCache((IntPtr)AttachState.processHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)60);

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

                        //but while we're at it we'll pop an event saying a DLL was loaded (and info telling which one). AttachState will sub to this and revaluate all module addresses.
                        Events.DLL_LOAD_EVENT_INVOKE(DebugManager.Instance, EventArgs.Empty);

                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.UNLOAD_DLL_DEBUG_EVENT)
                    {
                        Events.DLL_UNLOAD_EVENT_INVOKE(DebugManager.Instance, EventArgs.Empty);
                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.CREATE_THREAD_DEBUG_EVENT)
                    {
                        Events.THREAD_LOAD_EVENT_INVOKE(DebugManager.Instance, EventArgs.Empty);
                    }
                    else if (DebugEvent.dwDebugEventCode == PInvokes.EXIT_THREAD_DEBUG_EVENT)
                    {
                        Events.THREAD_UNLOAD_EVENT_INVOKE(DebugManager.Instance, EventArgs.Empty);

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


        }
        
    }
}
