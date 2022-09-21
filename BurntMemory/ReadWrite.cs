﻿using System.Text;

// using System.Diagnostics;
namespace BurntMemory
{
    public class ReadWrite
    {
        private AttachState _attachstate;

        public ReadWrite(AttachState attachState)
        {
            _attachstate = attachState;
        }

        public class Pointer
        {
            public string? Modulename;
            public int[]? Offsets;
            public IntPtr? Address;
            public IntPtr? BaseAddress;

            //Pointer class has various constructor overloads. ResolvePointer method also has matching overloads to deal with these, the end result being an IntPtr?.
            public Pointer(string? a, int[]? b)
            {
                this.Modulename = a;
                this.Offsets = b;
                this.Address = null;
                this.BaseAddress = null;
            }

            public Pointer(IntPtr? a, int[]? b)
            {
                this.Modulename = null;
                this.Offsets = b;
                this.Address = null;
                this.BaseAddress = a;
            }

            public Pointer(IntPtr? a)
            {
                this.Modulename = null;
                this.Offsets = null;
                this.Address = a;
                this.BaseAddress = null;
            }

            public Pointer(int[]? a)
            {
                this.Modulename = null;
                this.Offsets = a;
                this.Address = null;
                this.BaseAddress = null;
            }

            public Pointer(string? a, int[]? b, IntPtr? c, IntPtr? d) // used for deepcopy in +operator
            {
                this.Modulename = a;
                this.Offsets = b;
                this.Address = c;
                this.BaseAddress = d;
            }

            //a static method for adding an offset to a Pointer
            public static Pointer? operator +(Pointer? a, int? b)
            {
                if (a == null || a.Offsets == null)
                {
                    return a;
                }

                // we don't want to modify the original Pointer, so make a copy
                Pointer c = new(a.Modulename, (int[])a.Offsets.Clone(), a.Address, a.BaseAddress);

                int? lastElement = c.Offsets[^1];

                lastElement += b; // add offset to last element
                c.Offsets[^1] = lastElement.GetValueOrDefault(); // update copied Pointer's last element
                return c;
            }
        }

        public IntPtr? ResolvePointer(Pointer? ptr)
        {
            if (ptr == null)
            {
                return null;
            }

            if (ptr.Address != null)
            {
                return (IntPtr)ptr.Address;
            }

            if (ptr.Offsets != null)
            {
                if (ptr.BaseAddress != null)
                {
                    return ResolvePointer((IntPtr)ptr.BaseAddress, ptr.Offsets);
                }

                if (ptr.Modulename != null)
                {
                    return ResolvePointer((string)ptr.Modulename, (int[])ptr.Offsets);
                }

                return ResolvePointer(IntPtr.Zero, (int[])ptr.Offsets);
            }
            return null;
        }

        // TODO: double check this makes sense with multi level pointers
        private IntPtr? ResolvePointer(string modulename, int[]? offsets)
        {
            if (_attachstate.modules[modulename] == null)
            {
                return null;
            }

            IntPtr? baseAddress = _attachstate.modules[modulename];
            return ResolvePointer(baseAddress, offsets);
        }

        // TODO: test which of these approaches handles null values more gracefully (either the incoming offsets being literally null, or more likely scenario: readInteger/Qword is null.
        // TODO: should probably make the incomming baseaddress nullable?
        // TODO: test multi-level pointers
        private IntPtr? ResolvePointer(IntPtr? baseAddress, int[]? offsets)
        {
            if (baseAddress == null)
            {
                baseAddress = _attachstate.modules["main"];
                if (baseAddress == null)
                {
                    return null;
                }
            }

            IntPtr ptr = (IntPtr)baseAddress;

            if (offsets == null)
            {
                return ptr;
            }

            ptr += offsets[0];
            if (offsets.Length == 1)
            {
                return ptr;
            }

            offsets = offsets.Skip(1).ToArray();

            foreach (int i in offsets)
            {
                ptr = IntPtr.Add(new IntPtr((long)ReadQword(new Pointer(ptr)).GetValueOrDefault()), i);
            }

            return ptr;
        }

        private byte[]? ReadData(Pointer? ptr, int length = 1)
        {
            if (_attachstate.Attached == false)
                return null;

            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null)
                return null;

            byte[]? data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)_attachstate.processHandle, (IntPtr)addy, data, length, out _)) ? data : null;
        }

        public UInt32? ReadInteger(Pointer? ptr)
        {
            byte[]? data = ReadData(ptr, 4);
            return data != null ? BitConverter.ToUInt32(data, 0) : null;
        }

        public UInt64? ReadQword(Pointer? ptr)
        {
            byte[]? data = ReadData(ptr, 8);
            return data != null ? BitConverter.ToUInt64(data, 0) : null;
        }

        public byte[]? ReadBytes(Pointer? ptr, int length = 1)
        {
            return ReadData(ptr, length);
        }

        public float? ReadFloat(Pointer? ptr)
        {
            byte[]? data = ReadData(ptr, 4);
            return data != null ? BitConverter.ToSingle(data, 0) : null;
        }

        public double? ReadDouble(AttachState state, Pointer? ptr)
        {
            byte[]? data = ReadData(ptr, 8);
            return data != null ? BitConverter.ToDouble(data, 0) : null;
        }

        public string? ReadString(Pointer? ptr, int length, bool unicode)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            byte[]? data = ReadData(ptr, length);
            return data != null ? encoding.GetString(data) : null;
        }

        // TODO: instead of just having a success boolean, we should instead return error codes (zero if no error)

        public bool WriteData(Pointer? ptr, byte[]? data, bool isProtected)
        {
            if (_attachstate.Attached == false)
                return false;

            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null)
                return false;

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)_attachstate.processHandle, (IntPtr)addy, data.Length, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)_attachstate.processHandle, (IntPtr)addy, data, data.Length, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)_attachstate.processHandle, (IntPtr)addy, data.Length, lpflOldProtect, out _);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)_attachstate.processHandle, (IntPtr)addy, data, data.Length, out int bytesWritten);
            }

            return success;
        }

        public bool WriteInteger(Pointer? ptr, UInt32 value, bool isProtected)
        {
            return WriteData(ptr, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteQword(Pointer? ptr, UInt64 value, bool isProtected)
        {
            return WriteData(ptr, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteBytes(Pointer? ptr, int value, bool isProtected)
        {
            return WriteData(ptr, new byte[] { (byte)value }, isProtected);
        }

        public bool WriteBytes(Pointer? ptr, byte value, bool isProtected)
        {
            return WriteData(ptr, new byte[] { value }, isProtected);
        }

        public bool WriteBytes(Pointer? ptr, byte[] value, bool isProtected)
        {
            return WriteData(ptr, value, isProtected);
        }

        public bool WriteFloat(Pointer? ptr, float value, bool isProtected)
        {
            return WriteData(ptr, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteDouble(Pointer? ptr, double value, bool isProtected)
        {
            return WriteData(ptr, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteString(Pointer? ptr, string stringtowrite, bool isProtected, bool unicode)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            return WriteData(ptr, encoding.GetBytes(stringtowrite), isProtected);
        }
    }
}