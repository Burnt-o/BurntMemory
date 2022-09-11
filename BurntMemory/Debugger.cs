using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;


namespace BurntMemory
{
    public sealed class Debugger
    {
        //Singleton pattern
        private static readonly Debugger instance = new();
        private Debugger()
        {
            //constructor
        }

        public static Debugger Instance
        {
            get { return instance; }
        }


        //TODO: do I need this?
        public static AttachState AttachState
        {
            get { return BurntMemory.AttachState.Instance; }

        }

        private static bool _IsProcessDebugged;
        private static bool _IsDebugThreadActive;

        /*TODO: need to rethink my approach here.
        I want a singleton of the Debugger instance. This is to always exist whether we're actually debugging or not; we'll instantiate it when AttachState is instantiated.
        We'll have some sort of breakpoint struct (containning address, original instruction, function to run).
        A DebugSetbreakpoint function handles adding values to that struct; if the struct was previously empty, then we know to initialise the debugger thread!
        Likewise a DebugRemovebreakpoint removes values from the stuct; if it is now empty, we can terminate the debugger thread.

        Thus we'll have an InitiateDebuggerThread and TerminateDebuggerThread function. 
        And of course, the actual Debugger thread function (We'll put that in it's own class perhaps. but we will make sure we only EVER have one of these).

        Remember, a thread just runs from start to finish. We just happen to have a while(KeepDebugging) loop in there. 
        TerminateDebuggerThread will just set KeepDebugging to false; the thread will then run to completion (including the DebugActiveProcessStop).

        The Debuggerthread can handle any SETUP errors at the start, ah but how will it communicate that back to the InitiateDebuggerThread function?


        MAYBE INSTEAD we just setup the debug thread right away. If while(keepdebugging) is false, we can just while(sleep(1000)) til it isn't. 
        at the same time as passing it keepdebugging, we'll chuck it the process ID to use. or well let it access it from attachstate. and access the breakpoint list from here. 
        yeah that may be a MUCH simplier approach. Thread is always going. 

        */
        public static void DebugProcess(bool debugprocessflag)
        {
            if (_IsProcessDebugged)
            {
                if (!debugprocessflag) //want to turn off debug
                {
                    if (AttachState.AttachAndVerify() && AttachState.Instance.ProcessID != null)
                    PInvokes.DebugActiveProcessStop((uint)AttachState.Instance.ProcessID);

                    _IsProcessDebugged = false;
                }

                if (AttachState.AttachAndVerify())
                return;
                else 


            }
            else
            { 
            
            }

        }


        public static int SetBreakpoint(IntPtr? addy)
        {
            if (addy == null)
                return 1;

            if (AttachState.ProcessID == null)
                return 2;


            return 0;
        }
    }
}