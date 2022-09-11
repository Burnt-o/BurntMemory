using System.Runtime.InteropServices;
using System.Diagnostics;


namespace BurntMemory
{


    //A tiny state machine for attaching to an external process, and verifying attachment to that process
    public sealed class AttachState
    {

        //Singleton pattern
        private static readonly AttachState instance = new();
        private AttachState() { 
        //constructor
        }

        public static AttachState Instance
        {
            get { return instance; }
        }


        //for reading/writing from process memory
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;

        //a test property that I may remove later
        public bool Attached
        { 
        get { return attached; }    
        }

        //name of process to attach to
        private string? _processToAttach;
        public string? ProcessToAttach
        {
            get { return _processToAttach; }
            set { _processToAttach = value; }
        }

        private Int32? ProcessID = null;

        //ReadWrite will use this.. a lot
        public IntPtr? GlobalProcessHandle;

        private Process? Process;

        //list of process modules and their base addresses. the main module is stored under key "main".
        public Dictionary<string, IntPtr> modules = new();


        private bool attached = false;
        private bool Attach() //TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {

            //TODO: clean this up a bunch to handle errors and whatnot

            IntPtr processHandle;

            try
            { Process = Process.GetProcessesByName(ProcessToAttach)[0]; }//could add a check here that the array isn't null, and if so to uh.. throw our own exception to continue control flow ree
            catch
            { return false; }


            ProcessID = Process.Id;
            processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, Process.Id);

            GlobalProcessHandle = processHandle;
            attached = true;

            Console.WriteLine("Process: " + Process.ToString());
            Console.WriteLine("Main module: " + Process.MainModule.ToString());
            Console.WriteLine("MM addy " + Process.MainModule.BaseAddress.ToString());
            this.modules["main"] = Process.MainModule.BaseAddress;
            return true;
            }





        private bool VerifyAttached() //TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {           
            //check if we ever grabbed a processID
            if (ProcessID == null)
             return false; 

            //check if the process is still running
            if (!Process.GetProcesses().Any(x => x.Id == ProcessID))
             return false;

            //check if we can successfully read a byte from the process
            return ReadWrite.ReadBytes(this.modules["main"]) != null; ; 
         }

        

        public bool AttachAndVerify() //TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            //Verifies attachment, if false it attempts to attach, then verifies again

            if (VerifyAttached())
                return true;

            Attach();

            return VerifyAttached();

        }


        //TODO: probably going to make this private later. This is not it's final form in any case. Also I haven't tested it.
        public bool EvaluateModuleAddress(string modulename) //TODO: like ReadWrites reading functions, we should probably change this from a bool return to an error code
        {
            ProcessModuleCollection allmodules = Process.Modules;

            for (int i = 0; i < allmodules.Count; i++)
            {
                if (allmodules[i].ModuleName.Contains(modulename))
                {
                    modules[modulename] = allmodules[i].BaseAddress;
                    return true;
                }
            }

            return false;

        }


      



    }


}