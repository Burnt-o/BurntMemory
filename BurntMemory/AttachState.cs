using System.Diagnostics;
using System.Timers;
using Console = System.Diagnostics.Debug;

namespace BurntMemory
{
    // A tiny state machine for attaching to an external process, and verifying attachment to that process
    public sealed class AttachState
    {
        private AttachState()
        {
            // Constructor
            _TryToAttachTimer.Elapsed += new ElapsedEventHandler(this.TryToAttachLoop);
            _TryToAttachTimer.Interval = 1000;
            _TryToAttachTimer.Enabled = false;
            BurntMemory.Debugger dbg = BurntMemory.Debugger.Instance;
            dbg.DLL_LOAD_EVENT += new System.EventHandler(this.HandleDLLReload);
            dbg.DLL_UNLOAD_EVENT += new System.EventHandler(this.HandleDLLReload);

        }
        private static readonly System.Timers.Timer _TryToAttachTimer = new System.Timers.Timer();

        // Singleton pattern
        private static readonly AttachState instance = new();
        public static AttachState Instance
        {
            get { return instance; }
        }

        //stuff to handle looping attach and control logic
        private bool attached = false;
        public bool Attached
        {
            get { return this.attached; }
        }

        // name of processes to attach to
        private string[]? processesToAttach;
        public string[]? ProcessesToAttach
        {
            get { return this.processesToAttach; }
            set { this.processesToAttach = value; }
        }

        public System.Timers.Timer TryToAttachTimer
        {
            get { return _TryToAttachTimer; }
        }


        //attach process variables
        private Process? process = null;
        public IntPtr? processHandle; // ReadWrite will use this.. a lot
        public uint? ProcessID = null;
        public uint? OldProcessID = null; //used in debugger to make sure it can undebug the old process

        public Dictionary<string, IntPtr?> modules = new(); // List of process modules and their base addresses. the main module is stored under key "main". All modules first evaluated on successful Attach(), non-main modules re-evaluated by debugger (DLL load/unload) or ReEvaluateModules() 

        private void HandleDLLReload(object? sender, System.EventArgs e)
        {
            EvaluateModules();
        }

        public bool EvaluateModules() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            ProcessModuleCollection? allmodules = this.process?.Modules;

            if (allmodules == null)
            {
                return false;
            }

            for (int i = 0; i < allmodules.Count; i++)
            {
                if (allmodules[i].ModuleName != null)
                {
                    this.modules[allmodules[i].ModuleName] = allmodules[i].BaseAddress;
                    return true;
                }
            }

            return false;
        }


        private bool Attach(string process)
        {
            try
            {
                this.process = Process.GetProcessesByName(process)[0];
                this.processHandle = PInvokes.OpenProcess(PInvokes.PROCESS_ALL_ACCESS, false, this.process.Id);
                this.OldProcessID = this.ProcessID;
                this.ProcessID = (uint)this.process.Id;
                this.attached = true;
                this.modules["main"] = this.process.MainModule?.BaseAddress;
                EvaluateModules();
                this.process.EnableRaisingEvents = true;
                this.process.Exited += new EventHandler(AttachedProcess_Exited);
                BurntMemory.Debugger.needToStartDebugging = true;
                return true;
            }
            catch
            {
                Detach();
                return false;
            }
            
        }

        public void Detach()
        {
            this.process = null;
            this.processHandle = null;
            this.ProcessID = null;
            this.attached = false;
            this.modules["main"] = null;
        }


        private void AttachedProcess_Exited(object? sender, System.EventArgs e)
        {
            Debugger.needToStopDebugging = true; //tell debugger to stop debugging 
            Detach();
            TryToAttachTimer.Enabled = true;
        }

        public void ForceAttach()
        {
            TryToAttachLoop(null, EventArgs.Empty);
        }

        private void TryToAttachLoop(object? source, EventArgs e)
        {

            if (this.ProcessesToAttach != null)
            {
                bool success = false;
                foreach (string process in this.ProcessesToAttach)
                {
                    if (Attach(process))
                    {
                        success = true;
                        break;
                    }

                }

                if (success)
                {
                    TryToAttachTimer.Enabled = false;
                }

            }
        }
    }
}