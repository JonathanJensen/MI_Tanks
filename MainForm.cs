﻿using MapInfo.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace MI_Tanks
{
    public partial class MainForm : Form
    {
        private IMapInfoPro mapInfo;
        private IMapBasicApplication mapbasicApplication;
        private float tankAngle = 0;
        private string username = null;
        private bool disableMB = false;
        private int rotation = 15;
        private double move = 1.0;
        private bool keyup = false;
        private bool keydown = false;
        private bool keyleft = false;
        private bool keyright = false;

        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream = default(NetworkStream);
        string readData = null;
        private static string dllpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private void SendMessage(string message)
        {
            new Thread(delegate () {
                SendMessageThread(message);
            }).Start();
        }

        private void SendMessageThread(string message)
        {
            try
            {
                Debug.WriteLine(message);
                byte[] outStream = System.Text.Encoding.ASCII.GetBytes(message + "\n");
                serverStream.Write(outStream, 0, outStream.Length);
                serverStream.Flush();
            }
            catch { }
        }

        public MainForm(IMapInfoPro mapInfo, IMapBasicApplication mbApplication, string username)
        {
            this.mapInfo = mapInfo;
            this.mapbasicApplication = mbApplication;
            this.username = username;
            InitializeComponent();
            KeyPreview = true;

        }

        private void getMessage()
        {
            while (true)
            {
                NetworkStream ns = clientSocket.GetStream();
                byte[] receivedBytes = new byte[1024];
                int byte_count;

                //StringBuilder text = new StringBuilder();

                try
                {
                    while ((byte_count = ns.Read(receivedBytes, 0, receivedBytes.Length)) > 0)
                    {
                        string text = (Encoding.ASCII.GetString(receivedBytes, 0, byte_count));
                        readData = text;
                        Debug.WriteLine(text);
                        msg();
                    }
                }
                catch
                {
                    break;
                }

            }
        }

        private void msg()
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(msg));
            else
            {
                string[] lines = readData.Split('\n');
                foreach (string line in lines)
                {
                    string ln = line.Trim('\r');
                    if (!string.IsNullOrEmpty(ln))
                    {
                        try
                        {
                            /*NotificationObject obj = new NotificationObject();
                            obj.Message = "Test bubble";
                            obj.NotificationLocation = new System.Windows.Point(10, 10);
                            obj.Title = "Title Test";
                            obj.Type = NotificationType.Info;
                            obj.TimeToShow = 2000;
                            mapInfo.ShowNotification(obj);*/

                            if (!disableMB)
                            {
                                if (ln.StartsWith("/mb "))
                                {
                                    ln = ln.Replace("/mb ", "");

                                    RunMBCommand(ln);
                                }
                                else if (ln.StartsWith("/c"))
                                {
                                    string[] parms = ln.Substring(3).Split(',');
                                    mapbasicApplication.CallMapBasicSubroutine("SetColor", parms);
                                }
                                // These are done seperatly on the client so we can have relative paths from the mbx in the future
                                else if (ln.StartsWith("/OpnTnkTbl"))
                                {
                                    RunMBCommand("Open Table \"" + Path.Combine(dllpath, "data\\tank.TAB") + "\"");
                                }
                                else if (ln.StartsWith("/OpnPlrTbl"))
                                {
                                    RunMBCommand("Open Table \"" + Path.Combine(dllpath, "data\\Players.TAB") + "\"");
                                }
                            }

                            /*
                            if (!Application.Current.Dispatcher.CheckAccess())
                            {
                                Application.Current.Dispatcher.Invoke(
                                        () => mapInfo.RunMapBasicCommand(ln), DispatcherPriority.Normal);
                            }
                            else
                            {
                                mapInfo.RunMapBasicCommand(ln);
                            }
                        */
                            Application.DoEvents();
                            //System.Threading.Thread.Sleep(100);
                        }
                        catch { }
                    }
                }
            }
        }

        private void RunMBCommand(string command)
        {
            this.Invoke(new MethodInvoker(delegate
            {
                try
                {
                    mapInfo.RunMapBasicCommand(command);
                }
                catch { }
            }));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                    return true;
                case Keys.Shift | Keys.Right:
                case Keys.Shift | Keys.Left:
                case Keys.Shift | Keys.Up:
                case Keys.Shift | Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left:
                    keyleft = true;
                    break;
                case Keys.Right:
                    keyright = true;
                    break;
                case Keys.Up:
                    keyup = true;
                    break;
                case Keys.Down:
                    keydown = true;
                    break;
                case Keys.ControlKey:
                    System.Diagnostics.Debug.WriteLine("Boom!");
//                    SendMessage("/f");
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left:
                    keyleft = false;
                    break;
                case Keys.Right:
                    keyright = false;
                    break;
                case Keys.Up:
                    keyup = false;
                    break;
                case Keys.Down:
                    keydown = false;
                    break;
                case Keys.ControlKey:
//                    System.Diagnostics.Debug.WriteLine("Boom!");
//                    SendMessage("/f");
                    break;
            }
        }

        private void SendMapBasic(string text)
        {
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(text);
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        private void CreatePlayerTable(string name)
        {
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            labelName.Text = username;
            disableMB = !checkBoxAcceptMB.Checked;
            //CreatePlayerTable(username);

            try
            {
                clientSocket.Connect("127.0.0.1", 8066);
                serverStream = clientSocket.GetStream();
                SendMessage("/n " + username);
                Thread ctThread = new Thread(getMessage);
                ctThread.Start();
            }
            catch
            {
                MessageBox.Show("Unable to connect to server. Please try again!");
            }
        }

        private void checkBoxAcceptMB_CheckedChanged(object sender, EventArgs e)
        {
            disableMB = !((CheckBox)sender).Checked;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SendMessage("/quit");
            mapInfo.RunMapBasicCommand("END MAPINFO");
        }
       
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (keyleft)
            {
                System.Diagnostics.Debug.WriteLine("left");
                SendMessage("/l");
                tankAngle += rotation;
                RunMBCommand("Update " + username + " set obj = Rotate(obj, " + rotation + ")");
            }
            if (keyright)
            {
                System.Diagnostics.Debug.WriteLine("right");
                SendMessage("/r");
                tankAngle -= rotation;
                RunMBCommand("Update " + username + " set obj = Rotate(obj, -" + rotation + ")");
            }
            if (keyup)
            {
                System.Diagnostics.Debug.WriteLine("up");
                SendMessage("/u");
                RunMBCommand("Update " + username + " set obj = CartesianOffset(OBJ, " + (tankAngle + 90) + ", " + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")");
            }
            if (keydown)
            {
                System.Diagnostics.Debug.WriteLine("down");
                SendMessage("/d");
                RunMBCommand("Update " + username + " set obj = CartesianOffset(OBJ, " + (tankAngle + 90) + ", -" + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")");
            }

        }
    }
}
