using System.Text;

// using System.Diagnostics;
namespace BurntMemory
{
    public partial class ReadWrite
    {
        private AttachState _attachstate;

        public ReadWrite(AttachState attachState)
        {
            _attachstate = attachState;
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
                    //System.Diagnostics.Trace.WriteLine("HERE IS MY PROBLEM DEAR GOD WHY EEEEEEEEEE");
                    //the problem must be occuring here
                    //System.Diagnostics.Trace.WriteLine("moduleName: " + (string)ptr.Modulename);
                    //System.Diagnostics.Trace.WriteLine("offsets length: " + ptr.Offsets.Length);

                    IntPtr? res = ResolvePointer((string)ptr.Modulename, (int[])ptr.Offsets);
                    if (res == null)
                    {
                        //System.Diagnostics.Trace.WriteLine("RESULT WAS NULL?! WHY");
                    }
                    else
                    {
                       // System.Diagnostics.Trace.WriteLine("RESULT WAS NONNULL: " + res.Value.ToString("X"));
                    }

                    return ResolvePointer((string)ptr.Modulename, (int[])ptr.Offsets);
                }



                return ResolvePointer(IntPtr.Zero, (int[])ptr.Offsets);
            }


            return null;
        }

        // TODO: double check this makes sense with multi level pointers
        private IntPtr? ResolvePointer(string modulename, int[]? offsets)
        {
            //System.Diagnostics.Trace.WriteLine("Beginning module resolution");

            lock (_attachstate.modules)
            {
                IntPtr? baseAddress = null;
                if (!_attachstate.modules.ContainsKey(modulename) || _attachstate.modules[modulename] == null || _attachstate.modules[modulename].Address == null)
                {
                    //System.Diagnostics.Trace.WriteLine("horrible fix area");
                    if (modulename == "main")
                    {
                        //System.Diagnostics.Trace.WriteLine("main was null - trying horrible fix" + "BLEGHEGHGH");
                        //System.Diagnostics.Trace.WriteLine("_attachstate.modules.ContainsKey(modulename)" + _attachstate.modules.ContainsKey(modulename));
                        //System.Diagnostics.Trace.WriteLine("_attachstate.modules[modulename] == null" + _attachstate.modules[modulename] == null);
                        //System.Diagnostics.Trace.WriteLine("_attachstate.modules[modulename].BaseAddress == null" + _attachstate.modules[modulename].BaseAddress == null);
                        //try to fix it
                        uint? id = this._attachstate.ProcessID;
                        if (id == null) return null;
                        System.Diagnostics.Process? proc = System.Diagnostics.Process.GetProcessById((int)id);
                        if (proc == null) return null;
                        baseAddress = proc.MainModule?.BaseAddress;
                       // System.Diagnostics.Trace.WriteLine("proc mainmodule we're assigning to main: " + proc.MainModule?.BaseAddress.ToString("X"));
                        if (baseAddress != null)
                        {
                            System.Diagnostics.Trace.WriteLine("Horrible fix succeeded!");
                            _attachstate.modules.Remove("main");
                            _attachstate.modules.Add("main", new ReadWrite.Pointer(baseAddress));


                            //System.Diagnostics.Trace.WriteLine("Let's test it. does the key exist? " + (_attachstate.modules.ContainsKey("main") ? "yes" : "no"));
                            //System.Diagnostics.Trace.WriteLine("The address of main is: " + _attachstate.modules["main"].Address);
                        }
                        else
                        {
                            //System.Diagnostics.Trace.WriteLine("Why the fuck is this null?!");
                            return null; }
                    }

                    //return null;
                }


                if (modulename == "main")
                {
                    baseAddress = _attachstate.MainModuleBaseAddress;
                }
                else
                {
                    //System.Diagnostics.Trace.WriteLine("The ultimate problem must be here. The key we're looking for is: " + modulename);
                    //System.Diagnostics.Trace.WriteLine("Does modules contain this key? " + _attachstate.modules.ContainsKey(modulename));
                    if (_attachstate.modules.ContainsKey(modulename))
                    { 
                        ReadWrite.Pointer? ptr = _attachstate.modules[modulename];
                        //System.Diagnostics.Trace.WriteLine("Is the rw.ptr null?" + ptr == null);
                        if (ptr != null)
                        {
                            //System.Diagnostics.Trace.WriteLine("What's the module of this ptr? " + ptr.Modulename);
                        }
                    }
                    baseAddress = ResolvePointer(_attachstate.modules[modulename]);
                }

                return ResolvePointer(baseAddress, offsets);
            }
        }

        // TODO: test which of these approaches handles null values more gracefully (either the incoming offsets being literally null, or more likely scenario: readInteger/Qword is null.
        // TODO: should probably make the incomming baseaddress nullable?
        // TODO: test multi-level pointers
        private IntPtr? ResolvePointer(IntPtr? baseAddress, int[]? offsets)
        {
            if (baseAddress == null)
            {
                lock (_attachstate.modules)
                {
                    baseAddress = _attachstate.modules["main"]?.BaseAddress;
                }
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

        private byte[]? ReadData(IntPtr? addy, int length = 1)
        {
            if (_attachstate.Attached == false)
                return null;

            if (addy == null)
                return null;

            byte[]? data = new byte[length];
            return (PInvokes.ReadProcessMemory((IntPtr)_attachstate.processHandle, (IntPtr)addy, data, length, out _)) ? data : null;
        }


        public UInt32? ReadInteger(Pointer? ptr)
        { 
        return ReadInteger(ResolvePointer(ptr));
        }
        public UInt32? ReadInteger(IntPtr? addy)
        {
            byte[]? data = ReadData(addy, 4);
            return data != null ? BitConverter.ToUInt32(data, 0) : null;
        }

        public UInt64? ReadQword(Pointer? ptr)
        {
            return ReadQword(ResolvePointer(ptr));
        }
        public UInt64? ReadQword(IntPtr? addy)
        {
            byte[]? data = ReadData(addy, 8);
            return data != null ? BitConverter.ToUInt64(data, 0) : null;
        }


        public byte[]? ReadBytes(Pointer? ptr, int length = 1)
        {
            return ReadBytes(ResolvePointer(ptr), length);
        }
        public byte[]? ReadBytes(IntPtr? addy, int length = 1)
        {
            return ReadData(addy, length);
        }

        public byte? ReadByte(Pointer? ptr)
        {
            return ReadByte(ResolvePointer(ptr));
        }
        public byte? ReadByte(IntPtr? addy)
        {
            byte[]? data = ReadData(addy, 1);
            return data?[0];
        }

        public float? ReadFloat(Pointer? ptr)
        {
            return ReadFloat(ResolvePointer(ptr));
        }
        public float? ReadFloat(IntPtr? addy)
        {
            byte[]? data = ReadData(addy, 4);
            return data != null ? BitConverter.ToSingle(data, 0) : null;
        }

        public double? ReadDouble(Pointer? ptr)
        {
            return ReadDouble(ResolvePointer(ptr));
        }
        public double? ReadDouble(IntPtr? addy)
        {
            byte[]? data = ReadData(addy, 8);
            return data != null ? BitConverter.ToDouble(data, 0) : null;
        }

        public string? ReadString(Pointer? ptr, int length, bool unicode)
        { 
        return ReadString(ResolvePointer(ptr), length, unicode);    
        }
        public string? ReadString(IntPtr? addy, int length, bool unicode)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            byte[]? data = ReadData(addy, length);
            return data != null ? encoding.GetString(data) : null;
        }


        private bool WriteData(IntPtr? addy, byte[]? data, bool isProtected = false)
        {
            if (_attachstate.Attached == false || data == null)
                return false;

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

        public bool WriteInteger(Pointer? ptr, UInt32 value, bool isProtected = false)
        {
            return WriteInteger(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteInteger(IntPtr? addy, UInt32 value, bool isProtected = false)
        {
            return WriteData(addy, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteQword(Pointer? ptr, UInt64 value, bool isProtected = false)
        {
            return WriteQword(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteQword(IntPtr? addy, UInt64 value, bool isProtected = false)
        {
            return WriteData(addy, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteByte(Pointer? ptr, byte value, bool isProtected = false)
        {
            return WriteByte(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteByte(IntPtr? addy, byte value, bool isProtected = false)
        {
            return WriteData(addy, new byte[] { value }, isProtected);
        }

        public bool WriteBytes(Pointer? ptr, byte[] value, bool isProtected = false)
        {
            return WriteBytes(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteBytes(IntPtr? addy, byte[] value, bool isProtected = false)
        {
            return WriteData(addy, value, isProtected);
        }

        public bool WriteFloat(Pointer? ptr, float value, bool isProtected = false)
        { 
        return WriteFloat(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteFloat(IntPtr? addy, float value, bool isProtected = false)
        {
            return WriteData(addy, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteDouble(Pointer? ptr, double value, bool isProtected = false)
        {
            return WriteDouble(ResolvePointer(ptr), value, isProtected);
        }
        public bool WriteDouble(IntPtr? addy, double value, bool isProtected = false)
        {
            return WriteData(addy, BitConverter.GetBytes(value), isProtected);
        }

        public bool WriteString(Pointer? ptr, string stringtowrite, bool isProtected = false, bool unicode = false)
        { 
        return WriteString(ResolvePointer(ptr), stringtowrite, isProtected, unicode);
        }
        public bool WriteString(IntPtr? addy, string stringtowrite, bool isProtected = false, bool unicode = false)
        {
            Encoding encoding = unicode ? ASCIIEncoding.Unicode : ASCIIEncoding.ASCII;
            return WriteData(addy, encoding.GetBytes(stringtowrite), isProtected);
        }
    }
}