using System.Diagnostics;
using System.Timers;

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
                    if (this._attached) // Only invoke Detach Event if we were previously attached
                    {
                        Events.DEATTACH_EVENT_INVOKE(this, EventArgs.Empty);
                    }
                    
                }
                else if (value && !this._attached) //only pop Attach Event if we weren't already attached
                {
                    Events.AttachedEventArgs attachedEventArgs = new();
                    attachedEventArgs.NameOfProcess = nameOfAttachedProcess;
                    attachedEventArgs.ProcessVersion = _processVersion;
                    Events.ATTACH_EVENT_INVOKE(this, attachedEventArgs);
                }
                this._attached = value;
            }
        }

        private string? _processVersion;
        public string? ProcessVersion { get { return this._processVersion; } set { this._processVersion = value; } }

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
        public string? nameOfAttachedProcess = null;

        //public Dictionary<string, IntPtr?> modules = new(); // List of process modules and their base addresses. the main module is stored under key "main". All modules first evaluated on successful Attach(), non-main modules re-evaluated by debugger (DLL load/unload) or ReEvaluateModules()

        private Dictionary<string, ReadWrite.Pointer?> _modules = new();
        public Dictionary<string, ReadWrite.Pointer?> modules 
        {
            get
            {

                    return _modules;
            }
        }


        public bool SetModulePointer(string? moduleName, ReadWrite.Pointer? pointerToModule)
        {

            lock (modules)
            {
                if (pointerToModule == null || moduleName == null)
                {
                    Trace.WriteLine("OH GODDDDDDDDDD 1");
                    return false;
                }

                if (moduleName == "main")
                {
                    Trace.WriteLine("SetModulePoitner processing main");
                    if (modules.ContainsKey("main"))
                    {
                        modules.Remove("main");
                    }

                    modules.Add("main", new ReadWrite.Pointer(MainModuleBaseAddress));
                }
                else
                {
                    Trace.WriteLine("SetModulePoitner processing non-main. moduleName:" + moduleName);
                    Trace.WriteLine("pointerToModule is null? " + pointerToModule == null ? "yes" : "no");
                    if (modules.ContainsKey(moduleName))
                    {
                        modules.Remove(moduleName);
                    }
                    modules.Add(moduleName, pointerToModule);
                }

                return true;
            }
        }

        public bool EvaluateModules() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            //need to refresh our Process object
            Process? process;
            try
            {
                process = Process.GetProcessesByName(nameOfAttachedProcess)[0];
            }
            catch (Exception ex)
            {
                Trace.WriteLine("1Detaching, reason: " + ex.Message);
                Detach();
                return false;
            }
            ProcessModuleCollection? allmodules = process?.Modules;

            if (allmodules == null)
            {
                return false;
            }

            lock (modules)
            {
               /* foreach (ProcessModule module in allmodules)
                {
                    if (module.ModuleName != null)
                    {
                        this.modules[module.ModuleName] = null;
                        //Trace.WriteLine(module.ModuleName);
                    }
                }*/
            }

            return false;
        }

        public IntPtr? MainModuleBaseAddress { get; set; }

        private readonly object AttachLock = new object();
        private bool Attach(string processname)
        {
            lock (AttachLock)
            {
                try
                {
                    Process? ourProcess = null;
                    Process[] ProcessList = Process.GetProcesses();
                    foreach (Process proc in ProcessList)
                    {
                        //Needs to ignore case woo
                        if (proc.ProcessName.Equals(processname, StringComparison.OrdinalIgnoreCase))
                        {
                            ourProcess = proc;
                            break;
                        }
                    }

                    if (ourProcess == null)
                    {
                        Trace.WriteLine("Failed to find process of name: " + processname);
                        Detach();
                        return false;
                    }
                    this.processHandle = PInvokes.OpenProcess(PInvokes.PROCESS_ALL_ACCESS, false, ourProcess.Id);
                    this.nameOfAttachedProcess = processname;
                    this._processID = (uint)ourProcess.Id;
                    this.ProcessVersion = ourProcess.MainModule?.FileVersionInfo.ProductVersion?.ToString() ?? null;

     
                        Trace.WriteLine("ourProcess.MainModule is null? " + ourProcess.MainModule == null ? "yes" : "no");
                        Trace.WriteLine("ourProcess.MainModule?.BaseAddress " + ourProcess.MainModule != null ? ourProcess.MainModule.BaseAddress : "can't");
                        Trace.WriteLine("How bout procHandle? is null? " + this.processHandle == null ? "yes" : "no");
                        Trace.WriteLine("Address of procHandle: " + this.processHandle != null ? this.processHandle.Value.ToString("X") : "Can't");

                    lock (modules)
                    {
                        ReadWrite.Pointer mainmoduleptr = new ReadWrite.Pointer(ourProcess.MainModule?.BaseAddress);
                        if (modules.ContainsKey("main"))
                        {
                            modules.Remove("main");
                        }
                        modules.Add("main", mainmoduleptr);

                        MainModuleBaseAddress = ourProcess.MainModule?.BaseAddress;

                        if (this.modules["main"] == null)
                        {
                            Trace.WriteLine("Trying wack fix");
                            //this.modules["main"] = new ReadWrite.Pointer(this.processHandle);
                        }

                        if (this.modules["main"] == null)
                        {
                            throw new Exception("FUCKKKKK");
                        }
                    }

                    EvaluateModules();
                    ourProcess.EnableRaisingEvents = true;
                    ourProcess.Exited += new EventHandler(AttachedProcess_Exited);
                    Attached = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("2Detaching, reason: " + ex.Message + ex.StackTrace);
                    Detach();
                    return false;
                }
            }
        }

        public void Detach()
        {
            this.nameOfAttachedProcess = null;
            this.processHandle = null;
            this._processID = null;
            Attached = false;
            lock (modules)
            {
                this.modules.Clear();
            }
            
        }

        private void AttachedProcess_Exited(object? sender, System.EventArgs e)
        {
            Events.EXTERNAL_PROCESS_CLOSED_EVENT_INVOKE(sender, e);
            //do we need to unsubscibe this on detach?
            Trace.WriteLine("3Detaching, reason: Attached Process Exited");
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