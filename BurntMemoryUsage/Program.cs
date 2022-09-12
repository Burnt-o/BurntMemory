using BurntMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Threading;


using System.Security.AccessControl;
namespace BurntMemoryUsage


{
    internal class Program
    {
                 [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern uint GetSecurityInfo(
IntPtr handle,
SE_OBJECT_TYPE ObjectType,
SECURITY_INFORMATION SecurityInfo,
out IntPtr pSidOwner,
out IntPtr pSidGroup,
out IntPtr pDacl,
out IntPtr pSacl,
out IntPtr pSecurityDescriptor);

        enum SE_OBJECT_TYPE
        {
            SE_UNKNOWN_OBJECT_TYPE,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }
        enum SECURITY_INFORMATION
        {
            OWNER_SECURITY_INFORMATION = 1,
            GROUP_SECURITY_INFORMATION = 2,
            DACL_SECURITY_INFORMATION = 4,
            SACL_SECURITY_INFORMATION = 8,
        }



        static void Main(string[] args)
        {
            Console.WriteLine("Hello World! This is a dumb program for testing random shit as I code the rest of BurntMemory");
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "MCC-Win64-Shipping";
            mem.AttachAndVerify();
            mem.EvaluateModuleAddress("halo1");
            IntPtr Address = BurntMemory.ReadWrite.ResolveAddress("halo1", new int[] { 0x4E });
            string test = BurntMemory.ReadWrite.ReadString(Address, 4);
            Console.WriteLine("test: " + test);
            //WriteProcessMemory(mem.GlobalProcessHandle, AddressToWrite, BitConverter.GetBytes(value), 4, out int bytesWritten);

            IntPtr AddressToBreakpoint = BurntMemory.ReadWrite.ResolveAddress("halo1", new int[] { 0xC540B5 });
            BurntMemory.Debugger.Instance.SetBreakpoint(AddressToBreakpoint);
           



            Console.WriteLine(Marshal.GetLastWin32Error());
            Console.ReadKey();


            //BurntMemory.Debugger.Instance.CloseGracefully();

        }



        static void StopwatchShit()
        {
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            Stopwatch stopwatch = new Stopwatch();
            uint? data = 0;
            bool success;
            stopwatch.Start();
            for (int i = 0; i < 100000; i++)
            {
                data = BurntMemory.ReadWrite.ReadInteger(AddressToWrite);
            }
            stopwatch.Stop();

            Console.WriteLine("Test 1: Elapsed Time is {0} ms", stopwatch.ElapsedMilliseconds);

            stopwatch.Reset();
            stopwatch.Start();
            for (int i = 0; i < 100000; i++)
            {
                success = BurntMemory.ReadWrite.WriteInteger(AddressToWrite, 0xFFFFFFF, true);
            }
            stopwatch.Stop();

            Console.WriteLine("Test 2: Elapsed Time is {0} ms", stopwatch.ElapsedMilliseconds);



            stopwatch.Reset();
            stopwatch.Start();
            for (int i = 0; i < 100000; i++)
            {
                success = BurntMemory.ReadWrite.WriteInteger(AddressToWrite, 0xFFFFFFFF, false);
            }
            stopwatch.Stop();

            Console.WriteLine("Test 4: Elapsed Time is {0} ms", stopwatch.ElapsedMilliseconds);


        }

    }
}