﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using Console = System.Diagnostics.Debug;
using System.Windows;


namespace BurntMemory
{
    public class DebugManager
    {

        // Singleton pattern

        public List<Breakpoint> BreakpointList
        {
            get; private set;
        }



        public bool _MonitorReloads = false;



        private BurntMemory.AttachState _attachState;
        public BurntMemory.ReadWrite _readWrite;
        public DebugManager(BurntMemory.AttachState attachState, BurntMemory.ReadWrite readWrite)
            {
            _attachState = attachState;
            _readWrite = readWrite;

            BreakpointList = new List<Breakpoint>();


            Events.ATTACH_EVENT += new EventHandler(Debugger_HandleAttach);
            Events.DEATTACH_EVENT += new EventHandler(Debugger_HandleDetach);
            Events.EXTERNAL_PROCESS_CLOSED_EVENT += new EventHandler(GracefullyCloseDebugger);

            if (_attachState.Attached)
            {
                Debugger_HandleAttach(this, EventArgs.Empty);
            }
                

        }


        private DebugThread? DebugThread
        { get; set; }

        private void Debugger_HandleAttach(object? sender, EventArgs? e)
        {
            Trace.WriteLine("Debugger is handling attach");
            //create a new DebugThread
            if (DebugThread == null)
            {
                DebugThread = new(_attachState, _readWrite, this);
                DebugThread.ResetBreakpoints = true;
                DebugThread.NewBreakpoints = true;
            }
        }

        private void Debugger_HandleDetach(object? sender, EventArgs? e)
        {
            //tell old DebugThread to shut down
            if (DebugThread != null)
            {
                DebugThread.NeedToCloseThread = true; //tell thread to finish it's last loop
            }
            DebugThread = null; //Garbage collecter will come for it eventually

        }

        public void ClearBreakpoints()
        {
            foreach (Breakpoint bp in BreakpointList)
            {
                if (_readWrite.ReadBytes(bp.Pointer, 1)?[0] == 0xCC)
                {
                    _readWrite.WriteBytes( bp.Pointer, bp.originalCode, true);
                }
            }
            BreakpointList.Clear();
        }

        public void RemoveBreakpoint(string BreakpointName)
        {
            Trace.WriteLine("removing breakpoint");
            foreach (Breakpoint bp in BreakpointList.ToList())
            {
                if (bp.BreakpointName == BreakpointName)
                {
                    if (_attachState.Attached)
                    {
                        _readWrite.WriteBytes( bp.Pointer, bp.originalCode, true);
                    }
                    BreakpointList.Remove(bp);
                }
            }
            
        }

        public bool SetBreakpoint(string BreakpointName, ReadWrite.Pointer ptr, Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint)
        {
            if (_attachState.Attached)
            {

                RemoveBreakpoint(BreakpointName); //remove breakpoint if it was set before, we'll redo it here
                byte[]? originalCode = _readWrite.ReadBytes(ptr); //get the original assembly byte at the instruction of the breakpoint - we'l need this for removing the breakpoint later
                Trace.WriteLine("originalCode for bp: " + ptr.ToString() + ", oc: " + originalCode?[0].ToString());

                if (originalCode == null)
                {
                    Trace.WriteLine("Tried to SetBreakpoint but could't read original bytes of instruction.");
                    return false;
                }

                // create a new breakpoint and put it in the breakpoint list;
                BreakpointList.Add(new Breakpoint(BreakpointName, ptr, onBreakpoint, originalCode));
  

                if (DebugThread != null)
                {
                    DebugThread.NewBreakpoints = true;
                }
                
                return true;

            }
            return false;
        }

        public int BreakpointListContains(IntPtr addy, List<Breakpoint> BPList)
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
                if (_readWrite.ResolvePointer(BPList[i].Pointer) == addy)
                {
                    return i;
                }
            }

            return -3;
        }


        //debug loop control vars
        public bool needToStartDebugging = false;
        public bool processIsDebugged = false;
        public bool needToStopDebugging = false;
        public bool newBreakpoints = false;
        public bool resetBreakpoints = false;

        public bool debuggerIsOn = false;
        public bool debuggerNeedsToBeOn = false;





        public void GracefullyCloseDebugger(object? sender, EventArgs? e)
        {
            if (_attachState.Attached)
            {
                this.ClearBreakpoints();
            }
            if (this.DebugThread != null)
            {
                this.DebugThread.NeedToCloseThread = true; // A flag to tell the DebugThread to stop what it's doing after its current loop
                if (!this.DebugThread.Thread.Join(1000)) // Wait for thread to finish executing, or 1s
                {
                    Trace.WriteLine("DebugThread FAILED to shut down :(");
                }
                else
                {
                    Trace.WriteLine("DebugThread successfully shut down"); // This should always happen
                }
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