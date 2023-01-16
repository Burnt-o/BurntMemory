using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BurntMemory;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AttachState mem;
        private ReadWrite rw;
        private DLLInjector dll;
        private SpeedhackManager spd;

        public MainWindow()
        {
            InitializeComponent();

            this.mem = new AttachState();
            this.rw = new ReadWrite(this.mem);

            Events.ATTACH_EVENT += new EventHandler<Events.AttachedEventArgs>(Handle_Attach);

            this.mem.ProcessesToAttach = new string[] { "MCC-Win64-Shipping" };
            this.mem.TryToAttachTimer.Enabled = true;
            //this.mem.ForceAttach();
            this.dll = new(mem, rw);
            this.spd = new(mem, rw, dll);

        }


        private void Handle_Attach(object? sender, Events.AttachedEventArgs? e)
        {
            Trace.WriteLine("Attached");
        }


        private void Do(object sender, RoutedEventArgs e)
        {
            //Thread.Sleep(2000);
            Trace.WriteLine("injecting?");
            //spd.SetSpeed(1);
            bool success = dll.InjectDLL("ImGui DirectX 11 Kiero Hook.dll");
            if (success)
            {
               Trace.WriteLine("successful inject!");
            }
            else
            {
                Trace.WriteLine("failed inject!");
            }

        }


    }
}
