using MapInfo.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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

        public MainForm(IMapInfoPro mapInfo, string username)
        {
            this.mapInfo = mapInfo;
            this.username = username;
            InitializeComponent();
            labelName.Text = username;
            CreatePlayerTable(username);
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
                    RotateTank(0, 30);
                    break;
                case Keys.Right:
                    System.Diagnostics.Debug.WriteLine("right");
                    RotateTank(0, -30);
                    break;
                case Keys.Up:
                    System.Diagnostics.Debug.WriteLine("up");
                    MoveTank(0, 5);
                    break;
                case Keys.Down:
                    System.Diagnostics.Debug.WriteLine("down");
                    MoveTank(0, -5);
                    break;
                case Keys.Space:
                    System.Diagnostics.Debug.WriteLine("Boom!");
                    break;
            }
        }

        private void RotateTank(int id, float angle)
        {
            mapInfo.RunMapBasicCommand($"Update {username} set obj = Rotate(obj, {angle})");
            tankAngle += angle;
            labelAngle.Text = tankAngle.ToString();
        }

        private void MoveTank(int id, int speed)
        {
            mapInfo.RunMapBasicCommand($"Update {username} set obj = CartesianOffset(OBJ, {tankAngle+90}, {speed}, \"m\")");
        }

        private void CreatePlayerTable(string name)
        {
            lastUsedID++;
            mapInfo.RunMapBasicCommand($"select * from tanks into {username} where ID = {lastUsedID} noselect");
        }
    }
}
