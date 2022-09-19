using System.Diagnostics;
using System.Timers;
using Console = System.Diagnostics.Debug;



/* TODO LIST:
split custom events to their own class
    add a couple more events for AttachState.Attach and AttachState.Detach
run another code cleanup
add more documentation via comments
double check how errors are handled when say, external process closed or not started.


*/
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
            BurntMemory.DebugManager dbg = BurntMemory.DebugManager.Instance;
            Events.DLL_LOAD_EVENT += new System.EventHandler(this.HandleDLLReload);
            Events.DLL_UNLOAD_EVENT += new System.EventHandler(this.HandleDLLReload);

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
            set { 
                this.attached = value;
                if (value)
                {
                    Events.ATTACH_EVENT_INVOKE(this, EventArgs.Empty);
                }
                else 
                { 
                    Events.DEATTACH_EVENT_INVOKE(this, EventArgs.Empty); 
                }
            }

        }

        public bool ReloadModulesOnDLLEvent = false;

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
        public string? nameOfAttachedProcess = null;
        public Process? process = null;
        public IntPtr? processHandle; // ReadWrite will use this.. a lot
        public uint? ProcessID = null;
        public uint? OldProcessID = null; //used in debugger to make sure it can undebug the old process

        public Dictionary<string, IntPtr?> modules = new(); // List of process modules and their base addresses. the main module is stored under key "main". All modules first evaluated on successful Attach(), non-main modules re-evaluated by debugger (DLL load/unload) or ReEvaluateModules() 

        private void HandleDLLReload(object? sender, System.EventArgs e)
        {
            if (ReloadModulesOnDLLEvent)
            {
                Trace.WriteLine("Handling a DLL reload");
                //EvaluateModules();
                //So turns out if you EvaluateModules on DLL_LOAD_DEBUG_EVENT, you'll just generate even more DLL_LOAD_DEBUG_EVENTs in an infinite loop. bad times. 
                //a better solution would be to have the event pass the filehandle of the dll, then we have our own function here that gets the filename from the handle, then updates the key in our modulelist (importantly; not reiterating thru the whole modulecollection.
            }
        }

        public bool EvaluateModules() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {

            //need to refresh our Process object
            try
            {
                this.process = Process.GetProcessesByName(nameOfAttachedProcess)[0];
            }
            catch 
            {
                Detach();
                return false;
            }
            ProcessModuleCollection? allmodules = this.process?.Modules;

            if (allmodules == null)
            {
                return false;
            }


            foreach (ProcessModule module in allmodules)
                {
                if (module.ModuleName != null)
                {
                    this.modules[module.ModuleName] = module.BaseAddress;
                    Trace.WriteLine(module.ModuleName);
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
                this.nameOfAttachedProcess = process;
                this.OldProcessID = this.ProcessID;
                this.ProcessID = (uint)this.process.Id;
                Attached = true;
                this.modules["main"] = this.process.MainModule?.BaseAddress;
                EvaluateModules();
                this.process.EnableRaisingEvents = true;
                this.process.Exited += new EventHandler(AttachedProcess_Exited);

                BurntMemory.DebugManager.needToStartDebugging = true;
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
            this.nameOfAttachedProcess = null;
            this.process = null;
            this.processHandle = null;
            this.ProcessID = null;
            Attached = false;
            this.modules["main"] = null;
        }


        private void AttachedProcess_Exited(object? sender, System.EventArgs e)
        {
            DebugManager.needToStopDebugging = true; //tell debugger to stop debugging 
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