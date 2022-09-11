using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BurntMemory;

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
            var data = BurntMemory.ReadWrite.ReadInteger(BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E }));
            Assert.AreEqual(data, (uint)0x73696854);
        }

        [TestMethod]
        public void Test_ReadQword()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            var data = BurntMemory.ReadWrite.ReadQword(BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E }));
            Assert.AreEqual(data, (ulong)0x6F72702073696854);
        }

        [TestMethod]
        public void Test_ReadBytes()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            var data = BurntMemory.ReadWrite.ReadBytes(BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E }), 1);
            Assert.AreEqual(data[0], 0x54);
        }

        [TestMethod]
        public void Test_ReadString()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            var data = BurntMemory.ReadWrite.ReadString(BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E }), 4);
            Assert.AreEqual(data, "This");
        }

        [TestMethod]
        public void Test_WriteInteger()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadInteger(AddressToWrite);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteInteger(AddressToWrite, 0xFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadInteger(AddressToWrite);
            Assert.AreEqual(data_after, 0xFFFFFFFF);
            BurntMemory.ReadWrite.WriteInteger(AddressToWrite, (uint)data_before, true);
        }

        [TestMethod]
        public void Test_WriteQword()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadQword(AddressToWrite);
            Console.WriteLine("data before: " + data_before);
            Console.WriteLine(BurntMemory.ReadWrite.WriteQword(AddressToWrite, 0xFFFFFFFFFFFFFFFF, true).ToString());
            var data_after = BurntMemory.ReadWrite.ReadQword(AddressToWrite);
            Assert.AreEqual(data_after, 0xFFFFFFFFFFFFFFFF);
            BurntMemory.ReadWrite.WriteQword(AddressToWrite, (ulong)data_before, true);
        }

        [TestMethod]
        public void Test_WriteBytes()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadBytes(AddressToWrite);
            BurntMemory.ReadWrite.WriteBytes(AddressToWrite, new byte[] { 0x69 }, true);
            var data = BurntMemory.ReadWrite.ReadBytes(AddressToWrite, 1);
            Assert.AreEqual(data[0], 0x69);
            BurntMemory.ReadWrite.WriteBytes(AddressToWrite, (byte[])data_before, true);
        }

        [TestMethod]
        public void Test_WriteString()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            var data_before = BurntMemory.ReadWrite.ReadString(AddressToWrite, 4);
            BurntMemory.ReadWrite.WriteString(AddressToWrite, "Heck", true);
            var data = BurntMemory.ReadWrite.ReadString(AddressToWrite, 4);
            Assert.AreEqual(data, "Heck");
            BurntMemory.ReadWrite.WriteString(AddressToWrite, (string)data_before, true);
        }




    }
}
