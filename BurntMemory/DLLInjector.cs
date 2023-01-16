using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Runtime.InteropServices;

namespace BurntMemory
{
    public static class DLLInjector

    {

        public static IntPtr InjectDLL(string dllName, Process processToInject)
        {
            if (processToInject.HasExited) throw new ArgumentNullException("Couldn't evaluate process to inject, already exited?");

            // Check if the DLL was already loaded
            foreach (ProcessModule module in processToInject.Modules)
            {
                if (module.ModuleName == dllName)
                {
                    // We found it! We'll return it's handle instead of trying to inject again
                    Trace.WriteLine("Tried to inject DLL but it was already loaded in the target process!");
                    return module.BaseAddress;
                }
            }

            // All right, time to inject

            // First get the address of our own Kernel32's loadLibraryA (it will be the same in the target process because Kernel32 is loaded in the same virtual memory in all processes)
            IntPtr loadLibraryAddr = PInvokes.GetProcAddress(PInvokes.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            
            //Search for the named dll in the apps local folder.
            string currentFolder = System.AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = currentFolder + dllName;
            if (!File.Exists(dllPath)) throw new FileNotFoundException(dllPath);

            // Allocate some memory on the target process, enough to store the filepath of the DLL
            
            uint sizeToAlloc = (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char)));
            IntPtr allocMemAddress = PInvokes.VirtualAllocEx(processToInject.Handle, IntPtr.Zero, sizeToAlloc, PInvokes.ALLOC_FLAGS.MEM_COMMIT | PInvokes.ALLOC_FLAGS.MEM_RESERVE, PInvokes.ALLOC_FLAGS.PAGE_READWRITE);
            IntPtr? remoteThreadHandle = null;

            // We'll put the rest of the method in a try-finally since we'll want to free our allocated memory even if we run into an exception
            try
            {
                // Use WPM to write the dllPath string to that allocated path
                // Note we need to modify then restore the page protection as we do this
                byte[] stringBytes = Encoding.Default.GetBytes(dllPath);

                PInvokes.VirtualProtectEx(processToInject.Handle, allocMemAddress, (int)sizeToAlloc, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                PInvokes.WriteProcessMemory(processToInject.Handle, allocMemAddress, stringBytes, stringBytes.Length, out int bytesWritten);
                PInvokes.VirtualProtectEx(processToInject.Handle, allocMemAddress, (int)sizeToAlloc, lpflOldProtect, out _);

                if (bytesWritten != stringBytes.Length) throw new Exception("Couldn't write DLLpath to process alloc'd memory");

                // Woo, now let's create a thread inside the target process that will call LoadLibraryA, with allocMemAddress (our DLL path string) as parameter
                IntPtr? threadID = PInvokes.CreateRemoteThread(processToInject.Handle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
                if (threadID == null) throw new Exception("Couldn't create remote thread in target process to call LoadLibraryA");

                // Wait for the thread to finish executing (or some timeout)
                uint waitFor = PInvokes.WaitForSingleObject((IntPtr)threadID, 3000);

                // Check if the thread completed
                if (waitFor == 0x00000080) // WAIT_ABANDONED
                {
                    throw new Exception("Remote thread failed unexpectedly");
                }
                else if (waitFor == 0x00000102) // WAIT_TIMEOUT
                {
                    throw new Exception("Remote thread timed out");
                }
                
                // Looks like the thread completed, let's get it's exit code
                // A successful LoadLibraryA call will return a 32bit truncated handle to the loaded DLL
                PInvokes.GetExitCodeThread((IntPtr)threadID, out uint exitCode);

                // A return of 0 means a failure
                if (exitCode == 0)
                {
                  
                    int lastError = Marshal.GetLastWin32Error();
                    throw new Exception("LoadLibrary call failed, error code: " + lastError);
                }

                // Okay, if we got here then the LoadLibrary call was a success!
                // Just need to get a handle to the loaded Library to return it
                // While LoadLibraryA's exit code contains a handle to the loaded Library, unfortunately it only returns 32 bits. On 64 bit this means the handle is cut in half, so is probably useless.

                // We can just re-evaluate the process's modules again
                // IMPORTANT; we have to get a new Process object to refresh the module list
                Process refreshedProcess = Process.GetProcessById(processToInject.Id);
                if (refreshedProcess.HasExited) throw new Exception("Looks like we crashed the target process while injecting");

                foreach (ProcessModule module in refreshedProcess.Modules)
                {
                    if (module.ModuleName == dllName)
                    {
                        // We did it!
                        return module.BaseAddress;
                    }
                }

                throw new Exception("Successfully Injected? but couldn't find DLL in target process");

            }
            finally
            {
                // Free our allocated memory
                PInvokes.VirtualFreeEx(processToInject.Handle, allocMemAddress, (int)sizeToAlloc, PInvokes.AllocationType.Release);
                // Close handle to remote thread
                if (remoteThreadHandle != null)
                {
                    PInvokes.CloseHandle((IntPtr)remoteThreadHandle);
                }
            }
           
        }

        
    }
}