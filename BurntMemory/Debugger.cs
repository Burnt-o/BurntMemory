﻿using Console = System.Diagnostics.Debug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;


namespace BurntMemory
{
    public sealed class Debugger
    {

        
        private Debugger()
        {
            //constructor
            Console.WriteLine("Running debugger constructor");
            _DebugThread = new Thread(new ThreadStart(DebugLoop));
            _DebugThread.Start();

        }


        public readonly Thread _DebugThread;



        //Singleton pattern
        private static readonly Debugger instance = new();


        public static Debugger Instance
        {
            get { return instance; }
        }


        //TODO: do I need this?
        public static AttachState AttachState
        {
            get { return BurntMemory.AttachState.Instance; }

        }

        public struct Breakpoint
        {
            public ReadWrite.Pointer? Pointer;
            public Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;
            public byte[] originalCode;

            public Breakpoint(ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint, byte[] originalCode)
            {
                this.Pointer = ptr;
                this.onBreakpoint = onBreakpoint;
                this.originalCode = originalCode;
            }
        }



        private static List<Breakpoint> _BreakpointList = new();


        private static bool _KeepDebugging = false;
        private static bool _StartDebugging = false;
        private static bool _StopDebugging = false;
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

        private static IntPtr GetIntPtrFromByteArray(byte[] byteArray)
        {
            GCHandle pinnedArray = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            IntPtr intPtr = pinnedArray.AddrOfPinnedObject();
            pinnedArray.Free();
            return intPtr;
        }
        


        static int BreakpointListContains(IntPtr addy, List<Breakpoint> BPList)
        {
            if (BPList == null)
                return -1;

            if (BPList.Count == 0)
                return -2;

            for (int i = 0; i < BPList.Count; i++)
            {

                if (ReadWrite.ResolvePointer(BPList[i].Pointer) == addy)
                    return i;
            }

            return -3;
        }

        static void DebugLoop()
        {
            IntPtr? hThread = null;
            bool bb = false;
            bool ExitThread = false;
            try
            {
                int? lastbreakpointhit = null;
                IntPtr lpBaseOfDllLoad = IntPtr.Zero;
                while (!ExitThread)
                {
                    if (!_KeepDebugging)
                    {
                        if (_StopDebugging)
                        {
                            _StopDebugging = false;
                            try
                            {
                                //Marshal.FreeHGlobal(debugEventPtr);
                                PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
                            }
                            catch
                            {
                            } //process was probably closed or invalid so no need to stop debugging.

                        }

                        Thread.Sleep(100);
                    }
                    else
                    {
                        if (_StartDebugging)
                        {
                            //Setup stuff
                            _StartDebugging = false;
                            Debug.WriteLine("Starting debugging");
                            try
                            {
                                PInvokes.DebugActiveProcess((uint)AttachState.ProcessID);
                                PInvokes.DebugSetProcessKillOnExit(false);
                            }
                            catch { _KeepDebugging = false; _StopDebugging = true; Console.WriteLine("Somethings gone horrible wrong on DebugActiveProcess" + AttachState.ProcessID.ToString() + " " + PInvokes.GetLastError().ToString()); } //TODO: figure out error handling here.

                        }

                        //main debug thread loop logic:
                        //we don't want _BreakpointList to get modified while the loop is running, so make a copy
                        List<Breakpoint> _BreakpointListTemp = _BreakpointList.ConvertAll(s => new Breakpoint { Pointer = s.Pointer, onBreakpoint = s.onBreakpoint, originalCode = s.originalCode }).ToList();

                        IntPtr debugEventPtr = Marshal.AllocHGlobal(188);
                        bb = PInvokes.WaitForDebugEvent(debugEventPtr, 1000);
                        UInt32 dwContinueDebugEvent = PInvokes.DBG_EXCEPTION_NOT_HANDLED;
                        if (bb)
                        {
                            PInvokes.DEBUG_EVENT DebugEvent = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                            IntPtr debugInfoPtr = GetIntPtrFromByteArray(DebugEvent.u);


                            if (DebugEvent.dwDebugEventCode == PInvokes.EXCEPTION_DEBUG_EVENT)
                            {
                                PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                                string exceptionDebugStr = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                                    (ulong)ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo.ExceptionRecord.ExceptionCode);


                                switch (ExceptionDebugInfo.ExceptionRecord.ExceptionCode)
                                {
                                    case PInvokes.EXCEPTION_SINGLE_STEP:
                                        //Reset the instruction before this one to have 0xCC at the start
                                        //is there a way to do this generically without the lastbreakpointhitvar? If I could read the asm..

                                        if (lastbreakpointhit != null)
                                            //TODO: this will totally break if breakpoint list modified during singlestep
                                            //YEP it breaks
                                            ReadWrite.WriteBytes(_BreakpointListTemp[(int)lastbreakpointhit].Pointer, new byte[] { 0xCC }, true);
                                        PInvokes.FlushInstructionCache((IntPtr)AttachState.GlobalProcessHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);
                                        dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                        break;

                                    case PInvokes.EXCEPTION_BREAKPOINT:
                                        //Debug.WriteLine("checking breakpointlist");
                                        int BreakpointHit = BreakpointListContains(ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, _BreakpointListTemp);
                                        if (BreakpointHit >= 0)
                                        {
                                            dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                                            //Debug.WriteLine("breakpoint hit! @ " + BreakpointHit.ToString());
                                            lastbreakpointhit = BreakpointHit;
                                            PInvokes.CONTEXT64 context64 = new()
                                            {
                                                ContextFlags = PInvokes.CONTEXT_FLAGS.CONTEXT_ALL
                                            };

                                            //IntPtr hThread = Debugger.Instance._DebugThread.;
                                            //IntPtr hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT | PInvokes.SET_CONTEXT, false, Debugger.Instance._DebugThread.ManagedThreadId);
                                            hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT | PInvokes.SET_CONTEXT, false, DebugEvent.dwThreadId);

                                            if (PInvokes.GetThreadContext((IntPtr)hThread, ref context64))
                                            {
                                                //do custom function things
                                                context64 = _BreakpointListTemp[BreakpointHit].onBreakpoint(context64);

                                                ReadWrite.WriteBytes(_BreakpointListTemp[BreakpointHit].Pointer, _BreakpointListTemp[BreakpointHit].originalCode, true); //TODO: how to handle this failing?
                                                context64.Rip--; //go back an instruction to execute original code
                                                context64.EFlags |= 0x100; //Set trap flag, to raise single-step exception
                                                PInvokes.SetThreadContext((IntPtr)hThread, ref context64); //TODO: how to handle this failing?

                                            }
                                            PInvokes.FlushInstructionCache((IntPtr)AttachState.GlobalProcessHandle, ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, (UIntPtr)30);

                                            //actually I don't think we need our own threads here. 
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
                                        //Console.WriteLine("EXCEPTION_ACCESS_VIOLATION");
                                        break;

                                }


                            }
                            else if (DebugEvent.dwDebugEventCode == PInvokes.LOAD_DLL_DEBUG_EVENT)
                            {
                                //need to close handle or something?
                                PInvokes.LOAD_DLL_DEBUG_INFO LoadDLLDebugInfo = (PInvokes.LOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.LOAD_DLL_DEBUG_INFO));
                                PInvokes.CloseHandle(LoadDLLDebugInfo.hFile);
                            }
                            else if (DebugEvent.dwDebugEventCode == PInvokes.CREATE_THREAD_DEBUG_EVENT)
                            { Debug.WriteLine("CREATE_THREAD_DEBUG_EVENT"); }
                            else if (DebugEvent.dwDebugEventCode == PInvokes.EXIT_THREAD_DEBUG_EVENT)
                            { Debug.WriteLine("EXIT_THREAD_DEBUG_EVENT"); }

                            //Debug.WriteLine("Exception: " + DebugEvent.dwDebugEventCode.ToString());


                            PInvokes.DEBUG_EVENT DebugEvent2 = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                            IntPtr debugInfoPtr2 = GetIntPtrFromByteArray(DebugEvent.u);
                            PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo2 = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                            string exceptionDebugStr2 = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                                (ulong)ExceptionDebugInfo2.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo2.ExceptionRecord.ExceptionCode);
                            //Debug.WriteLine("debuginfo: " + exceptionDebugStr2);


                            // Resume executing the thread that reported the debugging event. 

                            bool bb1 = PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
                                        (uint)DebugEvent.dwThreadId,
                                        dwContinueDebugEvent);

                        }

                    }


                }
                
                //ExitThread was true, this is the last thing we're doing before thread ends
                try { PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID); }
                catch { }
                
            }
            catch (ThreadAbortException e)
            {
                _StopDebugging = true;
                _KeepDebugging = false;
                ExitThread = true;
                Thread.ResetAbort();




            }
        }

        

        private void EvaluateBreakpointList()
        {
            if (_BreakpointList.Count > 0)
            {
                if (_StartDebugging == false)
                {
                    Console.WriteLine("Setting _StartDebugging to true");
                    _StartDebugging = true;
                }
                    

                _KeepDebugging = true;
                
            }
            else
            {
                _KeepDebugging = false;
                _StopDebugging = true;
            }

 

        }

        public void SetBreakpoint(ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint)
        {
            Console.WriteLine("Attempting to set Breakpoint");

            if (AttachState.ProcessID == null)
                throw new RPMException("Tried to SetBreakpoint but ProcessID was null.");

            if (!AttachState.VerifyAttached())
                throw new RPMException("Tried to SetBreakpoint but wasn't attached to the process.");


            bool alreadyset = false;
            foreach (Breakpoint bp in _BreakpointList)
            { 
            if (bp.Pointer == ptr)
                    alreadyset = true;
            }

            if (alreadyset)
            {
                Console.WriteLine("Tried to set breakpoint but breakpoint was already set");
                return;
            }
                

            byte[]? originalCode = ReadWrite.ReadBytes(ptr);
            Debug.WriteLine("originalCode for bp: " + ptr.ToString() + ", oc: " + originalCode?[0].ToString());

            if (originalCode == null)
                throw new RPMException("Tried to SetBreakpoint but could't read original bytes of instruction.");

            //create a new breakpoint and put it in the breakpoint list;
            //using lambda expression to hold the function to run when breakpoint is hit
            _BreakpointList.Add(new Breakpoint(ptr, onBreakpoint, originalCode));
            EvaluateBreakpointList();
            Thread.Sleep(100);

            //this first so if it throws an exception the breakpoint isn't added.
            if (!ReadWrite.WriteBytes(ptr, new byte[] { 0xCC }, true))
                throw new RPMException("Tried to SetBreakpoint but could't write 0xCC to original instruction.");




            
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

        public void RemoveBreakpoint(ReadWrite.Pointer ptr)
        {
            Console.WriteLine("removing breakpoint");
            if (ptr == null)
                throw new RPMException("Tried to RemoveBreakpoint but ptr input was null.");

            foreach (Breakpoint bp in _BreakpointList.ToList())
            {
                if (ReadWrite.ResolvePointer(bp.Pointer) == ReadWrite.ResolvePointer(ptr) || bp.Pointer == ptr)
                {
                    
                    try
                    {

                        Console.WriteLine("restoring code t removebreakpoint");
                        ReadWrite.WriteBytes(bp.Pointer, bp.originalCode, true);
                        Thread.Sleep(100);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Couldn't restore original bytes of breakpoint: " + bp.Pointer?.ToString() + ", " + e.ToString());
                    }
                    _BreakpointList.Remove(bp);
                    EvaluateBreakpointList();
                }
            }
            
        }





    }
}