using BurntMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BurntMemoryTest
{
    [TestClass]
    public class ReadWriteTest
    {
        public void Setup()
        {
        }

        [TestMethod]
        public void Test_Attach()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            Assert.AreEqual(mem.Attached, true);
        }

        [TestMethod]
        public void Test_ReadInteger()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadInteger(ptr);
            Assert.AreEqual(data, (uint)0x73696854);
        }

        [TestMethod]
        public void Test_ReadQword()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadQword(ptr);
            Assert.AreEqual(data, (ulong)0x6F72702073696854);
        }

        [TestMethod]
        public void Test_ReadBytes()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadBytes(ptr, 1);
            Assert.AreEqual(data[0], 0x54);
        }

        [TestMethod]
        public void Test_ReadString()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data = BurntMemory.ReadWrite.ReadString(ptr, 4, false);
            Assert.AreEqual(data, "This");
        }

        [TestMethod]
        public void Test_WriteInteger()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadInteger(ptr);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteInteger(ptr, 0xFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadInteger(ptr);
            Assert.AreEqual(data_after, 0xFFFFFFFF);
            BurntMemory.ReadWrite.WriteInteger(ptr, (uint)data_before, true);
        }

        [TestMethod]
        public void Test_WriteQword()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadQword(ptr);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteQword(ptr, 0xFFFFFFFFFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadQword(ptr);
            Assert.AreEqual(data_after, 0xFFFFFFFFFFFFFFFF);
            BurntMemory.ReadWrite.WriteQword(ptr, (ulong)data_before, true);
        }

        [TestMethod]
        public void Test_WriteBytes()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadBytes(ptr);
            BurntMemory.ReadWrite.WriteBytes(ptr, new byte[] { 0x69 }, true);
            var data = BurntMemory.ReadWrite.ReadBytes(ptr, 1);
            Assert.AreEqual(data[0], 0x69);
            BurntMemory.ReadWrite.WriteBytes(ptr, (byte[])data_before, true);
        }

        [TestMethod]
        public void Test_WriteString()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            ReadWrite.Pointer ptr = new ReadWrite.Pointer("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadString(ptr, 4, false);
            BurntMemory.ReadWrite.WriteString(ptr, "Heck", true, false);
            var data = BurntMemory.ReadWrite.ReadString(ptr, 4, false);
            Assert.AreEqual(data, "Heck");
            BurntMemory.ReadWrite.WriteString(ptr, (string)data_before, true, false);
        }
    }
}