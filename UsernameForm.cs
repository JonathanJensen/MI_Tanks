using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MapInfo.Types;


namespace MI_Tanks
{
    public partial class UsernameForm : Form
    {
        private IMapInfoPro mapInfo;
        private IMapBasicApplication mapbasicApplication;

        public UsernameForm(IMapInfoPro mapInfo, IMapBasicApplication mbApp)
        {
            this.mapInfo = mapInfo;
            this.mapbasicApplication = mbApp;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.All(Char.IsLetter))
            {
                MainForm mainForm = new MainForm(mapInfo, mapbasicApplication, textBox1.Text);
                this.Close();
                mainForm.Show();
            }
            else
                MessageBox.Show("Only letters allowed, no numbers or spaces");

        }

        private void UsernameForm_Load(object sender, EventArgs e)
        {
            textBox1.Focus();
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                this.Close();
        }
    }
}
