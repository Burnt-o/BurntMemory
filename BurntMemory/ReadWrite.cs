using System.Text;

// using System.Diagnostics;
namespace BurntMemory
{
    public static class ReadWrite
    {
        // TODO: do I need this?
        public static AttachState AttachState
        {
            get { return BurntMemory.AttachState.Instance; }
        }

        public class Pointer
        {
            public string? Modulename;
            public int[]? Offsets;
            public IntPtr? Address;
            public IntPtr? BaseAddress;

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

            public static Pointer? operator +(Pointer? a, int? b)
            {
                if (a == null || a.Offsets == null)
                {
                    return a;
                }

                // we don't want to modify the original Pointer, so make a copy
                Pointer c = new(a.Modulename, (int[])a.Offsets.Clone(), a.Address, a.BaseAddress);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                int? lastElement = c.Offsets[^1];
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                lastElement += b; // add offset to last element
                c.Offsets[^1] = lastElement.GetValueOrDefault(); // update copied Pointer's last element
                return c;
            }
        }

        public static IntPtr? ResolvePointer(Pointer? ptr)
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
        private static IntPtr? ResolvePointer(string modulename, int[]? offsets)
        {
            if (AttachState.modules[modulename] == null)
            {
                return null;
            }

            IntPtr? baseAddress = AttachState.modules[modulename];
            return ResolvePointer(baseAddress, offsets);
        }

        // TODO: test which of these approaches handles null values more gracefully (either the incoming offsets being literally null, or more likely scenario: readInteger/Qword is null.
        // TODO: should probably make the incomming baseaddress nullable?
        // TODO: test multi-level pointers
        private static IntPtr? ResolvePointer(IntPtr? baseAddress, int[]? offsets)
        {
            if (baseAddress == null)
            {
                baseAddress = AttachState.modules["main"];
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

        public static UInt32? ReadInteger(Pointer? ptr)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[] data = new byte[4];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, 4, out int bytesRead)) ? BitConverter.ToUInt32(data, 0) : null;
        }

        public static UInt64? ReadQword(Pointer? ptr)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[] data = new byte[8];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, 8, out int bytesRead)) ? (ulong)BitConverter.ToInt64(data, 0) : null;
        }

        public static byte[]? ReadBytes(Pointer? ptr, uint length = 1)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[]? data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? data : null;
        }


        public static float? ReadFloat (Pointer? ptr)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[]? data = new byte[4];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? BitConverter.ToSingle(data) : null; 
        }

        public static double? ReadDouble(Pointer? ptr)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[]? data = new byte[8];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? BitConverter.ToDouble(data) : null; 
        }

        // TODO: add a unicode option
        public static string? ReadString(Pointer? ptr, uint length, bool unicode)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return null;
            }

            byte[] data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, data, data.Length, out int bytesRead)) ? encoding.GetString(data) : null;
        }

        // TODO: instead of just having a success boolean, we should instead return error codes (zero if no error)
        public static bool WriteInteger(Pointer? ptr, UInt32 value, bool isProtected)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 4, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 4, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteQword(Pointer? ptr, UInt64 value, bool isProtected)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 8, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 8, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteBytes(Pointer? ptr, int value, bool isProtected)
        {
            return WriteBytes(ptr, new byte[] { (byte)value }, isProtected);
        }

        public static bool WriteBytes(Pointer? ptr, byte value, bool isProtected)
        {
            return WriteBytes(ptr, new byte[] { value }, isProtected);
        }

        public static bool WriteBytes(Pointer? ptr, byte[] value, bool isProtected)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, value.Length, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, value, value.Length, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, value.Length, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, value, value.Length, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteFloat(Pointer? ptr, float value, bool isProtected)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 4, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 4, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 4, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteDouble(Pointer? ptr, double value, bool isProtected)
        {
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            bool success;
            if (isProtected)
            {
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 8, PInvokes.PAGE_READWRITE, out uint lpflOldProtect);
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
                PInvokes.VirtualProtectEx((IntPtr)AttachState.processHandle, (IntPtr)addy, 8, lpflOldProtect, out uint lpflOldProtect2);
            }
            else
            {
                success = PInvokes.WriteProcessMemory((IntPtr)AttachState.processHandle, (IntPtr)addy, BitConverter.GetBytes(value), 8, out int bytesWritten);
            }

            return success;
        }

        public static bool WriteString(Pointer? ptr, string stringtowrite, bool isProtected, bool unicode)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            IntPtr? addy = ResolvePointer(ptr);
            if (addy == null || AttachState.processHandle == null)
            {
                return false;
            }

            byte[] value = encoding.GetBytes(stringtowrite);
            return WriteBytes(new Pointer((IntPtr)addy), value, isProtected);
        }
    }
}