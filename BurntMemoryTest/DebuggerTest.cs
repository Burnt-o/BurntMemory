using BurntMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace BurntMemoryTest
{
    [TestClass]
    public class DebuggerTest
    {
        private BurntMemory.AttachState mem = new AttachState();

        [TestInitialize]
        public void SetUp()
        {
            this.mem.ProcessesToAttach = new string[] { "notepad" };
            this.mem.TryToAttachTimer.Enabled = true;
            this.mem.ForceAttach();
            Thread.Sleep(50);

        }

        [TestCleanup]
        public void CleanUp()
        {
            this.mem.GracefullyCloseDebugger();
            this.mem.Detach();
        }

        [TestMethod]
        public void Test_InterruptSet()
        {
            Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;
            onBreakpoint = context =>
            {
                //do nothing
                return context;
            };

            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x50 });
            this.mem.SetBreakpoint("This", ptr, onBreakpoint);
            Thread.Sleep(50);
            byte[] instruction = ReadWrite.ReadBytes(this.mem, ptr);
            Assert.AreEqual(instruction[0], 0xCC);
        }

        [TestMethod]
        public void Test_GetRegister() //for this test to past you must hover your cursor over the notepad window during the Thread.Sleep(1000)
        {
            ulong? RAXvalue = null;
            Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;
            onBreakpoint = context =>
            {
                RAXvalue = context.Rax; //value of RAX at this instruction should always be 1
                return context;
            };

            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0xB020 });
            this.mem.SetBreakpoint("GetMessageW", ptr, onBreakpoint);
            Thread.Sleep(1000);
            Assert.AreEqual(RAXvalue, (UInt64)1);
        }

        [TestMethod]
        public void Test_SetRegister() //for this test to past you must type "d" into the first character of the notepad window during the Thread.Sleep(5000)
        {
            mem.EvaluateModules(); // we need to make sure we have the msvcrt.dll module
            byte? R11_L_value = null;
            Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;
            onBreakpoint = context =>
            {
                R11_L_value = (byte)(context.R11 & 0xFF); //get value of lowest byte of R11. This is the char that is currently being written to the screen
                if (R11_L_value == 0x64) //char is "d"
                {
                    context.R11 = (context.R11 & 0xff00) | 0x65; //set char to "e"
                    Trace.WriteLine("R11 is now: " + context.R11);
                }
                return context;
            };

            foreach (KeyValuePair<string, IntPtr?> kv in this.mem.modules)
            {
                Trace.WriteLine("module: " + kv.Key);
            }

                ReadWrite.Pointer ptr = new ReadWrite.Pointer("msvcrt.dll", new int[] { 0x74428 });
            this.mem.SetBreakpoint("ChangeChar", ptr, onBreakpoint);

            Thread.Sleep(5000);

            ReadWrite.Pointer display_ptr = new ReadWrite.Pointer("main", new int[] { 0x00031690, 0, 0 });
            byte? first_displayed_value = ReadWrite.ReadBytes(this.mem, display_ptr)[0];
            Assert.AreEqual((Int32)first_displayed_value, 0x65);
        }
    }
}