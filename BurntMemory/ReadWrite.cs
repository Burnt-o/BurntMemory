using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
//using System.Diagnostics;

namespace BurntMemory
{
    public static class ReadWrite
    {

        public static AttachState AttachState
            {
            get { return BurntMemory.AttachState.Instance; }

        }

        //for reading/writing from process memory
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        private const int PROCESS_WM_READ = 0x0010;
        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress,
  int dwSize, uint flNewProtect, out uint lpflOldProtect);
        private const int PAGE_READWRITE = 0x40;


        public static IntPtr ResolveAddress(string modulename, int[] offsets)
        {
            IntPtr baseAddress = AttachState.modules[modulename];
            return ResolveAddress(baseAddress, offsets);
        }

        public static IntPtr ResolveAddress(IntPtr baseAddress, int[] offsets)
        {
            IntPtr ptr = baseAddress;

            if (offsets == null)
                return ptr;

            ptr = ptr + offsets[0];
            if (offsets.Length == 1)
                return ptr;

            offsets = offsets.Skip(1).ToArray();

            foreach (int i in offsets)
            {

                /*                if (IntPtr.Size == 4)
                                {
                                    Int32? read = ReadInteger(ptr);
                                    if (read.HasValue)
                                    ptr = IntPtr.Add(new IntPtr((ReadInteger(ptr).GetValueOrDefault())), i);
                                }
                                else 
                                {
                                    Int64? read = ReadInteger(ptr);
                                    if (read.HasValue)
                                        ptr = IntPtr.Add(new IntPtr((ReadQword(ptr).GetValueOrDefault())), i);
                                }*/

                ptr = (IntPtr.Size == 4)
                ? IntPtr.Add(new IntPtr((ReadInteger(ptr).GetValueOrDefault())), i)
                : ptr = IntPtr.Add(new IntPtr((ReadQword(ptr).GetValueOrDefault())), i);

                //TODO: test which of these approaches handles null values more gracefully (either the incoming offsets being literally null, or more likely scenario: readInteger/Qword is null.
            }


            return ptr;

        }








        public static UInt32? ReadInteger(IntPtr addy)
        {
            byte[] data = new byte[4];
            return (ReadProcessMemory(AttachState.GlobalProcessHandle, addy, data, 4, out int bytesRead)) ? BitConverter.ToUInt32(data, 0) : null;
        }

        public static Int64? ReadQword(IntPtr addy)
        {
            byte[] data = new byte[8];
            return (ReadProcessMemory(AttachState.GlobalProcessHandle, addy, data, 8, out int bytesRead)) ? BitConverter.ToInt64(data, 0) : null;
        }

        public static byte[]? ReadBytes(IntPtr addy, uint length = 1)
        {
            byte[]? data = new byte[length];
            return (ReadProcessMemory(AttachState.GlobalProcessHandle, addy, data, data.Length, out int bytesRead)) ? data : null;
        }

        
        public static string? ReadString(IntPtr addy, uint length) //TODO: add a unicode option
        {
            byte[] data = new byte[length];
            return (ReadProcessMemory(AttachState.GlobalProcessHandle, addy, data, data.Length, out int bytesRead)) ? ASCIIEncoding.ASCII.GetString(data) : null;
        }


        
        public static bool WriteInteger(IntPtr addy, UInt32 value, bool isProtected)
        {
            bool success;
            if (isProtected)
            {
                VirtualProtectEx(AttachState.GlobalProcessHandle, addy, 4, PAGE_READWRITE, out uint lpflOldProtect);
                success = WriteProcessMemory(AttachState.GlobalProcessHandle, addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
                VirtualProtectEx(AttachState.GlobalProcessHandle, addy, 4, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = WriteProcessMemory(AttachState.GlobalProcessHandle, addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
            }

            return success;
        }

        

        public static bool WriteBytes(IntPtr addy, byte[] value, bool isProtected)
        {
            bool success;
            if (isProtected)
            {
                VirtualProtectEx(AttachState.GlobalProcessHandle, addy, value.Length, PAGE_READWRITE, out uint lpflOldProtect);
                success = WriteProcessMemory(AttachState.GlobalProcessHandle, addy, value, value.Length, out int bytesWritten);
                VirtualProtectEx(AttachState.GlobalProcessHandle, addy, value.Length, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = WriteProcessMemory(AttachState.GlobalProcessHandle, addy, value, value.Length, out int bytesWritten);
            }

            return success;
        }





    }
}