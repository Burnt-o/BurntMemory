using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BurntMemory;

namespace BurntMemoryTest
{
    [TestClass]
    public class UnitTest1
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
        public void Test_WriteBytes()
        {
            BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;
            mem.ProcessToAttach = "notepad";
            mem.AttachAndVerify();
            IntPtr AddressToWrite = BurntMemory.ReadWrite.ResolveAddress("main", new int[] { 0x4E });
            BurntMemory.ReadWrite.WriteBytes(AddressToWrite, new byte[] { 0x69 }, true);
            var data = BurntMemory.ReadWrite.ReadBytes(AddressToWrite, 1);
            Assert.AreEqual(data[0], 0x69);
            BurntMemory.ReadWrite.WriteBytes(AddressToWrite, new byte[] { 0x54 }, true);
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
            BurntMemory.ReadWrite.WriteInteger(AddressToWrite, 0x73696854, true);
        }

    }
}
