using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RAS
{
    public partial class ConfigForm : Form
    {
        IniHelper ConfigFile = new IniHelper(@".\Config.ini");//定义ini读取类

        public bool isFileChanged = false;  //修改标志位

        public string STM32COM = "COM25";   //定义MCU串口号
        public string M8128COM = "COM26";   //定义六维力矩串口号
        public int Baud2COM = 115200;       //定义波特率

        public int RomLimitBeishen = 10;    //定义背伸运动极限
        public int RomLimitZhiqu = 10;      //定义跖屈运动极限
        public int RomLimitNeifan = 10;     //定义内翻运动极限
        public int RomLimitWaifan = 10;     //定义外翻运动极限
        public int RomLimitNeishou = 10;    //定义内收运动极限
        public int RomLimitWaizhan = 10;    //定义外展运动极限




        public ConfigForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 从ini文件读取变量
        /// </summary>
        void ReadFromFile()
        {
            /********读取串口变量*************************/
            comboMCUCom.Text = ConfigFile.IniReadValue("serialport", "MCUserial");
            comboM8128Com.Text = ConfigFile.IniReadValue("serialport", "M8128serial");

            /********读取运动范围变量*************************/
            textBoxBeishen.Text = ConfigFile.IniReadValue("ROM", "RomLimitBeishen");
            textBoxZhiqu.Text = ConfigFile.IniReadValue("ROM", "RomLimitZhiqu");
            textBoxNeifan.Text = ConfigFile.IniReadValue("ROM", "RomLimitNeifan");
            textBoxWaifan.Text = ConfigFile.IniReadValue("ROM", "RomLimitWaifan");
            textBoxNeishou.Text = ConfigFile.IniReadValue("ROM", "RomLimitNeishou");
            textBoxWaizhan.Text = ConfigFile.IniReadValue("ROM", "RomLimitWaizhan");
        }

        /// <summary>
        /// 将变量写入ini文件
        /// </summary>
        void Save2File()
        {
            /********写入串口变量*************************/
            ConfigFile.IniWriteValue("serialport", "M8128serial", comboM8128Com.Text);
            ConfigFile.IniWriteValue("serialport", "MCUserial", comboMCUCom.Text);

            /********写入运动范围变量*************************/
            ConfigFile.IniWriteValue("ROM", "RomLimitBeishen", textBoxBeishen.Text);
            ConfigFile.IniWriteValue("ROM", "RomLimitZhiqu", textBoxZhiqu.Text);
            ConfigFile.IniWriteValue("ROM", "RomLimitNeifan", textBoxNeifan.Text);
            ConfigFile.IniWriteValue("ROM", "RomLimitWaifan", textBoxWaifan.Text);
            ConfigFile.IniWriteValue("ROM", "RomLimitNeishou", textBoxNeishou.Text);
            ConfigFile.IniWriteValue("ROM", "RomLimitWaizhan", textBoxWaizhan.Text);
        }

        /// <summary>
        /// 扫描串口添加到Combobox
        /// </summary>
        /// <param name="MyPort">串口类</param>
        /// <param name="MyBox">ComboBox名称</param>
        private void SearchAndAddSerialToComboBox(SerialPort MyPort, ComboBox MyBox)
        {
            MyBox.Items.Clear();

            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                MyBox.Items.Add(port);
            }

        }
        /// <summary>
        /// 串口扫描函数
        /// </summary>
        private void ScanSerialPort()
        {
            SerialPort STMSerial = new SerialPort();//定义与MCU通信的串口
            SerialPort M8128Serial = new SerialPort();//定义与采集卡通讯的串口

            SearchAndAddSerialToComboBox(STMSerial, comboMCUCom);
            SearchAndAddSerialToComboBox(M8128Serial, comboM8128Com);
        }



        private void Save_button_Click(object sender, EventArgs e)
        {
            //保存到文件
            Save2File();

            isFileChanged = true;

            //保存到变量
            STM32COM = ConfigFile.IniReadValue("serialport", "MCUserial");
            M8128COM = ConfigFile.IniReadValue("serialport", "M8128serial");


            this.Close();
        }

        private void Cancel_button_Click(object sender, EventArgs e)
        {
            isFileChanged = false;
            this.Close();
        }

        private void ConfigForm_Load(object sender, EventArgs e)
        {
            isFileChanged = false;

            Thread childThread = new Thread(ScanSerialPort);
            childThread.Start();

            ReadFromFile();

        }
    }
}
