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
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World! This is a dumb program for testing random shit as I code the rest of BurntMemory");
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "MCC-Win64-Shipping";
            if (mem.AttachAndVerify())
            { 
            mem.EvaluateModuleAddress("halo1");

            ReadWrite.Pointer ptr = new ReadWrite.Pointer("halo1", new int[] { 0x4E });
            string? test = BurntMemory.ReadWrite.ReadString(ptr, 4);
            Console.WriteLine("Should say 'This': " + test);

            ReadWrite.Pointer BreakpointPtr = new ReadWrite.Pointer("halo1", new int[] { 0xC540B5 });
            BurntMemory.Debugger.Instance.SetBreakpoint(BreakpointPtr);
            }
            Console.WriteLine(Marshal.GetLastWin32Error());



                Console.ReadKey();


            //BurntMemory.Debugger.Instance.CloseGracefully();

        }



      

    }
}