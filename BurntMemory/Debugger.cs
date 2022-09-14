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
            _DebugThread = new Thread(new ThreadStart(DebugThread));
            _DebugThread.Start();

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
            public ReadWrite.Pointer? Pointer;
            public int function;
            public byte originalCode;

            public Breakpoint(ReadWrite.Pointer ptr, int function, byte originalCode)
            {
                this.Pointer = ptr;
                this.function = function;
                this.originalCode = originalCode;
            }
        }

        private static List<Breakpoint> _BreakpointList = new();


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
        private Thread _DebugThread;


        static int BreakpointListContains(IntPtr addy)
        {
            if (_BreakpointList == null)
                return -1;

            if (_BreakpointList.Count() == 0)
                return -2;

            for (int i = 0; i < _BreakpointList.Count(); i++)
            {

                if (ReadWrite.ResolvePointer(_BreakpointList[i].Pointer) == addy)
                    return i;
            }

            return -3;
        }

        static void DebugThread()
        {
            int? lastbreakpointhit = null;
            IntPtr lpBaseOfDllLoad = IntPtr.Zero;
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
                        try { PInvokes.DebugActiveProcess((uint)AttachState.ProcessID);
                            PInvokes.DebugSetProcessKillOnExit(false);
                        }
                        catch { _KeepDebugging = false; _StopDebugging = true; Console.WriteLine("Somethings gone horrible wrong on DebugActiveProcess" + AttachState.ProcessID.ToString() + " " + PInvokes.GetLastError().ToString()); } //TODO: figure out error handling here.
                        
                    }

                    //main debug thread loop logic:

                    //Console.WriteLine("yep in main debug thread loop");

                    IntPtr debugEventPtr = Marshal.AllocHGlobal(188);
                    bool bb = PInvokes.WaitForDebugEvent(debugEventPtr, 1000);
                    UInt32 dwContinueDebugEvent = PInvokes.DBG_CONTINUE;
                    if (bb)
                    {
                        PInvokes.DEBUG_EVENT DebugEvent = (PInvokes.DEBUG_EVENT)Marshal.PtrToStructure(debugEventPtr, typeof(PInvokes.DEBUG_EVENT));
                        IntPtr debugInfoPtr = GetIntPtrFromByteArray(DebugEvent.u);

                       

                        PInvokes.EXCEPTION_DEBUG_INFO ExceptionDebugInfo = (PInvokes.EXCEPTION_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXCEPTION_DEBUG_INFO));
                        string exceptionDebugStr = String.Format("EXCEPTION_DEBUG_EVENT: Exception Address: 0x{0:x}, Exception code: 0x{1:x}",
                            (ulong)ExceptionDebugInfo.ExceptionRecord.ExceptionAddress, ExceptionDebugInfo.ExceptionRecord.ExceptionCode);
                        Console.WriteLine(exceptionDebugStr);

                        switch (DebugEvent.dwDebugEventCode)
                        {
                            case PInvokes.CREATE_PROCESS_DEBUG_EVENT:
                                Console.WriteLine("CREATE_PROCESS_DEBUG_EVENT");
                                PInvokes.CREATE_PROCESS_DEBUG_INFO CreateProcessDebugInfo = (PInvokes.CREATE_PROCESS_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.CREATE_PROCESS_DEBUG_INFO));
                                //hProcess = CreateProcessDebugInfo.hProcess;
                                break;
                            case PInvokes.CREATE_THREAD_DEBUG_EVENT:
                                Console.WriteLine("CREATE_THREAD_DEBUG_EVENT");
                                /*              PInvokes.CREATE_THREAD_DEBUG_INFO CreateThreadDebugInfo;
                                            CreateThreadDebugInfo.hThread = (IntPtr)BitConverter.ToUInt64(DebugEvent.u, 0);
                                            CreateThreadDebugInfo.lpThreadLocalBase = (IntPtr)BitConverter.ToUInt64(DebugEvent.u, 8);
                                            CreateThreadDebugInfo.lpStartAddress = (PInvokes.PTHREAD_START_ROUTINE)Marshal.GetDelegateForFunctionPointer((IntPtr)BitConverter.ToUInt64(DebugEvent.u, 8), typeof(PInvokes.PTHREAD_START_ROUTINE));
                //                               CreateThreadDebugInfo.lpStartAddress = (PInvokes.PTHREAD_START_ROUTINE)BitConverter.ToUInt64(DebugEvent.u, 16);*/
                                break;

                            case PInvokes.EXIT_PROCESS_DEBUG_EVENT:
                                Console.WriteLine("EXIT_PROCESS_DEBUG_EVENT");
                                PInvokes.EXIT_PROCESS_DEBUG_INFO ExitProcessDebugInfo = (PInvokes.EXIT_PROCESS_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXIT_PROCESS_DEBUG_INFO));
                                //bContinueDebugging = false;
                                break;
                            case PInvokes.EXIT_THREAD_DEBUG_EVENT:
                                Console.WriteLine("EXIT_THREAD_DEBUG_EVENT");
                                PInvokes.EXIT_THREAD_DEBUG_INFO ExitThreadDebugInfo = (PInvokes.EXIT_THREAD_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.EXIT_THREAD_DEBUG_INFO));
                                break;
                            case PInvokes.LOAD_DLL_DEBUG_EVENT:
                                PInvokes.LOAD_DLL_DEBUG_INFO LoadDLLDebugInfo = (PInvokes.LOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.LOAD_DLL_DEBUG_INFO));
                                lpBaseOfDllLoad = LoadDLLDebugInfo.lpBaseOfDll;
                                //                            byte[] moduleName = new byte[1024];
                                //                            UInt32 dwRet = PInvokes.GetModuleFileNameEx(hProcess, LoadDLLDebugInfo.lpBaseOfDll, out moduleName, 1024);
                                //                            if (dwRet == 0)
                                //                                dwRet = PInvokes.GetLastError();
                                //                            Thread.Sleep(5000);
                                //                            Console.WriteLine("LOAD_DLL_DEBUG_EVENT: Dll name: " + FindModule((int)nPid, LoadDLLDebugInfo));
                                break;
                            case PInvokes.OUTPUT_DEBUG_STRING_EVENT:
                                Console.WriteLine("OUTPUT_DEBUG_STRING_EVENT");
                                PInvokes.OUTPUT_DEBUG_STRING_INFO OutputDebugStringInfo = (PInvokes.OUTPUT_DEBUG_STRING_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.OUTPUT_DEBUG_STRING_INFO));
                                UInt64 lpNumberOfBytesRead = 0;
                                byte[] lpBuffer = new byte[OutputDebugStringInfo.nDebugStringLength];
                                if (PInvokes.ReadProcessMemory((IntPtr)AttachState.GlobalProcessHandle, OutputDebugStringInfo.lpDebugStringData, lpBuffer, OutputDebugStringInfo.nDebugStringLength, ref lpNumberOfBytesRead))
                                {
                                    string debugOutputString = "";
                                    if (OutputDebugStringInfo.fUnicode == 0)
                                        debugOutputString = Encoding.ASCII.GetString(lpBuffer);
                                    else
                                        debugOutputString = Encoding.Unicode.GetString(lpBuffer);
                                    Console.WriteLine("OutputDebugString: " + debugOutputString);
                                }
                                break;
                            case PInvokes.RIP_EVENT:
                                Console.WriteLine("RIP_EVENT");
                                PInvokes.RIP_INFO RipInfo = (PInvokes.RIP_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.RIP_INFO));
                                break;
                            case PInvokes.UNLOAD_DLL_DEBUG_EVENT:
                                PInvokes.UNLOAD_DLL_DEBUG_INFO UnloadDebugInfo = (PInvokes.UNLOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.UNLOAD_DLL_DEBUG_INFO));
                                Console.WriteLine("UNLOAD_DLL_DEBUG_EVENT: Dll name: " + "actually I don't care");
                                break;
                        }

                        if (DebugEvent.dwDebugEventCode == PInvokes.EXCEPTION_DEBUG_EVENT)
                        {
                            
                            Console.WriteLine("Processing EXCEPTION_DEBUG_EVENT");
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
                                    if (lastbreakpointhit != null)
                                        //TODO: this will totally break if breakpoint list modified during singlestep
                                    ReadWrite.WriteBytes(_BreakpointList[(int)lastbreakpointhit].Pointer, new byte[] { 0xCC }, true);
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

                            //testing getting thread context (registers etc)

                            if (ExceptionDebugInfo.ExceptionRecord.ExceptionCode == PInvokes.EXCEPTION_BREAKPOINT)
                            {
                                //hey that actually worked.
                                //now to SetThreadContext ie change registers 
                                Console.WriteLine("Exception add: " + ExceptionDebugInfo.ExceptionRecord.ExceptionAddress.ToString());
                                int BreakpointHit = BreakpointListContains(ExceptionDebugInfo.ExceptionRecord.ExceptionAddress);
                                Console.WriteLine(BreakpointHit.ToString());
                                if (BreakpointHit >= 0)
                                {
                                    lastbreakpointhit = BreakpointHit;
                                    Console.WriteLine("Breakpoint HIT!: " + BreakpointHit);
                                    PInvokes.CONTEXT64 context64 = new()
                                    {
                                        ContextFlags = PInvokes.CONTEXT_FLAGS.CONTEXT_ALL
                                    };

                                    //IntPtr hThread2 = DebugEvent.dwThreadId;

                                    IntPtr hThread = PInvokes.OpenThread(PInvokes.GET_CONTEXT, false, DebugEvent.dwThreadId);

                                    //IntPtr hThread = _BreakpointList[0].Address;
                                    if (PInvokes.GetThreadContext(hThread, ref context64))
                                    {
                                        Console.WriteLine("Rbp    : {0}", context64.Rbp);
                                        Console.WriteLine("Rcx    : {0}", context64.Rcx);
                                        Console.WriteLine("Rip    : {0}", context64.Rip);
                                        Console.WriteLine("SegCs  : {0}", context64.SegCs);
                                        Console.WriteLine("EFlags : {0}", context64.EFlags);
                                        Console.WriteLine("Rsp    : {0}", context64.Rsp);
                                        Console.WriteLine("SegSs  : {0}", context64.SegSs);
                                    }

                                    IntPtr hThread2 = PInvokes.OpenThread(PInvokes.SET_CONTEXT, false, DebugEvent.dwThreadId);
                                    Console.WriteLine("Setting rcx to 0");
                                    context64.Rcx = 0;
                                    Console.WriteLine("Setting rip to rip - 1");
                                    context64.Rip = context64.Rip - 1;
                                    context64.EFlags |= 0x100; //Set trap flag, to raise single-step exception

                                    //is this hitting like a page protection exception every time? 
                                    //why so many exception codes? 
                                    ReadWrite.WriteBytes(_BreakpointList[BreakpointHit].Pointer, new byte[] { _BreakpointList[BreakpointHit].originalCode }, true);
                                    PInvokes.SetThreadContext(hThread2, ref context64);

                                    //Debugger.Instance.RemoveBreakpoint(_BreakpointList[BreakpointHit].Address); //DON'T DO THIS, THREAD SAFETY ISSUE
                                    PInvokes.ResumeThread(hThread);
                                    PInvokes.CloseHandle(hThread);
                                    PInvokes.ResumeThread(hThread2);
                                    PInvokes.CloseHandle(hThread2);




                                }

                            }
                           

                        }
                        else if (DebugEvent.dwDebugEventCode == PInvokes.LOAD_DLL_DEBUG_EVENT) 
                            {
                            PInvokes.LOAD_DLL_DEBUG_INFO LoadDLLDebugInfo = (PInvokes.LOAD_DLL_DEBUG_INFO)Marshal.PtrToStructure(debugInfoPtr, typeof(PInvokes.LOAD_DLL_DEBUG_INFO));
                            PInvokes.CloseHandle(LoadDLLDebugInfo.hFile);
                            //lpBaseOfDllLoad = LoadDLLDebugInfo.lpBaseOfDll;
                            //lpBaseOfDllLoad = IntPtr.Zero;
                        }
                        // Resume executing the thread that reported the debugging event. 


                        //maybe we also need to increment the RIP

                        bool bb1 = PInvokes.ContinueDebugEvent((uint)DebugEvent.dwProcessId,
                                    (uint)DebugEvent.dwThreadId,
                                    dwContinueDebugEvent);
                        Console.WriteLine("returnning execution at thread ID: " + DebugEvent.dwThreadId);

                        



                    }
                    if (debugEventPtr != null)
                        Marshal.FreeHGlobal(debugEventPtr);
                }
            
            
            }
            //if (AttachState.ProcessID != null)
            //PInvokes.DebugActiveProcessStop((uint)AttachState.ProcessID);
        }

        

        private void EvaluateBreakpointList()
        {
            if (_BreakpointList.Count() > 0)
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

        public void SetBreakpoint(ReadWrite.Pointer ptr)
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
                throw new RPMException("Tried to SetBreakpoint but that breakpoint was already set!");

            byte[] originalCode = ReadWrite.ReadBytes(ptr);
            Console.WriteLine("originalCode: " + originalCode[0]);
            _BreakpointList.Add(new Breakpoint(ptr, 0, originalCode[0]));
            //_BreakpointList.Append(new Breakpoint(addy, 0, originalCode[0]));
            
            Console.WriteLine("Appended");
            Console.WriteLine("_BreakpointList count: " + _BreakpointList.Count());
            ReadWrite.WriteBytes(ptr, new byte[] { 0xCC }, true);
            
            
            EvaluateBreakpointList();
        }

        public void RemoveBreakpoint(ReadWrite.Pointer ptr)
        {
            if (ptr == null)
                throw new RPMException("Tried to RemoveBreakpoint but ptr input was null.");

            foreach (Breakpoint bp in _BreakpointList)
            {
                if (bp.Pointer == ptr)
                    _BreakpointList.Remove(bp);
            }
            //_BreakpointList = Array.FindAll(_BreakpointList, bp => bp.Address == addy).ToArray();
            EvaluateBreakpointList();

        }





    }
}