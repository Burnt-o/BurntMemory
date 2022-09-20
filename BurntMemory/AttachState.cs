using System.Diagnostics;
using System.Timers;
using Console = System.Diagnostics.Debug;



/* TODO LIST:




add better dealing with nullability
cleanup fields and properties
add more documentation via comments
double check how errors are handled when say, external process closed or not started.
make sure any handles are cleaned up when attachstate closes 
implement a destructor instead of our hacky on-application-close stuff

*/
namespace BurntMemory
{
    // A tiny state machine for attaching to an external process, and verifying attachment to that process
    public partial class AttachState
    {
        public AttachState()
        {
            // Constructor

            _TryToAttachTimer.Elapsed += new ElapsedEventHandler(this.TryToAttachLoop);
            _TryToAttachTimer.Interval = 1000;
            _TryToAttachTimer.Enabled = false;


        }


        //stuff to handle looping attach and control logic
        private bool _attached = false;
        public bool Attached
        {
            get { return this._attached; }
            private set 
            { 
                
                if (!value)
                {
                    Events.DEATTACH_EVENT_INVOKE(this, EventArgs.Empty);
                    
                }
                else if (value && !this._attached) //only pop Attach Event if we weren't already attached
                {
                    Events.ATTACH_EVENT_INVOKE(this, EventArgs.Empty);
                }
                this._attached = value;
            }
        }

        // Timer that will continually try to attach to a process until it succeeds
        private static readonly System.Timers.Timer _TryToAttachTimer = new System.Timers.Timer();
        public System.Timers.Timer TryToAttachTimer
        {
            get { return _TryToAttachTimer; }
        }

        // Name of processes to attach to. The first one it successfully attaches to will be the actual attached process (useful to be able to provide more than one in cases like a process having different names for different versions, ie Notepad.exe vs Notepad64.exe)
        private string[]? processesToAttach;
        public string[]? ProcessesToAttach
        {
            get { return this.processesToAttach; }
            set { this.processesToAttach = value; }
        }


        // ReadWrite makes a lot of use of the processhandle for Read/WriteProcessMemory
        private IntPtr? _processHandle;
        public IntPtr? processHandle 
        {
            get { return _processHandle; }
            set { _processHandle = value; }
        }

        // The debugger makes a lot of use of the processID for DebugActiveProcess and DebugActiveProcessStop
        private uint? _processID = null;
        public uint? ProcessID
        {
            get { return _processID; }
            private set { _processID = value; }
        }


        //attach process variables
        private string? nameOfAttachedProcess = null;



        //public Dictionary<string, IntPtr?> modules = new(); // List of process modules and their base addresses. the main module is stored under key "main". All modules first evaluated on successful Attach(), non-main modules re-evaluated by debugger (DLL load/unload) or ReEvaluateModules() 
        private Dictionary<string, IntPtr?> _modules = new();
        public Dictionary<string, IntPtr?> modules 
        {
            get { return _modules; }
            private set { _modules = value; }
        }



        public bool EvaluateModules() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {

            //need to refresh our Process object
            Process? process;
            try
            {
                process = Process.GetProcessesByName(nameOfAttachedProcess)[0];
            }
            catch 
            {
                Detach();
                return false;
            }
            ProcessModuleCollection? allmodules = process?.Modules;

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



        private bool Attach(string processname)
        {
            try
            {
                Process process = Process.GetProcessesByName(processname)[0];
                this.processHandle = PInvokes.OpenProcess(PInvokes.PROCESS_ALL_ACCESS, false, process.Id);
                this.nameOfAttachedProcess = processname;
                this._processID = (uint)process.Id;
                Attached = true;
                this.modules["main"] = process.MainModule?.BaseAddress;
                EvaluateModules();
                process.EnableRaisingEvents = true;
                process.Exited += new EventHandler(AttachedProcess_Exited);
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
            this.processHandle = null;
            this._processID = null;
            Attached = false;
            this.modules["main"] = null;
        }


        private void AttachedProcess_Exited(object? sender, System.EventArgs e)
        {
            Events.EXTERNAL_PROCESS_CLOSED_EVENT_INVOKE(sender, e);
            //do we need to unsubscibe this on detach?
            Detach();
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