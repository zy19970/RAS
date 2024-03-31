#define SERIALPORT_REV_HANDLER  //使用官方接口进行串口收发
//#define SERIALPORT_REV_PRIVATE //使用私有接口进行串口收发

//#define qDEBUG_ANGLE  //输出实时获取的位姿
//#define qDEBUG_TQ     //输出实时获取的力矩


using System;
using System.Collections;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace RAS
{
    public partial class MainForm : Form
    {
        /********定义电机CAN驱动*************************/
        #region 定义电机CAN驱动
        static CANalystHelper Cnh = new CANalystHelper();
        HDTDriver HDTX = new HDTDriver("00000601", Cnh);
        HDTDriver HDTY = new HDTDriver("00000602", Cnh);
        HDTDriver HDTZ = new HDTDriver("00000603", Cnh);
        #endregion
        /************************************************/

        /********定义机器人硬件信息结构体*************************/
        #region 定义机器人硬件信息结构体
        /// <summary>
        /// 六维力矩信息
        /// </summary>
        public struct M8128
        {
            public float forceX;//实际力
            public float torqueX;//实际力矩

            public float forceY;//实际力
            public float torqueY;//实际力矩

            public float forceZ;//实际力
            public float torqueZ;//实际力矩
        }
        /// <summary>
        /// 末端位姿信息
        /// </summary>
        public struct DeDetail
        {
            public float degreeX;//X方向实际角度
            public float degreeY;//Y方向实际角度
            public float degreeZ;//Z方向实际角度
        }
        #endregion
        /************************************************/


        /********定义串口相关类*************************/
        #region 定义串口相关类
        private string STM32COM;//MCU串口号
        private string M8128COM;//采集卡串口号
        private int Baud2COM = 115200;//串口通讯波特率

        public static SerialPort STMSerial = new SerialPort();//定义与MCU通信的串口
        public static SerialPort M8128Serial = new SerialPort();//定义与采集卡通讯的串口

        static byte[] M8128RevData = new byte[29];//定义采集卡接收的一帧的字节
        static byte[] STMRevData = new byte[14];//定义MCU接收的一帧的字节

        static Queue STMSendQueue = new Queue(104);//MCU发送队列
        static Queue M8128SendQueue = new Queue();//采集卡发送队列

        static Thread M8128SendMsgThread = new Thread(M8128SendMsg);//采集卡串口发送线程
        static Thread STMSendMsgThread = new Thread(STMSendMsg);//MCU串口发送线程

        static bool TorqeIsUpdate = false;//力矩更新标识符
        static bool AngleIsUpdate = false;//角度更新标识符

        public static Object M8128Lock = new Object();//采集卡线程间安全锁
        public static Object STMLock = new Object();//MCU线程安全锁

        static M8128 FTSensor = new M8128();//定义采集卡的力和力矩的数据结构
        static DeDetail DegreeSensor = new DeDetail();//定义编码器三个角度的数据结构

        static bool IsSTMRev = false;
        static Thread STMRevMsgThread = new Thread(STMRevStart);
        static Thread M8128RevMsgThread = new Thread(M8128RevStart);//MCU串口发送线程

        static bool IsConsoleError = false;//是否输出串口校验报错信息
        #endregion
        /************************************************/


        /********动态图表相关变量*************************/
        #region 动态图表相关变量
        static int NumOfPoint = 100;//曲线绘制点个数

        private Queue<double> XDegreedata = new Queue<double>(NumOfPoint);
        private Queue<double> YDegreedata = new Queue<double>(NumOfPoint);
        private Queue<double> ZDegreedata = new Queue<double>(NumOfPoint);

        static private Queue<double> XFdata = new Queue<double>(NumOfPoint);
        static private Queue<double> YFdata = new Queue<double>(NumOfPoint);
        static private Queue<double> ZFdata = new Queue<double>(NumOfPoint);

        static private Queue<double> XTdata = new Queue<double>(NumOfPoint);
        static private Queue<double> YTdata = new Queue<double>(NumOfPoint);
        static private Queue<double> ZTdata = new Queue<double>(NumOfPoint);

        public static Object ChartLock = new Object();//采集卡线程间安全锁

        bool IsPainting = false;
        #endregion
        /************************************************/

        /********消息队列相关集合*************************/
        #region 消息队列相关集合
        static private Queue DegreeXQueue = new Queue();
        static private Queue DegreeYQueue = new Queue();
        static private Queue DegreeZQueue = new Queue();
        static private Queue TorqueXQueue = new Queue();
        static private Queue TorqueYQueue = new Queue();
        static private Queue TorqueZQueue = new Queue();
        #endregion
        /************************************************/


        /********传感器标定相关参数*************************/
        #region 传感器标定相关参数
        static public float Offset_Fx = 55.701f;
        static public float Offset_Fy = -12.738f;
        static public float Offset_Fz = -52.249f;
        static public float Offset_Tx = -1.227f;
        static public float Offset_Ty = -0.677f;
        static public float Offset_Tz = 0.615f;


        static public float Offset_Dx = -3.566f;
        static public float Offset_Dy = 4.203f;
        static public float Offset_Dz = 4.071f;
        #endregion
        /************************************************/

        /********指示灯相关类*************************/
        #region 指示灯相关类
        LED CanLED = new LED();
        LED McuLED = new LED();
        LED TqLED = new LED();
        LED TrainLED = new LED();
        #endregion
        /************************************************/

        /********配置文件读写相关类*************************/
        #region 配置文件读写相关类
        IniHelper ConfigFile = new IniHelper(@".\Config.ini");//定义ini读取类
        #endregion
        /************************************************/

        /********配置实时日志相关类*************************/
        #region 配置实时日志相关类
        LogListUI LogUI = new LogListUI();//定义实时日志类
        #endregion
        /************************************************/

        public MainForm()
        {
            InitializeComponent();
            LogInit();
            LEDInit();
            SetialPortInit();
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "初始化", "结束..........");
        }

        #region 初始化操作
        /// <summary>
        /// LED指示控件初始化
        /// </summary>
        void LEDInit()
        {
            CanLED.SetPicBox(CAN_LED_PicBox);
            McuLED.SetPicBox(MCU_LED_PicBox);
            TqLED.SetPicBox(Tq_LED_PicBox) ;
            TrainLED.SetPicBox(Train_LED_PicBox);

            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "初始化", "LED控件", "初始化成功");
        }
        /// <summary>
        /// Log UI控件初始化
        /// </summary>
        void LogInit()
        {
            LogUI.SetListBox(LogListBox);
        }
        #endregion

        #region 串口操作
        /// <summary>
        /// 串口初始化，包含从ini文件读取变量
        /// </summary>
        private void SetialPortInit()
        {
            STMSerial.DataReceived += new SerialDataReceivedEventHandler(STMDataReceivedHandler);
            M8128Serial.DataReceived += new SerialDataReceivedEventHandler(M8128DataReceivedHandler);

            /********读取串口变量*************************/
            STM32COM = ConfigFile.IniReadValue("serialport", "MCUserial");
            M8128COM = ConfigFile.IniReadValue("serialport", "M8128serial");

            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "初始化", "串口", "单片机串口为"+ STM32COM);
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "初始化", "串口", "力矩传感器串口为" + M8128COM);
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "初始化", "串口", "波特率为" + Baud2COM);

        }
        /// <summary>
        /// 串口连接
        /// </summary>
        private void SerialPortConnect()
        {
            try
            {
                STMSerial.PortName = STM32COM;
                STMSerial.BaudRate = Baud2COM;

                M8128Serial.PortName = M8128COM;
                M8128Serial.BaudRate = Baud2COM;

                STMSerial.Open();
                if (!STMSendMsgThread.IsAlive)
                {
                    STMSendMsgThread = new Thread(STMSendMsg);
                    STMSendMsgThread.Start();
                }
#if !SERIALPORT_REV_HANDLER
                if (!STMRevMsgThread.IsAlive)
                {
                    STMRevMsgThread = new Thread(STMRevStart);
                    STMRevMsgThread.Start();
                }
#endif


                M8128Serial.Open();
                if (!M8128SendMsgThread.IsAlive)
                {
                    M8128SendMsgThread = new Thread(M8128SendMsg);
                    M8128SendMsgThread.Start();
                }
#if !SERIALPORT_REV_HANDLER
                if (!M8128RevMsgThread.IsAlive)
                {
                    M8128RevMsgThread = new Thread(M8128RevStart);
                    M8128RevMsgThread.Start();
                }
#endif
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 断开连接
        /// </summary>
        private void SerialPortDisconnect()
        {
            try
            {

                if (!STMSendMsgThread.IsAlive)
                {
                    STMSendMsgThread.Abort();
                    Thread.Sleep(200);
                }

                STMSerial.Close();


                if (!M8128SendMsgThread.IsAlive)
                {
                    M8128SendMsgThread.Abort();
                    Thread.Sleep(200);
                }
                M8128Serial.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// PC to 采集卡，发送M8128SendQueue中的信息
        /// </summary>
        public static void M8128SendMsg()
        {
            while (true)
            {
                if (M8128SendQueue.Count > 0)
                {
                    byte[] send_data = (byte[])M8128SendQueue.Dequeue();
                    if (send_data != null && M8128Serial.IsOpen)
                    {
                        try { M8128Serial.Write(send_data, 0, send_data.Length); }
                        catch { }

                    }

                }
                Thread.Sleep(2);
            }

        }

        /// <summary>
        /// PC to MCU，发送STMSendQueue中的信息
        /// </summary>
        public static void STMSendMsg()
        {
            while (STMSerial.IsOpen)
            {
                if (STMSendQueue.Count > 0)
                {
                    byte[] send_data = (byte[])STMSendQueue.Dequeue();
                    if (send_data != null && STMSerial.IsOpen)
                    {
                        try { STMSerial.Write(send_data, 0, send_data.Length); }
                        catch { }
                    }
                }
                Thread.Sleep(2);
            }
            if (!STMSerial.IsOpen)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }

        }

        /// <summary>
        /// 官方注册的MCU串口接收线程
        /// </summary>
        private static void STMDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            AngleIsUpdate = false;
            STMRevData = new byte[14];
            SerialPort sp = (SerialPort)sender;

            if (sp.BytesToRead <= 0){return;}

            bool FirstIsOk = false;
            bool HeadIsOK = false;

            int RevNum = 0;

            for (; RevNum < 14;)
            {
                byte RevData = 0x00;
                if (!sp.IsOpen) { return; }

                try { RevData = Convert.ToByte(sp.ReadByte()); }
                catch { return; }



                if (HeadIsOK)
                {
                    STMRevData[RevNum] = RevData;
                    RevNum++;
                }
                if (!FirstIsOk && RevData == 0x11)
                {
                    FirstIsOk = true;
                }
                if (FirstIsOk && RevData == 0xFF && !HeadIsOK)
                {
                    HeadIsOK = true;
                }
            }
            lock (STMLock)
            {
                DegreeSensor.degreeX = BitConverter.ToSingle(STMRevData, 1);
                DegreeXQueue.Enqueue(DegreeSensor.degreeX);
                DegreeSensor.degreeY = BitConverter.ToSingle(STMRevData, 5);
                DegreeYQueue.Enqueue(DegreeSensor.degreeY);
                DegreeSensor.degreeZ = BitConverter.ToSingle(STMRevData, 9);
                DegreeZQueue.Enqueue(DegreeSensor.degreeZ);
                AngleIsUpdate = true;
            }
#if DEBUG && qDEBUG_ANGLE
            Console.WriteLine("DegreeX=" + DegreeSensor.degreeX
                + "DegreeY=" + DegreeSensor.degreeY
                + "DegreeZ=" + DegreeSensor.degreeZ
            );
#endif
        }

        /// <summary>
        /// [已弃用]私有实现，MCU串口接收线程
        /// </summary>
        private static void STMRevThd()
        {
            SerialPort sp = (SerialPort)STMSerial;
            if (!M8128Serial.IsOpen) { return; }
            if (sp.BytesToRead <= 0) { return; }

            byte RevData = 0x00;

            try { RevData = Convert.ToByte(sp.ReadByte()); }
            catch { return; }
            if (RevData != 0x11) { return; }


            bool HeadIsOK = false;
            AngleIsUpdate = false;
            STMRevData = new byte[14];
            int RevNum = 0;


            for (; RevNum < 14;)
            {
                if (!sp.IsOpen) { return; }

                try { RevData = Convert.ToByte(sp.ReadByte()); }
                catch { return; }



                if (HeadIsOK)
                {
                    STMRevData[RevNum] = RevData;
                    RevNum++;
                }
                if (RevData == 0xFF)
                {
                    HeadIsOK = true;
                }
            }
            lock (STMLock)
            {
                DegreeSensor.degreeX = BitConverter.ToSingle(STMRevData, 1) + Offset_Dx;
                DegreeXQueue.Enqueue(DegreeSensor.degreeX);
                DegreeSensor.degreeY = BitConverter.ToSingle(STMRevData, 5) + Offset_Dy;
                DegreeYQueue.Enqueue(-DegreeSensor.degreeY);
                DegreeSensor.degreeZ = BitConverter.ToSingle(STMRevData, 9) + Offset_Dz;
                DegreeZQueue.Enqueue(-DegreeSensor.degreeZ);
                AngleIsUpdate = true;
            }
            //Console.WriteLine("DegreeX=" + DegreeSensor.degreeX
            //    + "DegreeY=" + DegreeSensor.degreeY
            //    + "DegreeZ=" + DegreeSensor.degreeZ);
        }
        /// <summary>
        /// [已弃用]私有实现，MCU串口串口接收线程启动入口
        /// </summary>
        private static void STMRevStart()
        {
            while (STMSerial.IsOpen)
            {
                Thread mythread = new Thread(STMRevThd);
                mythread.Start();
                Thread.Sleep(80);
            }
            if (!STMSerial.IsOpen)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
        /// <summary>
        /// 官方注册的M8128串口接收线程
        /// </summary>
        private static void M8128DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            TorqeIsUpdate = false;
            M8128RevData = new byte[29];
            SerialPort sp = (SerialPort)sender;

            if (!M8128Serial.IsOpen) { return; }

            if (sp.BytesToRead <= 0) { return; }


            int RevNum = 0;
            byte RevData = 0x00;

            try { RevData = Convert.ToByte(sp.ReadByte()); }
            catch { return; }

            if (RevData != 0xAA) { return; }
            bool HeadIsOK = false;

            for (; RevNum < 29;)
            {

                if (!sp.IsOpen) { return; }

                try { RevData = Convert.ToByte(sp.ReadByte()); }
                catch { return; }


                if (HeadIsOK)
                {
                    M8128RevData[RevNum] = RevData;
                    RevNum++;
                }
                if (RevData == 0x55)
                {
                    HeadIsOK = true;
                }

            }




            if (M8128RevData == null) { return; }
            try
            {
                if (M8128RevData[0] == 0x00 && M8128RevData[1] == 0x1b) { } else { return; }

                if (!StringHelper.IsSUM(M8128RevData))
                {
                    Console.WriteLine(DateTime.Now.ToLocalTime().ToString() + "\t校验失败。");
                    return;
                }

                if ((BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx) < -5000)
                {
                    Console.WriteLine("Fx:{0}", BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx);
                }
                else
                { FTSensor.forceX = BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx; }
                if ((BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx) < -5000)
                {
                    Console.WriteLine("Tx:{0}", BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx);
                }
                else
                { FTSensor.torqueX = BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx; }


                if ((BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy) < -5000)
                {
                    Console.WriteLine("Fy:{0}", BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy);
                }
                else
                { FTSensor.forceY = BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy; }
                if ((BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty) < -5000)
                {
                    Console.WriteLine("Ty:{0}", BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty);
                }
                else
                { FTSensor.torqueY = BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty; }


                if ((BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz) < -5000)
                {
                    Console.WriteLine("Fz:{0}", BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz);
                }
                else
                { FTSensor.forceZ = BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz; }
                if ((BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz) < -5000)
                {
                    Console.WriteLine("Tz:{0}", BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz);
                }
                else
                { FTSensor.torqueZ = BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz; }

                lock (M8128Lock)
                {
                    TorqueXQueue.Enqueue(FTSensor.torqueX);
                    TorqueYQueue.Enqueue(FTSensor.torqueY);
                    TorqueZQueue.Enqueue(FTSensor.torqueZ);
                    TorqeIsUpdate = true;
#if DEBUG && qDEBUG_TQ
                    Console.WriteLine("Fx=" + FTSensor.forceX + "\tTx=" + FTSensor.torqueX
                        + "\tFy=" + FTSensor.forceY + "\tTy=" + FTSensor.torqueY
                        + "\tFz=" + FTSensor.forceZ + "\tTz=" + FTSensor.torqueZ
                        );
#endif
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }

            //Thread.Sleep(1);

        }
        /// <summary>
        /// [已弃用]私有实现，M8128串口接收线程
        /// </summary>
        private static void M8128RevThd()
        {

            SerialPort sp = (SerialPort)M8128Serial;

            if (!M8128Serial.IsOpen) { return; }
            if (sp.BytesToRead <= 0) { return; }

            byte RevData = 0x00;

            try { RevData = Convert.ToByte(sp.ReadByte()); }
            catch { return; }
            if (RevData != 0xAA) { return; }

            bool HeadIsOK = false;
            TorqeIsUpdate = false;
            M8128RevData = new byte[29];
            int RevNum = 0;

            for (; RevNum < 29;)
            {

                if (!sp.IsOpen) { return; }

                try { RevData = Convert.ToByte(sp.ReadByte()); }
                catch { return; }


                if (HeadIsOK)
                {
                    M8128RevData[RevNum] = RevData;
                    RevNum++;
                }
                if (RevData == 0x55)
                {
                    HeadIsOK = true;
                }

            }


            if (M8128RevData == null) { return; }
            try
            {
                if (M8128RevData[0] == 0x00 && M8128RevData[1] == 0x1b) { } else { return; }

                if (!StringHelper.IsSUM(M8128RevData))
                {
                    if (IsConsoleError) Console.WriteLine(DateTime.Now.ToLocalTime().ToString() + "\t校验失败。");
                    return;
                }

                if ((BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx) < -5000)
                {
                    Console.WriteLine("Fx:{0}", BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx);
                }
                else
                { FTSensor.forceX = BitConverter.ToSingle(M8128RevData, 4) + Offset_Fx; }
                if ((BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx) < -5000)
                {
                    Console.WriteLine("Tx:{0}", BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx);
                }
                else
                { FTSensor.torqueX = BitConverter.ToSingle(M8128RevData, 16) + Offset_Tx; }


                if ((BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy) < -5000)
                {
                    Console.WriteLine("Fy:{0}", BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy);
                }
                else
                { FTSensor.forceY = BitConverter.ToSingle(M8128RevData, 8) + Offset_Fy; }
                if ((BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty) < -5000)
                {
                    Console.WriteLine("Ty:{0}", BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty);
                }
                else
                { FTSensor.torqueY = BitConverter.ToSingle(M8128RevData, 20) + Offset_Ty; }


                if ((BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz) < -5000)
                {
                    Console.WriteLine("Fz:{0}", BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz);
                }
                else
                { FTSensor.forceZ = BitConverter.ToSingle(M8128RevData, 12) + Offset_Fz; }
                if ((BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz) > 5000.0f || (BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz) < -5000)
                {
                    Console.WriteLine("Tz:{0}", BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz);
                }
                else
                { FTSensor.torqueZ = BitConverter.ToSingle(M8128RevData, 24) + Offset_Tz; }

                lock (M8128Lock)
                {
                    TorqueXQueue.Enqueue(FTSensor.torqueX);
                    TorqueYQueue.Enqueue(FTSensor.torqueY);
                    TorqueZQueue.Enqueue(FTSensor.torqueZ);
                    TorqeIsUpdate = true;
#if DEBUG
                    Console.WriteLine("Fx=" + FTSensor.forceX + "\tTx=" + FTSensor.torqueX
                        + "\tFy=" + FTSensor.forceY + "\tTy=" + FTSensor.torqueY
                        + "\tFz=" + FTSensor.forceZ + "\tTz=" + FTSensor.torqueZ
                        );
#endif
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// [已弃用]私有实现，M8128串口接收线程启动入口
        /// </summary>
        private static void M8128RevStart()
        {
            while (M8128Serial.IsOpen)
            {
                Thread mythread = new Thread(M8128RevThd);
                mythread.Start();
                Thread.Sleep(10);
            }
            if (!M8128Serial.IsOpen)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }

        /// <summary>
        /// 初始化采集卡，使能其发送
        /// </summary>
        private void M8218MqInit()
        {
            string strCmd = "AT+GETSD=STOP\r\n";
            byte[] byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);

            Thread.Sleep(50);

            strCmd = "AT+SGDM=(A01,A02,A03,A04,A05,A06);E;1;(WMA:1,1,1,2,4)\r\n";
            byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);

            Thread.Sleep(50);

            strCmd = "AT+DCKMD=SUM\r\n";
            byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);

            Thread.Sleep(50);

            strCmd = "AT+SMPR=85\r\n";
            byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);

            Thread.Sleep(50);

            strCmd = "AT+GSD\r\n";
            byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);
        }
        /// <summary>
        /// M8128停止发送命令
        /// </summary>
        private void M8218MqStop()
        {
            string strCmd = "AT+GETSD=STOP\r\n";
            byte[] byteCmd = Encoding.Default.GetBytes(strCmd);
            M8128SendQueue.Enqueue(byteCmd);
        }
        #endregion

        #region 力矩平衡操作
        private void Tq_Sensor_GoToZeroButton_Click(object sender, EventArgs e)
        {
            Thread childThread = new Thread(OffSet);
            childThread.Start();
        }
        /// <summary>
        /// 实现力矩平衡函数
        /// </summary>
        private void OffSet()
        {
            pictureBox4.Visible = false;
            Tq_Sensor_GoToZeroButton.Enabled = false;
            Offset_Fx = 0;
            Offset_Fy = 0;
            Offset_Fz = 0;
            Offset_Tx = 0;
            Offset_Ty = 0;
            Offset_Tz = 0;
            Tq_Sensor_ProgressBar.Value = 0;
            float result1 = 0, result2 = 0, result3 = 0, result4 = 0, result5 = 0, result6 = 0;

            for (int i = 0; i < 100; i++)
            {
                result1 += FTSensor.forceX;
                result2 += FTSensor.torqueX;
                result3 += FTSensor.forceY;
                result4 += FTSensor.torqueY;
                result5 += FTSensor.forceZ;
                result6 += FTSensor.torqueZ;
                Tq_Sensor_ProgressBar.Value = i + 1;
                Thread.Sleep(40);
            }
            Offset_Fx = -result1 / 100.0f;
            Offset_Fy = -result3 / 100.0f;
            Offset_Fz = -result5 / 100.0f;
            Offset_Tx = -result2 / 100.0f;
            Offset_Ty = -result4 / 100.0f;
            Offset_Tz = -result6 / 100.0f;

            Thread.Sleep(800);

            pictureBox4.Visible = true;

            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "", "", "Fx：" + Offset_Fx.ToString("0.000") + "；Fy：" + Offset_Fy.ToString("0.000") + "；Fz：" + Offset_Fz.ToString("0.000"));
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "", "", "Tx：" + Offset_Tx.ToString("0.000") + "；Ty：" + Offset_Ty.ToString("0.000") + "；Tz：" + Offset_Tz.ToString("0.000"));

            //弹窗显示力矩偏执
            // MessageBox.Show("校准完成！Fx偏置为：" + Offset_Fx.ToString("0.000") + "；Tx的偏置为：" + Offset_Tx.ToString("0.000") + "；Fy的偏置为：" + Offset_Fy.ToString("0.000")+ "；Ty的偏置为：" + Offset_Ty.ToString("0.000") + "；Fz的偏置为：" + Offset_Fz.ToString("0.000") + "；Tz的偏置为：" + Offset_Tz.ToString("0.000") + ".", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Tq_Sensor_GoToZeroButton.Enabled = true;

        }
        #endregion




        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// 唤起配置页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void open_configform_button_Click(object sender, EventArgs e)
        {
            this.Hide();
            ConfigForm CfgForm = new ConfigForm();
            CfgForm.ShowDialog();
            this.Show();


            if (!CfgForm.isFileChanged)
            {
                Console.WriteLine("配置文件未修改.");
                return;
            }

            //此处添加代码，配置完毕重读重要变量
            STM32COM = CfgForm.STM32COM;
            M8128COM = CfgForm.M8128COM;

        }
        /// <summary>
        /// 连接按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButton_Click(object sender, EventArgs e)
        {
            SerialPortConnect();
            try
            {
                if (STMSendMsgThread.IsAlive && STMSerial.IsOpen)
                {
                    McuLED.On();
                    if (M8128SendMsgThread.IsAlive && M8128Serial.IsOpen)
                    {
                        TqLED.On();
                    }
                    else
                    {
                        TqLED.Error();
                        return;
                    }
                }
                else
                {
                    McuLED.Error();
                    return;
                }

                if (Cnh.Strat()) { CanLED.On(); }
                else { CanLED.Error(); }

                M8218MqInit();
                Thread.Sleep(20);

                //PaintStrat();-------绘制功能

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
           
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "测试", "写入操作", "无细节");
        }

        /// <summary>
        /// 配置手动调整UI使能按钮
        /// </summary>
        private void checkBox_Main_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked) { panel2.Enabled = true; }
            else { panel2.Enabled = false; }

            if (checkBox17.Checked) { panel9.Enabled = true; }
            else { panel9.Enabled = false; }

            if (checkBox3.Checked) { panel4.Enabled = true; }
            else { panel4.Enabled = false; }

            if (checkBox4.Checked) { panel5.Enabled = true; }
            else { panel5.Enabled = false; }

            if (checkBox5.Checked) { panel6.Enabled = true; }
            else { panel6.Enabled = false; }
        }

    }
}
