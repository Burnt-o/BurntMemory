﻿using BurntMemory;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace BurntMemorySample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public BurntMemory.Debugger dbg = BurntMemory.Debugger.Instance;

        public BurntMemory.AttachState mem = BurntMemory.AttachState.Instance;

        private UInt64 playeraddy = 0;

        public MainWindow()
        {
            // AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            InitializeComponent();
            mem.ProcessesToAttach = new string[] { "MCC-Win64-Shipping" };
        }

        private static void PrintMessage(string message)
        {
            // trim to 62 chars
            message = message.Length > 62 ? message[..61] : message;

            // add whitespace to fill up the 62 chars
            message = message.Length < 62 ? message.PadRight(62) : message;

            uint? tickcount = ReadWrite.ReadInteger(Pointers.Tickcount);
            Debug.WriteLine("tickcount: " + tickcount.ToString());
            if (tickcount != null)
            {
                Debug.WriteLine("1: " + ReadWrite.WriteInteger(Pointers.MessageTC, (uint)tickcount, true).ToString());
                Debug.WriteLine("2: " + ReadWrite.WriteString(Pointers.MessageTC + Offsets.MessageText, message, true, true).ToString());
                Debug.WriteLine("3: " + ReadWrite.WriteBytes(Pointers.MessageTC + Offsets.MessageFlag, new byte[] { 0, 0, 1, 0 }, true).ToString());
                Debug.WriteLine("4: " + ReadWrite.WriteInteger(Pointers.MessageTC + Offsets.MessageInt, 0xFFFFFFFF, true).ToString());
            }
        }

        private void CheckboxBoolmode_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CheckboxInvuln_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify())
                {
                    if (CheckboxInvuln.IsChecked == true)
                    {
                        dbg.ClearBreakpoints();
                        Func<PInvokes.CONTEXT64, PInvokes.CONTEXT64> onBreakpoint;

                        onBreakpoint = context =>
                        {
                            playeraddy = (UInt64)context.R15;
                            return context;
                        };
                        dbg.SetBreakpoint(Pointers.PlayerAddy, onBreakpoint);
                        // dbg.SetBreakpoint(new ReadWrite.Pointer((IntPtr)ReadWrite.ResolvePointer(Pointers.PlayerAddy)), onBreakpoint);

                        onBreakpoint = context =>
                        {
                            if (context.Rdi == (playeraddy + 0xA0))
                            {
                                context.Rcx = 0;
                            }
                            return context;
                        };
                        dbg.SetBreakpoint(new ReadWrite.Pointer((IntPtr)ReadWrite.ResolvePointer(Pointers.ShieldBreak)), onBreakpoint);

                        onBreakpoint = context =>
                        {
                            if (context.Rdi == (playeraddy + 0xA0))
                            {
                                context.R9 = 0x0800;
                            }
                            return context;
                        };
                        dbg.SetBreakpoint(new ReadWrite.Pointer((IntPtr)ReadWrite.ResolvePointer(Pointers.ShieldChip)), onBreakpoint);

                        onBreakpoint = context =>
                        {
                            if (context.Rbx == playeraddy)
                            {
                                context.Rbp = 0;
                            }
                            return context;
                        };
                        dbg.SetBreakpoint(new ReadWrite.Pointer((IntPtr)ReadWrite.ResolvePointer(Pointers.Health)), onBreakpoint);
                    }
                    else
                    {
                        dbg.ClearBreakpoints();
                        /*                        Debug.WriteLine("_BreakpointList.Count: "+ BurntMemory.Debugger._BreakpointList.Count);
                                                if (BurntMemory.Debugger._BreakpointList.Count > 0)
                                                {
                                                    foreach (BurntMemory.Debugger.Breakpoint bp in BurntMemory.Debugger._BreakpointList)
                                                    {
                                                        Debug.WriteLine("bp.Pointer: " + bp.Pointer?.ToString());
                                                        Debug.WriteLine("bp.onBreakpoint: " + bp.onBreakpoint.ToString());
                                                        Debug.WriteLine("bp.originalCode: " + bp.originalCode.ToString());
                                                    }
                                                }*/
                    }
                }
                else
                    throw new Exception("CheckboxInvuln test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("CheckboxInvuln error, " + ex.Message + ", " + PInvokes.GetLastError());
            }
        }

        private void CheckboxMedusa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify())
                {
                    ReadWrite.WriteBytes(Pointers.Medusa, CheckboxMedusa.IsChecked == true ? 1 : 0, true);
                }
                else
                    throw new Exception("TriggerRevert test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("TriggerRevert error, " + ex.Message + ", " + PInvokes.GetLastError());
                CheckboxMedusa.IsChecked = false;
            }
        }

        private void CheckboxSpeedhack_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void MainWindow_closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("I'm out of here");
            if (AttachState.Instance.VerifyAttached())
            {
                if (ReadWrite.ReadBytes(Pointers.Medusa, 1)?[0] == 1)
                {
                    ReadWrite.WriteBytes(Pointers.Medusa, 0, false);
                }

                // below stuff should probably be moved to the Debugger Library at some point as some kind of "cleanup debugger" function.
                BurntMemory.Debugger.Instance.ClearBreakpoints();
            }

            dbg.ExitThread = true;
            if (!dbg._DebugThread.Join(1000)) // wait for thread to finish executing, or 2s
            { Debug.WriteLine("debugger thread didn't exit on it's own :("); }
            else
            { Debug.WriteLine("wow the thread exited on it's own!"); }
        }

        private void TriggerCheckpoint(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify() && ReadWrite.ReadInteger(Pointers.CPMessageCall) == 135945192)
                {
                    uint? currenttick = ReadWrite.ReadInteger(Pointers.Tickcount);
                    ReadWrite.WriteBytes(Pointers.CPMessageCall, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }, true);
                    PrintMessage("Custom Checkpoint... done");
                    ReadWrite.WriteBytes(Pointers.Checkpoint, new byte[] { 1 }, false);
                    Thread.Sleep(500);
                    ReadWrite.WriteBytes(Pointers.CPMessageCall, new byte[] { 0xE8, 0x5B, 0x1A, 0x08, 0x00 }, true);
                }
                else
                    throw new Exception("TriggerCheckpoint test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("TriggerCheckpoint error, " + ex.Message + ", " + PInvokes.GetLastError());
            }
        }

        private void TriggerCoreload(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify())
                {
                    ReadWrite.WriteBytes(Pointers.Coreload, new byte[] { 1 }, true);
                }
                else
                    throw new Exception("TriggerRevert test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("TriggerRevert error, " + ex.Message + ", " + PInvokes.GetLastError());
            }
        }

        private void TriggerCoresave(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify())
                {
                    ReadWrite.WriteBytes(Pointers.Coresave, new byte[] { 1 }, true);
                }
                else
                    throw new Exception("TriggerRevert test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("TriggerRevert error, " + ex.Message + ", " + PInvokes.GetLastError());
            }
        }

        private void TriggerRevert(object sender, RoutedEventArgs e)
        {
            try
            {
                if (mem.AttachAndVerify())
                {
                    ReadWrite.WriteBytes(Pointers.Revert, new byte[] { 1 }, true);
                }
                else
                    throw new Exception("TriggerRevert test failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show("TriggerRevert error, " + ex.Message + ", " + PInvokes.GetLastError());
            }
        }

        public static class Offsets
        {
            public static readonly int MessageFlag = 0x80;
            public static readonly int MessageInt = 0x84;
            public static readonly int MessageText = 0x4;
        }

        public static class Pointers
        {
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
            public static readonly ReadWrite.Pointer SteamMenuInd = new("main", new int[] { 0x03B80E98, 0x8, 0x3A4A7C9 });
            public static readonly ReadWrite.Pointer SteamStateInd = new("main", new int[] { 0x03B80E98, 0x8, 0x3B40D69 });
            public static readonly ReadWrite.Pointer Tickcount = new("main", new int[] { 0x03B80E98, 0x8, 0x2B5FCE8 });
            public static readonly ReadWrite.Pointer WinstMenuInd = new("main", new int[] { 0x03B80E98, 0x8, 0x36ADD10 });
            public static readonly ReadWrite.Pointer WinstStateInd = new("main", new int[] { 0x03B80E98, 0x8, 0x39E3865 });
        }
    }
}