using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BurntMemory;

namespace BurntMemorySample
{
    public class BoolManager
    {

        public BoolManager(MainWindow a)
        {
            mw = a;
        }
        private MainWindow mw;
        private uint checkindex = 0;

        private uint? lasttick = 0;
        private uint? currenttick = 0;

        private uint lastbsp = 0;
            private uint currentbsp = 0;

        private TriggerInformation[] TriggerInformationArray =
            {


        };

        public string? TrainerMessage { get; set; }
        public void BoolModeLoop(AttachState mem, ReadWrite rw)
        {
            lasttick = currenttick;
            currenttick = rw.ReadInteger(MainWindow.Pointers.Tickcount);

            if (currenttick < lasttick) //player reverted/core loaded
            {
                checkindex = 0;
            }

            string BoolMessage = string.Format("%02d", checkindex - 1) + "/37 Trigs. Next: ";




            mw.PrintMessage(BoolMessage + TrainerMessage);
            TrainerMessage = null;
        }

    }
}
