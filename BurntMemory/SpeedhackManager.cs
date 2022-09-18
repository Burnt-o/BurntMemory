using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public class SpeedhackManager
    {

        private IntPtr? GetSpeedhackAddress()
        {
            IntPtr? address = null;
            AttachState.Instance.EvaluateModules();
                foreach (KeyValuePair<string, IntPtr?> kv in this.mem.modules)
            {
                if (kv.Key == "SpeedHack.dll")
                { 
                address = kv.Value;
                }
            }
            return address;
        }

        public bool SetSpeed(double speed)
        { 
        IntPtr? address = GetSpeedhackAddress();
            if (address == null)
            {
                SpeedhackInjector.InjectSpeedhack();
                address = GetSpeedhackAddress();

                if (address == null)
                { return false; }
                
            }
                
            int offsetofcontrolvar = 0x100; //TODO: find out what the value is
            ReadWrite.Pointer ptr = new ReadWrite.Pointer(address + offsetofcontrolvar);
            return ReadWrite.WriteDouble(ptr, speed, true);

        }


    }
}
