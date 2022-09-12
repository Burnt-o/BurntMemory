using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace BurntMemory
{
    public sealed class Debugger
    {

        private Debugger()
        {
            //constructor
            Console.WriteLine("Running debugger constructor");
            Thread t = new Thread(new ThreadStart(DebugThread));
            t.Start();

        }

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
            public IntPtr Address;
            public int function;

            public Breakpoint(IntPtr Address, int function)
            {
                this.Address = Address;
                this.function = function;
            }
        }

        private Breakpoint[] _BreakpointList = new Breakpoint[0];


        private static bool _IsProcessDebugged;
        private static bool _IsDebugThreadActive;


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

        static void DebugThread()
        {
            Console.WriteLine("Hello from DebugThread");
            while (true)
            {
                if (!_KeepDebugging)
                {
                    if (_StopDebugging)
                    { 
                    _StopDebugging = false;
                        try
                        {
                            PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
                        }
                        catch { } //process was probably closed or invalid so no need to stop debugging.

                        }

                    Thread.Sleep(100);
                }
                else
                {
                    if (_StartDebugging)
                    { 
                    //Setup stuff
                    _StartDebugging = false;
                        Console.WriteLine("Starting debugging");
                        try { PInvokes.DebugActiveProcess((uint)AttachState.ProcessID); }
                        catch { _KeepDebugging = false; _StopDebugging = true; Console.WriteLine("Somethings gone horrible wrong on DebugActiveProcess" + AttachState.ProcessID.ToString() + " " + PInvokes.GetLastError().ToString()); } //TODO: figure out error handling here.
                        
                    }

                    //main debug thread loop logic:
                    //none of this is tested yet.
                    //But the logic as it is right now is to catch ALL exception_debug_events.
                    //Later we'll add a check for if the debug event is at a location described in our breakpointlist.

                    Console.WriteLine("yep in main debug thread loop");

                    IntPtr debugEventPtr = Marshal.AllocHGlobal(188);
                    bool bb = PInvokes.WaitForDebugEvent(debugEventPtr, 1000);
                    UInt32 dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                    if (bb)
                    {
                        PInvokes.DEBUG_EVENT DebugEvent = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                        IntPtr debugInfoPtr = GetIntPtrFromByteArray(DebugEvent.u);


                        if (DebugEvent.dwDebugEventCode == PInvokes.EXCEPTION_DEBUG_EVENT)
                        {
                            PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                            string exceptionDebugStr = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                                (ulong)ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo.ExceptionRecord.ExceptionCode);
                            Console.WriteLine(exceptionDebugStr);
                            switch (ExceptionDebugInfo.ExceptionRecord.ExceptionCode)
                            {
                                case PInvokes.EXCEPTION_ACCESS_VIOLATION:
                                    Console.WriteLine("EXCEPTION_ACCESS_VIOLATION");
                                    PInvokes.ContinueDebugEvent(DebugEvent.dwProcessId,
                                           DebugEvent.dwThreadId,
                                           PInvokes.DBG_EXCEPTION_NOT_HANDLED);
                                    break;

                                case PInvokes.EXCEPTION_BREAKPOINT:
                                    Console.WriteLine("EXCEPTION_BREAKPOINT");
                                    break;

                                case PInvokes.EXCEPTION_DATATYPE_MISALIGNMENT:
                                    Console.WriteLine("EXCEPTION_DATATYPE_MISALIGNMENT");
                                    break;

                                case PInvokes.EXCEPTION_SINGLE_STEP:
                                    Console.WriteLine("EXCEPTION_SINGLE_STEP");
                                    break;

                                case PInvokes.DBG_CONTROL_C:
                                    Console.WriteLine("DBG_CONTROL_C");
                                    break;
                                case PInvokes.EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
                                    Console.WriteLine("EXCEPTION_ARRAY_BOUNDS_EXCEEDED");
                                    break;
                                case PInvokes.EXCEPTION_INT_DIVIDE_BY_ZERO:
                                    PInvokes.ContinueDebugEvent(DebugEvent.dwProcessId,
                                           DebugEvent.dwThreadId,
                                           PInvokes.DBG_EXCEPTION_NOT_HANDLED);
                                    Console.WriteLine("EXCEPTION_INT_DIVIDE_BY_ZERO");
                                    break;
                                default:
                                    Console.WriteLine("Handle other exceptions.");
                                    break;
                            }
                        }
                        // Resume executing the thread that reported the debugging event. 
                        bool bb1 = PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
                                    (uint)DebugEvent.dwThreadId,
                                    dwContinueDebugEvent);

                    }
                    if (debugEventPtr != null)
                        Marshal.FreeHGlobal(debugEventPtr);
                }
            
            
            }
            
        }

        

        private void EvaluateBreakpointList()
        {
            if (_BreakpointList.Count() == 0)
            {
                _KeepDebugging = true;
                _StartDebugging = true;
                Console.WriteLine("Setting _StartDebugging to true");
            }
            else
            {
                _KeepDebugging = false;
                _StopDebugging = true;
            }

 

        }

        public void SetBreakpoint(IntPtr addy)
        {
            Console.WriteLine("Attempting to set Breakpoint");
            if (addy == null)
               throw new RPMException("Tried to SetBreakpoint but addy input was null.");

            if (AttachState.ProcessID == null)
                throw new RPMException("Tried to SetBreakpoint but ProcessID was null.");

            if (!AttachState.VerifyAttached())
                throw new RPMException("Tried to SetBreakpoint but wasn't attached to the process.");


            bool alreadyset = false;
            foreach (Breakpoint bp in _BreakpointList)
            { 
            if (bp.Address == addy)
                    alreadyset = true;
            }

            if (alreadyset)
                throw new RPMException("Tried to SetBreakpoint but that breakpoint was already set!");

            _BreakpointList.Append(new Breakpoint(addy, 0));
            Console.WriteLine("Appended");
            EvaluateBreakpointList();
        }

        public void RemoveBreakpoint(IntPtr addy)
        {
            if (addy == null)
                throw new RPMException("Tried to RemoveBreakpoint but addy input was null.");

            _BreakpointList = Array.FindAll(_BreakpointList, bp => bp.Address == addy).ToArray();
            EvaluateBreakpointList();

        }





    }
}