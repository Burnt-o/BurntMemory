using BurntMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace BurntMemoryTest
{
    [TestClass]
    public class DebuggerTest
    {
        public BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
        public BurntMemory.DebugManager dbg = BurntMemory.DebugManager.Instance;

        [TestInitialize]
        public void SetUp()
        {
            this.mem.ProcessesToAttach = new string[] { "notepad" };
            this.mem.TryToAttachTimer.Enabled = true;
            this.mem.ForceAttach();
            

        }

        [TestMethod]
        public void Test_InterruptSet()
        {
            dbg.SetBreakpoint("This", new ReadWrite.Pointer("main", new int[] { 0x4E }), )
            Assert.AreEqual(mem.Attached, true);
        }

       
    }
}