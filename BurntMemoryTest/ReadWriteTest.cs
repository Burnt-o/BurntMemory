using BurntMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace BurntMemoryTest
{
    [TestClass]
    public class ReadWriteTest
    {
        private AttachState mem = new();

        [TestInitialize]
        public void SetUp()
        {
            this.mem.ProcessesToAttach = new string[] { "notepad" };
            this.mem.TryToAttachTimer.Enabled = true;
            this.mem.ForceAttach();
        }

        [TestMethod]
        public void Test_Attach()
        {
            
            Assert.AreEqual(mem.Attached, true);
        }

        [TestMethod]
        public void Test_ReadInteger()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadInteger(this.mem, ptr);
            Assert.AreEqual(data, (uint)0x73696854);
        }

        [TestMethod]
        public void Test_ReadQword()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadQword(this.mem, ptr);
            Assert.AreEqual(data, (ulong)0x6F72702073696854);
        }

        [TestMethod]
        public void Test_ReadBytes()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadBytes(this.mem, ptr, 1);
            Assert.AreEqual(data[0], 0x54);
        }

        [TestMethod]
        public void Test_ReadString()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadString(this.mem, ptr, 4, false);
            Assert.AreEqual(data, "This");
        }

        [TestMethod]
        public void Test_WriteInteger()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadInteger(this.mem, ptr);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteInteger(this.mem, ptr, 0xFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadInteger(this.mem, ptr);
            Assert.AreEqual(data_after, 0xFFFFFFFF);
            BurntMemory.ReadWrite.WriteInteger(this.mem, ptr, (uint)data_before, true);
        }

        [TestMethod]
        public void Test_WriteQword()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadQword(this.mem, ptr);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteQword(this.mem, ptr, 0xFFFFFFFFFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadQword(this.mem, ptr);
            Assert.AreEqual(data_after, 0xFFFFFFFFFFFFFFFF);
            BurntMemory.ReadWrite.WriteQword(this.mem, ptr, (ulong)data_before, true);
        }

        [TestMethod]
        public void Test_WriteBytes()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadBytes(this.mem, ptr);
            BurntMemory.ReadWrite.WriteBytes(this.mem, ptr, new byte[] { 0x69 }, true);
            var data = BurntMemory.ReadWrite.ReadBytes(this.mem, ptr, 1);
            Assert.AreEqual(data[0], 0x69);
            BurntMemory.ReadWrite.WriteBytes(this.mem, ptr, (byte[])data_before, true);
        }

        [TestMethod]
        public void Test_WriteString()
        {
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadString(this.mem, ptr, 4, false);
            BurntMemory.ReadWrite.WriteString(this.mem, ptr, "Heck", true, false);
            var data = BurntMemory.ReadWrite.ReadString(this.mem, ptr, 4, false);
            Assert.AreEqual(data, "Heck");
            BurntMemory.ReadWrite.WriteString(this.mem, ptr, (string)data_before, true, false);
        }
    }
}