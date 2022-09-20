using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public class SpeedhackManager
    {

        private AttachState _attachState;
        private ReadWrite _readWrite;
        private DLLInjector _injector;
        public SpeedhackManager(AttachState attachState, ReadWrite readWrite, DLLInjector injector)
            {
            _attachState = attachState;
            _readWrite = readWrite;
            _injector = injector;
            }

        private IntPtr? GetSpeedhackAddress()
        {
            IntPtr? address = null;
            _attachState.EvaluateModules();
                foreach (KeyValuePair<string, IntPtr?> kv in _attachState.modules)
            {
                if (kv.Key == "SpeedHack.dll")
                { 
                address = kv.Value;
                }
            }
            return address;
        }

        public void RemoveSpeedHack(object? sender, EventArgs? e) //basically same as SetSpeed but without trying to inject the dll. TODO: make this actually unload the DLL if it's in there and we're still attached.
        {
            IntPtr? address = GetSpeedhackAddress();
            if (address != null)
            {
                const int offset_newspeed = 0x6740;
                const int offset_newspeedflag = 0x6738;
                ReadWrite.Pointer ptr = new ReadWrite.Pointer(address + offset_newspeed);
                if (_readWrite.WriteDouble(ptr, 1, true))
                {
                    ptr = new ReadWrite.Pointer(address + offset_newspeedflag);
                    _readWrite.WriteBytes(ptr, (byte)1, true);
                }
            }
        }


        public bool SetSpeed(double speed)
        { 
        IntPtr? address = GetSpeedhackAddress();
            if (address == null)
            {
                _injector.InjectDLL("SpeedHack.dll");
                Thread.Sleep(50);
                address = GetSpeedhackAddress();

                if (address == null)
                { return false; }
                
            }
                
            //these offsets will have to be updated whenever the speedhack dll is modified and rebuilt.
            const int offset_newspeed = 0x6740;
            const int offset_newspeedflag = 0x6738;
            ReadWrite.Pointer ptr = new ReadWrite.Pointer(address + offset_newspeed);
            if (_readWrite.WriteDouble(ptr, speed, true))
            { 
            ptr = new ReadWrite.Pointer(address + offset_newspeedflag);
                return _readWrite.WriteBytes(ptr, (byte)1, true);
            }
            return false;

        }


    }
}
