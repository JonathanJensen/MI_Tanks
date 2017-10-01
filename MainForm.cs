using MapInfo.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MI_Tanks
{
    public partial class MainForm : Form
    {
        private IMapInfoPro mapInfo;
        private float tankAngle = 0;
        private string username = null;
        private int lastUsedID = -1;

        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream = default(NetworkStream);
        string readData = null;

        private void SendMessage(string message)
        {
            Debug.WriteLine(message);
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(message + "\n");
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        public MainForm(IMapInfoPro mapInfo, string username)
        {
            this.mapInfo = mapInfo;
            this.username = username;
            InitializeComponent();
            labelName.Text = username;
            //CreatePlayerTable(username);

            try
            {
                clientSocket.Connect("127.0.0.1", 8066);
                serverStream = clientSocket.GetStream();
                SendMessage("/name " + username);
                Thread ctThread = new Thread(getMessage);
                ctThread.Start();
            }
            catch
            {
                MessageBox.Show("Unable to connect to server. Please try again!");
            }
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
                    string ln = line.Trim();
                    if (!string.IsNullOrEmpty(ln))
                    {
                        try
                        {
                            mapInfo.RunMapBasicCommand(ln);
                        }
                        catch { }
                    }
                }
            }
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
                    System.Diagnostics.Debug.WriteLine("left");
                    SendMessage("/left");
                    break;
                case Keys.Right:
                    System.Diagnostics.Debug.WriteLine("right");
                    SendMessage("/right");
                    break;
                case Keys.Up:
                    System.Diagnostics.Debug.WriteLine("up");
                    SendMessage("/up");
                    break;
                case Keys.Down:
                    System.Diagnostics.Debug.WriteLine("down");
                    SendMessage("/down");
                    break;
                case Keys.Space:
                    System.Diagnostics.Debug.WriteLine("Boom!");
                    SendMessage("/fire");
                    break;
            }
        }

        private void SendMapBasic(string text)
        {
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(text);
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        private void RotateTank(int id, float angle)
        {
            string command = $"Update {username} set obj = Rotate(obj, {angle})";
            SendMessage("/left");
            //            tankAngle += angle;
            //            labelAngle.Text = tankAngle.ToString();
        }

        private void MoveTank(int id, int speed)
        {
            string command = "Update {username} set obj = CartesianOffset(OBJ, {tankAngle+90}, 0.5, \"m\")";
            SendMessage(command);
        }

        private void CreatePlayerTable(string name)
        {
        }
    }
}
