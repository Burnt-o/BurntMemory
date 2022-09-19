using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public partial class AttachState
    {

        private IntPtr? GetSpeedhackAddress()
        {
            IntPtr? address = null;
            EvaluateModules();
                foreach (KeyValuePair<string, IntPtr?> kv in modules)
            {
                if (kv.Key == "SpeedHack.dll")
                { 
                address = kv.Value;
                }
            }
            return address;
        }

        public void RemoveSpeedHack() //basically same as SetSpeed but without trying to inject the dll. TODO: make this actually unload the DLL if it's in there and we're still attached.
        {
            IntPtr? address = GetSpeedhackAddress();
            if (address != null)
            {
                const int offset_newspeed = 0x6740;
                const int offset_newspeedflag = 0x6738;
                ReadWrite.Pointer ptr = new ReadWrite.Pointer(address + offset_newspeed);
                if (ReadWrite.WriteDouble(this, ptr, 1, true))
                {
                    ptr = new ReadWrite.Pointer(address + offset_newspeedflag);
                    ReadWrite.WriteBytes(this, ptr, (byte)1, true);
                }
            }
        }


        public bool SetSpeed(double speed)
        { 
        IntPtr? address = GetSpeedhackAddress();
            if (address == null)
            {
                InjectSpeedhack();
                Thread.Sleep(50);
                address = GetSpeedhackAddress();

                if (address == null)
                { return false; }
                
            }
                
            //these offsets will have to be updated whenever the speedhack dll is modified and rebuilt.
            const int offset_newspeed = 0x6740;
            const int offset_newspeedflag = 0x6738;
            ReadWrite.Pointer ptr = new ReadWrite.Pointer(address + offset_newspeed);
            if (ReadWrite.WriteDouble(this, ptr, speed, true))
            { 
            ptr = new ReadWrite.Pointer(address + offset_newspeedflag);
                return ReadWrite.WriteBytes(this, ptr, (byte)1, true);
            }
            return false;

        }


    }
}
