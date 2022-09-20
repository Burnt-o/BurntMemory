using System.Text;
using System.Runtime.InteropServices;
using System.Threading;


namespace BurntMemory
{
    public class DLLInjector
    
        
        {

        private AttachState _attachState;
        private ReadWrite _readWrite;
        public DLLInjector(AttachState attachState, ReadWrite readWrite)
        { 
        _attachState = attachState;
            _readWrite = readWrite;
        }



        public bool InjectDLL(string dllName)
        {
            if (_attachState.Attached == false)
                return false;
            
            // searching for the address of LoadLibraryA and storing it in a pointer
            IntPtr? loadLibraryAddr = PInvokes.GetProcAddress(PInvokes.GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (loadLibraryAddr == null) //this should like NEVER happen, right? Unless our app isn't elevated.
                return false;

            //Search for the named dll in the apps local folder.
            string currentfolder = System.AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = currentfolder + dllName;
            if (!File.Exists(dllPath))
            {
                return false;
            }

            // alocating some memory on the target process - enough to store the name of the dll
            // and storing its address in a pointer
            IntPtr allocMemAddress;
            try
            {
                allocMemAddress = PInvokes.VirtualAllocEx((IntPtr)_attachState.processHandle, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), PInvokes.ALLOC_FLAGS.MEM_COMMIT | PInvokes.ALLOC_FLAGS.MEM_RESERVE, PInvokes.ALLOC_FLAGS.PAGE_READWRITE);
            }
            catch
            {
                return false;
            }
            

            // writing the name of the dll at the pointer
            BurntMemory.ReadWrite.Pointer ptr = new ReadWrite.Pointer(allocMemAddress);
            _readWrite.WriteBytes(ptr, Encoding.Default.GetBytes(dllPath), true);

            // creating a thread that will call LoadLibraryA with allocMemAddress as argument
            IntPtr? threadID = PInvokes.CreateRemoteThread((IntPtr)_attachState.processHandle, IntPtr.Zero, 0, (IntPtr)loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);

            return threadID != null;
        }



    }
}
