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
        //TODO: do I need this?
        public static AttachState AttachState
            {
            get { return BurntMemory.AttachState.Instance; }

        }

        //for reading/writing from process memory
       

        //TODO: double check this makes sense with multi level pointers
        public static IntPtr ResolveAddress(string modulename, int[] offsets)
        {
            IntPtr baseAddress = AttachState.modules[modulename];
            return ResolveAddress(baseAddress, offsets);
        }

        //TODO: test which of these approaches handles null values more gracefully (either the incoming offsets being literally null, or more likely scenario: readInteger/Qword is null.
        //TODO: should probably make the incomming baseaddress nullable?
        //TODO: test multi-level pointers
        public static IntPtr ResolveAddress(IntPtr baseAddress, int[] offsets)
        {
            IntPtr ptr = baseAddress;

            if (offsets == null)
                return ptr;

            ptr += offsets[0];
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
                : ptr = IntPtr.Add(new IntPtr((long)ReadQword(ptr).GetValueOrDefault()), i);
                //here's a question- why do IntPtrs use signed ints instead of unsigned? and will that cause issues?

            }


            return ptr;

        }







        
        public static UInt32? ReadInteger(IntPtr? addy)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return null;

            byte[] data = new byte[4];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, data, 4, out int bytesRead)) ? BitConverter.ToUInt32(data, 0) : null;
        }

        public static UInt64? ReadQword(IntPtr? addy)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return null;

            byte[] data = new byte[8];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, data, 8, out int bytesRead)) ? (ulong)BitConverter.ToInt64(data, 0) : null;
        }

        public static byte[]? ReadBytes(IntPtr? addy, uint length = 1)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return null;
            byte[]? data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? data : null;
        }

        //TODO: add a unicode option
        public static string? ReadString(IntPtr? addy, uint length) 
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return null;
            byte[] data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? ASCIIEncoding.ASCII.GetString(data) : null;
        }


        //TODO: instead of just having a success boolean, we should instead return error codes (zero if no error)
        public static bool WriteInteger(IntPtr? addy, UInt32 value, bool isProtected)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return false;

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, 4, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, 4, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteQword(IntPtr? addy, UInt64 value, bool isProtected)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return false;

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, 8, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, 8, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
            }

            return success;
        }



        public static bool WriteBytes(IntPtr? addy, byte[] value, bool isProtected)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return false;

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, value.Length, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, value, value.Length, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, value.Length, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.GlobalProcessHandle, (IntPtr)addy, value, value.Length, out int bytesWritten);
            }

            return success;
        }

        //TODO: add a unicode option
        public static bool WriteString(IntPtr? addy, string stringtowrite, bool isProtected)
        {
            if (addy == null || AttachState.GlobalProcessHandle == null)
                return false;

            byte[] value = Encoding.ASCII.GetBytes(stringtowrite);
            return WriteBytes(addy, value, isProtected);
        }




    }
}