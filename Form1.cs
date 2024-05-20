#define SERIALPORT_REV_HANDLER  //使用官方接口进行串口收发
//#define SERIALPORT_REV_PRIVATE //使用私有接口进行串口收发

//#define qDEBUG_ANGLE  //输出实时获取的位姿
//#define qDEBUG_TQ     //输出实时获取的力矩

//#define ENCODER_OFFSET //使能编码器标定


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
using System.Windows.Forms.DataVisualization.Charting;
using static RAS.TrainModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
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

        /********定义机器人传感器信息*************************/
        #region 定义机器人传感器信息
        public static M8128 FTSensor = new M8128();//定义采集卡的力和力矩的数据结构
        public static DeDetail DegreeSensor = new DeDetail();//定义编码器三个角度的数据结构

        public static DeDetail IdealDegreeSensor = new DeDetail();//定义下发到机器人的编码器角度
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


        static public float Offset_Dx = -3.954f;
        static public float Offset_Dy = 5.491f;
        static public float Offset_Dz = 1.456f;
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


        TrainModel trainModel = new TrainModel();//定义经典训练模式对象


        public MainForm()
        {
            InitializeComponent();
            LogInit();
            LEDInit();
            SetialPortInit();
            ChartInit();
            Manual_DriverInit();
            TrainModel_DriverInit();
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
                else if (!FirstIsOk && RevData == 0x11)
                {
                    FirstIsOk = true;
                }
                else if (FirstIsOk && RevData == 0xFF && !HeadIsOK)
                {
                    HeadIsOK = true;
                }
                else { return; }
            }
            lock (STMLock)
            {
                //DegreeSensor.degreeX = BitConverter.ToSingle(STMRevData, 1);
                //DegreeXQueue.Enqueue(DegreeSensor.degreeX);
                //DegreeSensor.degreeY = BitConverter.ToSingle(STMRevData, 5);
                //DegreeYQueue.Enqueue(DegreeSensor.degreeY);
                //DegreeSensor.degreeZ = BitConverter.ToSingle(STMRevData, 9);
                //DegreeZQueue.Enqueue(DegreeSensor.degreeZ);
                //AngleIsUpdate = true;
                DegreeSensor.degreeX = BitConverter.ToSingle(STMRevData, 1) + Offset_Dx;
                DegreeXQueue.Enqueue(DegreeSensor.degreeX);
                DegreeSensor.degreeY = BitConverter.ToSingle(STMRevData, 5) + Offset_Dy;
                DegreeYQueue.Enqueue(-DegreeSensor.degreeY);
                DegreeSensor.degreeZ = BitConverter.ToSingle(STMRevData, 9) + Offset_Dz;
                DegreeZQueue.Enqueue(-DegreeSensor.degreeZ);
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
        private void M8128DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
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
                else if (RevData == 0x55)
                {
                    HeadIsOK = true;
                }
                else { return; }

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
#if ENCODER_OFFSET
            Offset_Dx = 0;
            Offset_Dy = 0;
            Offset_Dz=0;
#endif
            Tq_Sensor_ProgressBar.Value = 0;
            float result1 = 0, result2 = 0, result3 = 0, result4 = 0, result5 = 0, result6 = 0;
#if ENCODER_OFFSET
            float a1 = 0;float a2 = 0;float a3 = 0;
#endif
            for (int i = 0; i < 100; i++)
            {
                result1 += FTSensor.forceX;
                result2 += FTSensor.torqueX;
                result3 += FTSensor.forceY;
                result4 += FTSensor.torqueY;
                result5 += FTSensor.forceZ;
                result6 += FTSensor.torqueZ;
#if ENCODER_OFFSET
                a1 += DegreeSensor.degreeX;
                a2 += DegreeSensor.degreeY;
                a3 += DegreeSensor.degreeZ;
#endif
                Tq_Sensor_ProgressBar.Value = i + 1;
                Thread.Sleep(40);
            }
            Offset_Fx = -result1 / 100.0f;
            Offset_Fy = -result3 / 100.0f;
            Offset_Fz = -result5 / 100.0f;
            Offset_Tx = -result2 / 100.0f;
            Offset_Ty = -result4 / 100.0f;
            Offset_Tz = -result6 / 100.0f;
#if ENCODER_OFFSET
            Offset_Dx = -a1 / 100.0f;
            Offset_Dy = -a2 / 100.0f;
            Offset_Dz = -a3 / 100.0f;
#endif
            Thread.Sleep(800);

            pictureBox4.Visible = true;

            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "", "", "Fx：" + Offset_Fx.ToString("0.000") + "；Fy：" + Offset_Fy.ToString("0.000") + "；Fz：" + Offset_Fz.ToString("0.000"));
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "", "", "Tx：" + Offset_Tx.ToString("0.000") + "；Ty：" + Offset_Ty.ToString("0.000") + "；Tz：" + Offset_Tz.ToString("0.000"));

            //弹窗显示力矩偏执
            // MessageBox.Show("校准完成！Fx偏置为：" + Offset_Fx.ToString("0.000") + "；Tx的偏置为：" + Offset_Tx.ToString("0.000") + "；Fy的偏置为：" + Offset_Fy.ToString("0.000")+ "；Ty的偏置为：" + Offset_Ty.ToString("0.000") + "；Fz的偏置为：" + Offset_Fz.ToString("0.000") + "；Tz的偏置为：" + Offset_Tz.ToString("0.000") + ".", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
#if ENCODER_OFFSET
            MessageBox.Show("校准完成！Ax偏置为：" + Offset_Dx.ToString("0.000") + "；Ay的偏置为：" + Offset_Dy.ToString("0.000") + "；Az的偏置为：" + Offset_Dz.ToString("0.000")+".", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
#endif

            Tq_Sensor_GoToZeroButton.Enabled = true;

        }
#endregion

        #region 图表绘制
        /// <summary>
        /// 图表初始化函数
        /// </summary>
        private void ChartInit()
        {
            //AngleChart.Titles.Add("Real Time Angle Data");

            AngleChart.Series.Clear();

            Series seriesX = new Series("X");
            seriesX.ChartArea = "ChartArea1";
            seriesX.Color = System.Drawing.Color.Red;
            AngleChart.Series.Add(seriesX);

            Series seriesY = new Series("Y");
            seriesY.ChartArea = "ChartArea1";
            seriesY.Color = System.Drawing.Color.Purple;
            AngleChart.Series.Add(seriesY);

            Series seriesZ = new Series("Z");
            seriesZ.ChartArea = "ChartArea1";
            seriesZ.Color = System.Drawing.Color.Navy;
            AngleChart.Series.Add(seriesZ);

            for (int i = 0; i < 3; i++)
            {
                AngleChart.Series[i].ChartType = SeriesChartType.Spline;
                AngleChart.Series[i].BorderWidth = 2; //线条粗细
            }

            //添加的两组Test数据
            List<int> txData2 = new List<int>() { 1, 2, 3, 4, 5, 6 };
            List<int> tyData2 = new List<int>() { 9, 6, 7, 4, 5, 4 };
            List<int> txData3 = new List<int>() { 1, 2, 3, 4, 5, 6 };
            List<int> tyData3 = new List<int>() { 3, 8, 2, 5, 4, 9 };
            AngleChart.Series[0].Points.DataBindXY(txData2, tyData2); //添加数据
            AngleChart.Series[1].Points.DataBindXY(txData3, tyData3); //添加数据

            //M8128Chart.Titles.Add("Real Time Torque Data");

            M8128Chart.Series.Clear();

            Series seriesFX = new Series("Fx");
            seriesFX.ChartArea = "ChartArea1";
            seriesFX.Color = System.Drawing.Color.Red;
            M8128Chart.Series.Add(seriesFX);

            Series seriesFY = new Series("Fy");
            seriesFY.ChartArea = "ChartArea1";
            seriesFY.Color = System.Drawing.Color.Purple;
            M8128Chart.Series.Add(seriesFY);

            Series seriesFZ = new Series("Fz");
            seriesFZ.ChartArea = "ChartArea1";
            seriesFZ.Color = System.Drawing.Color.Navy;
            M8128Chart.Series.Add(seriesFZ);

            Series seriesTX = new Series("Tx");
            seriesTX.ChartArea = "ChartArea1";
            seriesTX.Color = System.Drawing.Color.Green;
            M8128Chart.Series.Add(seriesTX);

            Series seriesTY = new Series("Ty");
            seriesTY.ChartArea = "ChartArea1";
            seriesTY.Color = System.Drawing.Color.Fuchsia;
            M8128Chart.Series.Add(seriesTY);

            Series seriesTZ = new Series("Tz");
            seriesTZ.ChartArea = "ChartArea1";
            seriesTZ.Color = System.Drawing.Color.Orange;
            M8128Chart.Series.Add(seriesTZ);


            for (int i = 0; i < 6; i++)
            {
                M8128Chart.Series[i].ChartType = SeriesChartType.Spline;
                M8128Chart.Series[i].BorderWidth = 2; //线条粗细
            }

            M8128Chart.Series[4].Points.DataBindXY(txData2, tyData2); //添加数据
            M8128Chart.Series[5].Points.DataBindXY(txData3, tyData3); //添加数据

        }

        /// <summary>
        /// 角度曲线更新函数
        /// </summary>
        private void UpdateAngleQueue()
        {

            int realpoint = 0;
            if (AngleXcheckBox.Checked && XDegreedata.Count > realpoint) realpoint = XDegreedata.Count;
            if (AngleYcheckBox.Checked && YDegreedata.Count > realpoint) realpoint = YDegreedata.Count;
            if (AngleZcheckBox.Checked && ZDegreedata.Count > realpoint) realpoint = ZDegreedata.Count;

            lock (ChartLock)
            {
                if (realpoint > NumOfPoint)
                {
                    XDegreedata.Dequeue();
                    YDegreedata.Dequeue();
                    ZDegreedata.Dequeue();
                }


                lock (STMLock)
                {
                    XDegreedata.Enqueue(DegreeSensor.degreeX);
                    YDegreedata.Enqueue(DegreeSensor.degreeY);
                    //YDegreedata.Enqueue(RealDegree);
                    ZDegreedata.Enqueue(DegreeSensor.degreeZ);
                }

                for (int i = 0; i < 3; i++)
                {
                    AngleChart.Series[i].Points.Clear();
                }

                for (int i = 0; i < XDegreedata.Count; i++)
                {
                    if (AngleXcheckBox.Checked) AngleChart.Series[0].Points.AddXY((i + 1), XDegreedata.ElementAt(i));
                    if (AngleYcheckBox.Checked) AngleChart.Series[1].Points.AddXY((i + 1), YDegreedata.ElementAt(i));
                    if (AngleZcheckBox.Checked) AngleChart.Series[2].Points.AddXY((i + 1), ZDegreedata.ElementAt(i));
                }
            }
        }
        /// <summary>
        /// 角度曲线绘制函数
        /// </summary>
        private void PaintAngle()
        {



            AngleIsUpdate = false;

            for (int i = 0; i < 3; i++)
            {
                AngleChart.Series[i].Points.Clear();
            }
            lock (ChartLock)
            {
                List<double> a = new List<double>();
                a = XDegreedata.ToList();
                Queue<double> b = new Queue<double>(NumOfPoint);
                Queue<double> c = new Queue<double>(NumOfPoint);
                for (int i = 0; i < a.Count; i++)
                {
                    if (AngleXcheckBox.Checked) AngleChart.Series[0].Points.AddXY((i + 1), a.ElementAt(i));
                    //if (AngleYcheckBox.Checked) AngleChart.Series[1].Points.AddXY((i + 1), b.ElementAt(i));
                    //if (AngleZcheckBox.Checked) AngleChart.Series[2].Points.AddXY((i + 1), c.ElementAt(i));
                }
            }
        }
        /// <summary>
        /// 六维曲线更新函数
        /// </summary>
        private void UpdateTorqeQueue()
        {
            int realpoint = 0;
            if (FXcheckBox.Checked && XFdata.Count > realpoint) realpoint = XFdata.Count;
            if (FYcheckBox.Checked && YFdata.Count > realpoint) realpoint = YFdata.Count;
            if (FZcheckBox.Checked && ZFdata.Count > realpoint) realpoint = ZFdata.Count;
            if (TXcheckBox.Checked && XTdata.Count > realpoint) realpoint = XTdata.Count;
            if (TYcheckBox.Checked && YTdata.Count > realpoint) realpoint = YTdata.Count;
            if (TZcheckBox.Checked && ZTdata.Count > realpoint) realpoint = ZTdata.Count;

            if (realpoint > NumOfPoint)
            {
                XFdata.Dequeue();
                YFdata.Dequeue();
                ZFdata.Dequeue();
                XTdata.Dequeue();
                YTdata.Dequeue();
                ZTdata.Dequeue();
            }
            lock (M8128Lock)
            {
                XFdata.Enqueue(FTSensor.forceX);
                XTdata.Enqueue(FTSensor.torqueX);
                YFdata.Enqueue(FTSensor.forceY);
                YTdata.Enqueue(FTSensor.torqueY);
                ZFdata.Enqueue(FTSensor.forceZ);
                ZTdata.Enqueue(FTSensor.torqueZ);


            }
            PaintTorqe();

        }
        /// <summary>TXcheckBox
        /// 六维曲线绘制函数
        /// </summary>
        private void PaintTorqe()
        {
            TorqeIsUpdate = false;

            for (int i = 0; i < 6; i++)
            {
                M8128Chart.Series[i].Points.Clear();
            }
            for (int i = 0; i < XTdata.Count; i++)
            {
                if (FXcheckBox.Checked) M8128Chart.Series[0].Points.AddXY((i + 1), XFdata.ElementAt(i));

                if (FYcheckBox.Checked) M8128Chart.Series[1].Points.AddXY((i + 1), YFdata.ElementAt(i));

                if (FZcheckBox.Checked) M8128Chart.Series[2].Points.AddXY((i + 1), ZFdata.ElementAt(i));

                if (TXcheckBox.Checked) M8128Chart.Series[3].Points.AddXY((i + 1), XTdata.ElementAt(i));

                if (TYcheckBox.Checked) M8128Chart.Series[4].Points.AddXY((i + 1), YTdata.ElementAt(i));

                if (TZcheckBox.Checked) M8128Chart.Series[5].Points.AddXY((i + 1), ZTdata.ElementAt(i));
            }


        }
        /// <summary>
        /// 开始绘图
        /// </summary>
        private void PaintStrat()
        {
            IsPainting = true;
            //Thread mythread = new Thread(PaintThread);
            //mythread.Start();
            ChartTimer.Start();
        }
        /// <summary>
        /// 结束绘图
        /// </summary>
        private void PaintStop()
        {
            IsPainting = false;
            ChartTimer.Stop();
        }

        private void PaintThread()
        {
            while (IsPainting)
            {
                //if (AngleIsUpdate)
                //{
                UpdateAngleQueue();
                //Console.WriteLine("Angle is OK;");
                //}
                for (int i = 0; i < 50; i++)
                {
                    if (IsPainting)
                        Thread.Sleep(1);
                }
                if (TorqeIsUpdate)
                {
                    // UpdateTorqeQueue();
                    //Console.WriteLine("Tq is OK;");
                }
                for (int i = 0; i < 50; i++)
                {
                    if (IsPainting)
                        Thread.Sleep(1);
                }
            }
            if (!IsPainting)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }

        }
        private void ChartTimer_Tick(object sender, EventArgs e)
        {
            UpdateAngleQueue();
            UpdateTorqeQueue();
        }
        #endregion

        #region 文件存储
        FileHelper AngleFile;//角度存储类
        FileHelper FTFile;//力矩信息存储类

        int Timesample2SaveFIle = 100;//存储间隔
        bool IsSaveFile = false;//存储标志位

        /// <summary>
        /// 保存一条数据
        /// </summary>
        private void AddItem2File()
        {
            AngleFile.SaveOneItem(DegreeSensor.degreeX, DegreeSensor.degreeY, DegreeSensor.degreeZ, IdealDegreeSensor.degreeX, IdealDegreeSensor.degreeY, IdealDegreeSensor.degreeZ);
            FTFile.SaveOneItem(FTSensor.forceX, FTSensor.torqueX, FTSensor.forceY, FTSensor.torqueY, FTSensor.forceZ, FTSensor.torqueZ);
        }
        /// <summary>
        /// 保存线程
        /// </summary>
        private void Save_main()
        {
            while (IsSaveFile)
            {
                Thread childThread = new Thread(AddItem2File);
                childThread.Start();
                Thread.Sleep(Timesample2SaveFIle - 2);
            }
            if (!IsSaveFile)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
        /// <summary>
        /// 打开存储路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("Explorer.exe", Application.StartupPath + @"\Data");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            AngleFile = new FileHelper();
            FTFile = new FileHelper();

            string filePath = Application.StartupPath + @"\Data\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_Angle.txt";
            AngleFile.SetFilePath(filePath);
            filePath = Application.StartupPath + @"\Data\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "_FT.txt";
            FTFile.SetFilePath(filePath);

            AngleFile.SetFileHeader("实时角度", textBox9.Text, textBox8.Text, "");
            FTFile.SetFileHeader("实时力矩", textBox9.Text, textBox8.Text, "");

            AngleFile.StratSaveFile();
            FTFile.StratSaveFile();

            IsSaveFile = true;

            AngleFile.SaveHeader(31);
            FTFile.SaveHeader(60);

            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "保存", "开始保存", "已开始");

            Thread childThread = new Thread(Save_main);
            childThread.Start();
        }
        /// <summary>
        /// 停止保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            IsSaveFile = false;

            AngleFile.ForcedStop();
            FTFile.ForcedStop();
            LogUI.Log(Thread.CurrentThread.ManagedThreadId, "保存", "停止保存", "已终止");
        }
        #endregion

        #region 手动操作
        ManualAdjustment Ma = new ManualAdjustment();//定义手动调整对象
        /// <summary>
        /// 手动操作控制驱动器初始化
        /// </summary>
        void Manual_DriverInit()
        {
            Ma.SetHDTDriver(HDTX, HDTY, HDTZ);
        }
        /// <summary>
        /// 手动运动目标位置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button24_Click(object sender, EventArgs e)
        {
            Ma.GoToPoint(Convert.ToInt32(textBox5.Text), Convert.ToInt32(textBox6.Text), Convert.ToInt32(textBox7.Text));
        }
        /// <summary>
        /// 左侧推杆(受试者角度)伸长
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button7_Click(object sender, EventArgs e)
        {
            Ma.Manual_PushRod_Left(Convert.ToInt32(textBox1.Text));
        }
        /// <summary>
        /// 左侧推杆(受试者角度)缩短
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button8_Click(object sender, EventArgs e)
        {
            Ma.Manual_PushRod_Left(-Convert.ToInt32(textBox1.Text));
        }
        /// <summary>
        /// 停止手动运动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PushRod_Stop(object sender, EventArgs e)
        {
            Ma.Manual_Stop();
        }
        /// <summary>
        /// 右侧推杆(受试者角度)伸长
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button30_Click(object sender, EventArgs e)
        {
            Ma.Manual_PushRod_Right(Convert.ToInt32(textBox13.Text));
        }
        /// <summary>
        /// 右侧推杆(受试者角度)缩短
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button29_Click(object sender, EventArgs e)
        {
            Ma.Manual_PushRod_Right(-Convert.ToInt32(textBox13.Text));
        }
        /// <summary>
        /// 底部电机正转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button15_Click(object sender, EventArgs e)
        {
            Ma.Manual_Motor_Buttom(Convert.ToInt32(textBox3.Text));
        }
        /// <summary>
        /// 底部电机反转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button14_Click(object sender, EventArgs e)
        {
            Ma.Manual_Motor_Buttom(-Convert.ToInt32(textBox3.Text));
        }
        /// <summary>
        /// 手动背伸操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button17_Click(object sender, EventArgs e)
        {
            Ma.Manual_BeishenZhiqu(Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动跖屈操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button18_Click(object sender, EventArgs e)
        {
            Ma.Manual_BeishenZhiqu(-Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动内收操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button20_Click(object sender, EventArgs e)
        {
            Ma.Manual_NeishouWaizhan(Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动外展操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button19_Click(object sender, EventArgs e)
        {
            Ma.Manual_NeishouWaizhan(-Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动内翻操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button22_Click(object sender, EventArgs e)
        {
            Ma.Manual_NeifanWaifan(Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动外翻操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button21_Click(object sender, EventArgs e)
        {
            Ma.Manual_NeifanWaifan(-Convert.ToInt32(textBox4.Text));
        }
        /// <summary>
        /// 手动回零操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button23_Click(object sender, EventArgs e)
        {
            Ma.GoToZero();
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

            if (checkBox18.Checked) { panel12.Enabled = true; }
            else { panel12.Enabled = false; }
        }
        #endregion

        #region 被动训练
        /// <summary>
        /// 配置训练模式驱动器
        /// </summary>
        void TrainModel_DriverInit()
        {
            trainModel.SetHDTDriver(HDTX, HDTY, HDTZ);
        }

        /// <summary>
        /// 开始被动训练按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start_Beidong_Button_Click(object sender, EventArgs e)
        {
            int axix = 0x01;
            int Low = 10;
            int High = 10;

            if (radioButton1.Checked)
            {
                axix = Train_Axis.BeishenZhiqu;
                High = Convert.ToInt32(textBox16.Text);
                Low = Convert.ToInt32(textBox17.Text);
            }
            else if (radioButton2.Checked)
            {
                axix = Train_Axis.NeishouWaizhan;
                Low = Convert.ToInt32(textBox20.Text);
                High = Convert.ToInt32(textBox21.Text);
            }
            else if (radioButton3.Checked)
            {
                axix = Train_Axis.NeifanWaifan;
                Low = Convert.ToInt32(textBox18.Text);
                High = Convert.ToInt32(textBox19.Text);
            }
            trainModel.Set_BeiDong_Parameter(axix, High, Low, Convert.ToInt32(textBox11.Text));
            trainModel.Set_Train_UI_Parameter(BeiDong_UI_panel, Start_Beidong_Button, Stop_Beidong_Button);
            trainModel.BeiDong_Strat();
        }
        /// <summary>
        /// 停止被动训练按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop_Beidong_Button_Click(object sender, EventArgs e)
        {
            trainModel.BeiDong_Stop();
        }

        #endregion

        #region 主动等速训练
        /// <summary>
        /// 开始等速训练
        /// </summary>
        private void Start_Dengsu_Button_Click(object sender, EventArgs e)
        {
            int axix = 0x01;
            int Low = 10;
            int High = 10;
            int Speed = Convert.ToInt32(textBox27.Text);
            int Thd = Convert.ToInt32(textBox26.Text);

            if (radioButton9.Checked)
            {
                axix = Train_Axis.BeishenZhiqu;
                High = Convert.ToInt32(textBox16.Text);
                Low = Convert.ToInt32(textBox17.Text);
            }
            else if (radioButton8.Checked)
            {
                axix = Train_Axis.NeishouWaizhan;
                Low = Convert.ToInt32(textBox20.Text);
                High = Convert.ToInt32(textBox21.Text);
            }
            else if (radioButton7.Checked)
            {
                axix = Train_Axis.NeifanWaifan;
                Low = Convert.ToInt32(textBox18.Text);
                High = Convert.ToInt32(textBox19.Text);
            }

            trainModel.Set_Train_UI_Parameter(DengSu_UI_panel, Start_Dengsu_Button, Stop_Dengsu_Button);
            trainModel.Set_ZhuDong_Parameter(axix, High, Low, Speed, Thd);
            trainModel.ZhuDongTrain_Start();
        }
        /// <summary>
        /// 停止等速训练
        /// </summary>

        private void Stop_Dengsu_Button_Click(object sender, EventArgs e)
        {
            trainModel.ZhuDongTrain_Stop();
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

                PaintStrat();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        TrajectoryHelper Tj=new TrajectoryHelper();
        private void button2_Click(object sender, EventArgs e)
        {
            Tj.SetTrackBar(Ref_TrackBar,Real_TrackBar);
            Tj.StartDynamicsTrackBar();

        }

        private void button32_Click(object sender, EventArgs e)
        {
            
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Tj.StopDynamicsTrackBar();
        }
    }
}
