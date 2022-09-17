global using Console = System.Diagnostics.Debug;
using BurntMemory;
using System.Runtime.InteropServices;

namespace BurntMemoryUsage

{
    internal class Program
    {
        private static void Main()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            ulong setRCXto = 6;
            Console.WriteLine("Hello World! This is a dumb program for testing random shit as I code the rest of BurntMemory");
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessesToAttach = new string[] { "MCC-Win64-Shipping" };
            if (mem.AttachAndVerify())
            {
                mem.EvaluateModuleAddress("halo1");

                ReadWrite.Pointer ptr = new("main", new int[] { 0x03B80E98, 0x8, 0x4E });
                string? test = BurntMemory.ReadWrite.ReadString(ptr, 4, false);
                Console.WriteLine("Should say 'This': " + test);

                ReadWrite.Pointer BreakpointPtr = new("halo1", new int[] { 0xC540B5 });

                PInvokes.CONTEXT64 onBreakpoint(PInvokes.CONTEXT64 context)
                {
                    context.Rcx = setRCXto;
                    return context;
                }
                BurntMemory.Debugger.Instance.SetBreakpoint(BreakpointPtr, onBreakpoint);
            }
            Console.WriteLine(Marshal.GetLastWin32Error());

            System.Console.ReadKey();

            // BurntMemory.Debugger.Instance.CloseGracefully();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("I'm out of here");
            if (AttachState.Instance.VerifyAttached())
            {
                ReadWrite.Pointer BreakpointPtr2 = new("halo1", new int[] { 0xC540B5 });
                BurntMemory.Debugger.Instance.RemoveBreakpoint(BreakpointPtr2);
            }
        }
    }
}