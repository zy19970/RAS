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




        public ConfigForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 从ini文件读取变量
        /// </summary>
        void ReadFromFile()
        {
            comboMCUCom.Text = ConfigFile.IniReadValue("serialport", "MCUserial");
            comboM8128Com.Text = ConfigFile.IniReadValue("serialport", "M8128serial");
        }

        /// <summary>
        /// 将变量写入ini文件
        /// </summary>
        void Save2File()
        {
            ConfigFile.IniWriteValue("serialport", "M8128serial", comboM8128Com.Text);
            ConfigFile.IniWriteValue("serialport", "MCUserial", comboMCUCom.Text);
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
