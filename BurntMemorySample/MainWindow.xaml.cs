using BurntMemory;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.IO;


namespace BurntMemorySample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AttachState mem;
        private ReadWrite rw;
        private DebugManager? dbg;
        private DLLInjector? inj;
        private SpeedhackManager? spd;

        private UInt64 playeraddy = 0;

        private ExternalMemoryObject ScriptStateObject;
        private BoolManager boolManager;
        //private PointerStructs. pointerStructs;

        public MainWindow()
        {
            this.InitializeComponent();
            this.DataContext = this;
            SetupExternalMemoryObjects();

            this.mem = new AttachState();
            this.rw = new ReadWrite(this.mem);

            Events.ATTACH_EVENT += new EventHandler<Events.AttachedEventArgs>(Handle_Attach);
            Events.DEATTACH_EVENT += new EventHandler(Handle_Detach);
            this.mem.ProcessesToAttach = new string[] { "MCC-Win64-Shipping" };
            this.mem.TryToAttachTimer.Enabled = true;
            this.mem.ForceAttach();

            boolManager = new BoolManager(this);   
        }

        private void Handle_Attach(object? sender, Events.AttachedEventArgs? e)
        {
/*            LoadPointers load = new LoadPointers();
            if (load.Load(e.NameOfProcess, e.ProcessVersion, pointerStructs))
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetUI(true);
                }));
            }
            else
            {
                this.mem.Detach();
            }*/
        }

        private void Handle_Detach(object? sender, EventArgs? e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                SetUI(false);
            }));
        }

        private void SetUI(bool truth)
        {
            this.Effect1Checkpoint.IsEnabled = truth;
            this.Effect2Revert.IsEnabled = truth;
            this.Effect3Coresave.IsEnabled = truth;
            this.Effect4Coreload.IsEnabled = truth;
            this.Effect5Invuln.IsEnabled = truth;
            this.Effect6Speedhack.IsEnabled = truth;
            this.Effect7Medusa.IsEnabled = truth;
            this.Effect8Bool.IsEnabled = truth;
        }

        private void MainWindow_closing(object sender, CancelEventArgs e)
        {
            Trace.WriteLine("I'm out of here");
            if (this.mem.Attached)
            {
                if (this.rw.ReadBytes(Pointers.Medusa, 1)?[0] == 1)
                {
                    this.rw.WriteBytes(Pointers.Medusa, 0, false);
                }
            }

            if (this.dbg != null)
            {
                this.dbg.GracefullyCloseDebugger(sender, e);
            }

            if (this.spd != null)
            {
                this.spd.RemoveSpeedHack(sender, e);
            }
        }


        private enum ScriptFieldOffsets
        { 
        TickCount = 0,
        ExpressionIndex = 4
        }

        private void SetupExternalMemoryObjects()
        { 

            Field[] ScriptBlocks = new Field[8];
            for (int i = 0; i < ScriptBlocks.Length; i++)
            { 
            ScriptBlocks[i] = new Field(0 + (i * Offsets.ScriptGap), new Field[] {
            new Field ((int)ScriptFieldOffsets.TickCount),
            new Field ((int)ScriptFieldOffsets.ExpressionIndex),
            });
            }

            this.ScriptStateObject = new (Pointers.ScriptState, ScriptBlocks);
        }

        private bool GameStateIsValid(AttachState attachState, ReadWrite readWrite)
        {
            return true;
/*            if (attachState == null || attachState.Attached == false)
            {
                return false;
            }
            byte[]? menu = readWrite.ReadBytes(PointerStructs.MenuInd);
            byte[]? state = readWrite.ReadBytes(PointerStructs.StateInd);

            return (menu != null && menu[0] == 0x07 && state != null && state[0] != 44);*/
        }


        public void PrintMessage(string message)
        {
            // trim to 62 chars
            message = message.Length > 62 ? message[..61] : message;

            // add whitespace to fill up the 62 chars
            message = message.Length < 62 ? message.PadRight(62) : message;

            uint? tickcount = this.rw.ReadInteger(Pointers.Tickcount);
            Trace.WriteLine("tickcount: " + tickcount.ToString());
            if (tickcount != null)
            {

        ReadWrite.Pointer ptr = Pointers.MessageTC; //cache the result
                rw.WriteInteger(ptr, (uint)tickcount, true);
                rw.WriteString(ptr + Offsets.MessageText, message, true, true);
                rw.WriteBytes(ptr + Offsets.MessageFlag, new byte[] { 0, 0, 1, 0 }, true);
                rw.WriteInteger(ptr + Offsets.MessageInteger, 0xFFFFFFFF, true);
            }
        }

        private void CheckboxBoolmode_Click(object sender, RoutedEventArgs e)
        {
            if (GameStateIsValid(this.mem, this.rw) && rw.ReadString(Pointers.LevelName, 3, false) == "c40")
            {
                if (this.dbg == null)
                {
                    this.dbg = new DebugManager(this.mem, this.rw);
                }

                if (this.CheckboxBoolmode.IsChecked == true)
                {
                    Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;

                    onBreakpoint = context =>
                    {
                        this.playeraddy = (UInt64)context.R15;
                        if (CheckboxBoolmode.IsChecked.GetValueOrDefault())
                        {
                            this.boolManager.BoolModeLoop(this.mem, this.rw);
                        }
                        return context;
                    };
                    this.dbg.SetBreakpoint("PlayerAddy", Pointers.PlayerAddy, onBreakpoint);
                }
                else
                {
                    if (!CheckboxInvuln.IsChecked.GetValueOrDefault()) //Invuln uses PlayerAddy breakpoint so don't remove it if it's enabled
                    {
                        this.dbg.RemoveBreakpoint("PlayerAddy");
                    }
                }
            }
            else
            {
                this.CheckboxBoolmode.IsChecked = false;
                if (this.dbg != null && !this.CheckboxInvuln.IsChecked.GetValueOrDefault())
                {
                    this.dbg.ClearBreakpoints();
                }
            }
        }


       

        private void CheckboxInvuln_Click(object sender, RoutedEventArgs e)
        {
            if (GameStateIsValid(this.mem, this.rw))
            {
                if (this.dbg == null)
                {
                    this.dbg = new DebugManager(this.mem, this.rw);
                }

                if (this.CheckboxInvuln.IsChecked == true)
                {
                    Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;

                    onBreakpoint = context =>
                    {
                        this.playeraddy = (UInt64)context.R15;
                        if (CheckboxBoolmode.IsChecked.GetValueOrDefault())
                        {
                            this.boolManager.BoolModeLoop(this.mem, this.rw);
                        }
                        return context;
                    };
                    this.dbg.SetBreakpoint("PlayerAddy", Pointers.PlayerAddy, onBreakpoint);
                    onBreakpoint = context =>
                    {
                        if (context.Rdi == (this.playeraddy + 0xA0))
                        {
                            context.Rcx = 0;
                        }
                        return context;
                    };
                    this.dbg.SetBreakpoint("ShieldBreak", new ReadWrite.Pointer((IntPtr)this.rw.ResolvePointer(Pointers.ShieldBreak)), onBreakpoint);

                    onBreakpoint = context =>
                    {
                        if (context.Rdi == (this.playeraddy + 0xA0))
                        {
                            context.R9 = 0x0800;
                        }
                        return context;
                    };
                    this.dbg.SetBreakpoint("ShieldChip", new ReadWrite.Pointer((IntPtr)this.rw.ResolvePointer(Pointers.ShieldChip)), onBreakpoint);

                    onBreakpoint = context =>
                    {
                        if (context.Rbx == this.playeraddy)
                        {
                            context.Rbp = 0;
                        }
                        return context;
                    };
                    this.dbg.SetBreakpoint("Health", new ReadWrite.Pointer((IntPtr)this.rw.ResolvePointer(Pointers.Health)), onBreakpoint);
                }
                else
                {
                    if (!CheckboxBoolmode.IsChecked.GetValueOrDefault()) //BOOL mode uses PlayerAddy breakpoint so don't remove it if it's enabled
                    {
                        this.dbg.RemoveBreakpoint("PlayerAddy");
                    }
                    this.dbg.RemoveBreakpoint("ShieldBreak");
                    this.dbg.RemoveBreakpoint("ShieldChip");
                    this.dbg.RemoveBreakpoint("Health");
                }
            }
            else
            {
                this.CheckboxInvuln.IsChecked = false;
                this.CheckboxBoolmode.IsChecked = false;
                if (this.dbg != null)
                {
                    this.dbg.ClearBreakpoints();
                }
            }
        }

        private void CheckboxMedusa_Click(object sender, RoutedEventArgs e)
        {
            if (GameStateIsValid(this.mem, this.rw))
            {
                this.rw.WriteBytes(Pointers.Medusa, this.CheckboxMedusa.IsChecked == true ? (byte)1 : (byte)0, true);
            }
            else
            { 
                this.CheckboxMedusa.IsChecked = false;
            }
        }

        private void CheckboxSpeedhack_Click(object sender, RoutedEventArgs e)
        {
            if (this.mem.Attached)
            {
                if (this.spd == null || this.inj == null)
                {
                    this.inj = new DLLInjector(this.mem, this.rw);
                    this.spd = new SpeedhackManager(this.mem, this.rw, this.inj);
                }

                double value = (this.CheckboxSpeedhack.IsChecked == true) ? 10 : 1;
            }
            else
            { 
            CheckboxSpeedhack.IsChecked = false;
            }
        }

        private void TriggerCheckpoint(object sender, RoutedEventArgs e)
        {
                if (GameStateIsValid(this.mem, this.rw))
                {
                if (CheckboxBoolmode.IsChecked == true)
                {
                    this.rw.WriteBytes(Pointers.Checkpoint, new byte[] { 1 }, false);
                    boolManager.TrainerMessage = "(Custom Checkpoint!)";
                }
                else
                {
                    uint? currenttick = this.rw.ReadInteger(Pointers.Tickcount);
                    this.rw.WriteBytes(Pointers.CPMessageCall, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }, true);
                    PrintMessage("Custom Checkpoint... done");
                    this.rw.WriteBytes(Pointers.Checkpoint, new byte[] { 1 }, false);
                    Thread.Sleep(50);
                    this.rw.WriteBytes(Pointers.CPMessageCall, new byte[] { 0xE8, 0x5B, 0x1A, 0x08, 0x00 }, true);
                }
                }
        }

        private void TriggerCoreload(object sender, RoutedEventArgs e)
        {
                if (GameStateIsValid(this.mem, this.rw))
                {
                    this.rw.WriteBytes(Pointers.Coreload, new byte[] { 1 }, true);
                }
        }

        private void TriggerCoresave(object sender, RoutedEventArgs e)
        {
            if (GameStateIsValid(this.mem, this.rw))
            {
                this.rw.WriteBytes(Pointers.Coresave, new byte[] { 1 }, true);
                PrintMessage("Core Save... done");
            }
        }

        private void TriggerRevert(object sender, RoutedEventArgs e)
        {
            if (GameStateIsValid(this.mem, this.rw))
            {
                this.rw.WriteBytes(Pointers.Revert, new byte[] { 1 }, true);
            }
        }

        public static class Offsets
        {
            public static readonly int MessageFlag = 0x80;
            public static readonly int MessageInteger = 0x84;
            public static readonly int MessageText = 0x4;
            public static readonly int ScriptGap = 0x40;
        }

        public static class Pointers
        {
            public static readonly ReadWrite.Pointer SteamMenuInd = new("main", new int[] { 0x3E5DF29 });
            public static readonly ReadWrite.Pointer SteamStateInd = new("main", new int[] { 0x3F519E9 });
            public static readonly ReadWrite.Pointer WinMenuInd = new("main", new int[] { 0x3CAFAA9 });
            public static readonly ReadWrite.Pointer WinStateInd = new("main", new int[] { 0x3DA30E5 });
            public static readonly ReadWrite.Pointer Checkpoint = new("main", new int[] { 0x03B80E98, 0x8, 0x2AF8247 });
            public static readonly ReadWrite.Pointer Coreload = new("main", new int[] { 0x03B80E98, 0x8, 0x2AF825A });
            public static readonly ReadWrite.Pointer Coresave = new("main", new int[] { 0x03B80E98, 0x8, 0x2AF8259 });
            public static readonly ReadWrite.Pointer CPMessageCall = new("main", new int[] { 0x03B80E98, 0x8, 0xADC050 });
            public static readonly ReadWrite.Pointer Health = new("main", new int[] { 0x03B80E98, 0x8, 0xC03FE3 });
            public static readonly ReadWrite.Pointer Medusa = new("main", new int[] { 0x03B80E98, 0x8, 0x1ADB926 });
            public static readonly ReadWrite.Pointer MessageTC = new("main", new int[] { 0x03B80E98, 0x8, 0x2A54C98, 0x0 });
            public static readonly ReadWrite.Pointer PlayerAddy = new("main", new int[] { 0x03B80E98, 0x8, 0xAF17F7 });
            public static readonly ReadWrite.Pointer Revert = new("main", new int[] { 0x03B80E98, 0x8, 0x2AF8242 });
            public static readonly ReadWrite.Pointer ShieldBreak = new("main", new int[] { 0x03B80E98, 0x8, 0xC047A7 });
            public static readonly ReadWrite.Pointer ShieldChip = new("main", new int[] { 0x03B80E98, 0x8, 0xC046F7 });
            public static readonly ReadWrite.Pointer Tickcount = new("main", new int[] { 0x03B80E98, 0x8, 0x2B5FCE8 });
            public static readonly ReadWrite.Pointer ScriptState = new("main", new int[] { 0x03B80E98, 0x8, 0x291AF68 });
            public static readonly ReadWrite.Pointer LevelName = new("main", new int[] { 0x03B80E98, 0x8, 0 });
        }
    }
}