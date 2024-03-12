using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RAS
{
    public partial class MainForm : Form
    {
        /********定义电机CAN驱动*************************/
        static CANalystHelper Cnh = new CANalystHelper();
        HDTDriver HDTX = new HDTDriver("00000601", Cnh);
        HDTDriver HDTY = new HDTDriver("00000602", Cnh);
        HDTDriver HDTZ = new HDTDriver("00000603", Cnh);
        /************************************************/





        public MainForm()
        {
            InitializeComponent();
        }

        private void button17_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void button25_Click(object sender, EventArgs e)
        {

        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void open_configform_button_Click(object sender, EventArgs e)
        {
            this.Hide();
            ConfigForm CfgForm = new ConfigForm();
            CfgForm.ShowDialog();
            //此处添加代码，配置完毕重读重要变量
            this.Show();
        }
    }
}
