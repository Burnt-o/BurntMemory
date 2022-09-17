using System.Diagnostics;
using System.Timers;
using Console = System.Diagnostics.Debug;

namespace BurntMemory
{
    // A tiny state machine for attaching to an external process, and verifying attachment to that process
    public sealed class AttachState
    {
        private static readonly Debugger debugger = BurntMemory.Debugger.Instance;

        // ReadWrite will use this.. a lot
        public IntPtr? GlobalProcessHandle;

        // list of process modules and their base addresses. the main module is stored under key "main".
        public Dictionary<string, IntPtr?> modules = new();

        public uint? ProcessID = null;

        private static readonly System.Timers.Timer _TryToAttachTimer = new System.Timers.Timer();

        // Singleton pattern
        private static readonly AttachState instance = new();
        // name of processes to attach to
        private string[]? _processesToAttach;

        // a test property that I may remove later
        private bool attached = false;

        private Process? Process;

        private AttachState()
        {
            // Constructor

            _TryToAttachTimer.Elapsed += new ElapsedEventHandler(TryToAttachLoop);
            _TryToAttachTimer.Interval = 1000;
            _TryToAttachTimer.Enabled = false;
        }

        public static AttachState Instance
        {
            get { return instance; }
        }

        public bool Attached
        {
            get { return attached; }
        }

        public string[]? ProcessesToAttach
        {
            get { return _processesToAttach; }
            set { _processesToAttach = value; }
        }

        public Process? ProcessPublic
        {
            get { return Process; }
            set { Process = value; }
        }

        public System.Timers.Timer TryToAttachTimer
        {
            get { return _TryToAttachTimer; }
        }
        public bool AttachAndVerify() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            // Verifies attachment, if false it attempts to attach, then verifies again

            if (VerifyAttached())
                return true;

            Attach();

            return VerifyAttached();
        }

        // TODO: probably going to make this private later. This is not it's final form in any case. Also I haven't tested it.
        public bool EvaluateModuleAddress(string modulename) // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            ProcessModuleCollection? allmodules = Process?.Modules;

            if (allmodules == null)
                return false;

            for (int i = 0; i < allmodules.Count; i++)
            {
                if (allmodules[i].ModuleName != null && allmodules[i].ModuleName.Contains(modulename))
                {
                    modules[modulename] = allmodules[i].BaseAddress;
                    return true;
                }
            }

            return false;
        }

        public bool VerifyAttached() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            // check if we ever grabbed a processID
            if (ProcessID == null)
                return false;

            // check if the process is still running
            if (!Process.GetProcesses().Any(x => x.Id == ProcessID))
                return false;

            // check if we can successfully read a byte from the process
            IntPtr? baseAddress = modules["main"];
            if (modules["main"] != null && baseAddress != null)
            {
                return ReadWrite.ReadBytes(new ReadWrite.Pointer(baseAddress)) != null; ;
            }
            return false;
        }

        private bool Attach() // TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            // TODO: clean this up a bunch to handle errors and whatnot

            // Should we add a closehandle here?

            IntPtr processHandle;

            try
            { Process = Process.GetProcessesByName(ProcessesToAttach[0])[0]; }// could add a check here that the array isn't null, and if so to uh.. throw our own exception to continue control flow ree
            catch
            { return false; }

            ProcessID = (uint)Process.Id;
            processHandle = PInvokes.OpenProcess(PInvokes.PROCESS_ALL_ACCESS, false, Process.Id);

            GlobalProcessHandle = processHandle;
            attached = true;

            Console.WriteLine("Process: " + Process.ToString());
            Console.WriteLine("Main module: " + Process.MainModule?.ToString());
            Console.WriteLine("MM addy " + Process.MainModule?.BaseAddress.ToString());
            this.modules["main"] = Process.MainModule?.BaseAddress;
            Thread.Sleep(100);
            return true;
        }

        private void TryToAttachLoop(object? source, ElapsedEventArgs e)
        {
        }
    }
}