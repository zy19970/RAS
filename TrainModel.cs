/**
* @ClassName：TrainModel
* @Author: Joey (zy19970@hotmail.com)
* @Date: 2024.04.03
* @Para: BeiDong_Train_Axis、SetHDTDriver()、Set_BeiDong_Parameter()
* @Rely: 私有类CANalystHelper、HDTDriver
* @Description: 实现三个方向的被动康复训练。
*/
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using static RAS.MainForm;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace RAS
{
    /// <summary>
    /// 定义训练方向
    /// </summary>
    public static class Train_Axis
    {
        public static int BeishenZhiqu = 0x01;
        public static int NeishouWaizhan = 0x02;
        public static int NeifanWaifan = 0x03;
    }
    internal class TrainModel
    {

        public double RealDegree;

        public double M2Admittance = 0.1;
        public double B2Admittance = 0.5;
        public double K2Admittance = 0.8;

        public double HighLimit = 10;
        public double LowLimit = 10;

        public UInt32 BeidongIndex = 0;
        public double BeidongCycleTime = 10;//s
        public int BeidongCycleMs = 25;//ms

        public bool IsTrainCycle = false; //训练状态标志位

        public double[] Poistion;//位置队列
        public double[] Response;//响应队列


        public static CANalystHelper Cnh = new CANalystHelper();
        public HDTDriver HDTX;
        public HDTDriver HDTY;
        public HDTDriver HDTZ;

        Panel UIpanel;
        Button StratBtn;
        Button StopBtn;

        int CurrentTrainingDirection = 0x01;//被动训练运动方向

        #region 配置传感器
        public void SetTrainParameter()
        {

        }
        /// <summary>
        /// 配置机器人参与训练的驱动器
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Z"></param>
        public void SetHDTDriver(HDTDriver X, HDTDriver Y, HDTDriver Z)
        {
            HDTX = X;
            HDTY = Y;
            HDTZ = Z;
        }
        /// <summary>
        /// 发送插补位置
        /// </summary>
        /// <param name="pluse"></param>
        private void SendPos(int[] pluse)
        {
            HDTX.SetIPosition(pluse[0]);
            HDTY.SetIPosition(pluse[1]);
            HDTZ.SetIPosition(pluse[2]);
        }
        /// <summary>
        /// 配置训练交互UI
        /// </summary>
        /// <param name="p">模式变更控件</param>
        /// <param name="on">开始训练按钮</param>
        /// <param name="off">结束训练按钮</param>
        public void Set_Train_UI_Parameter(Panel p, Button on, Button off)
        {
            UIpanel = p; StratBtn = on; StopBtn = off;
        }
        #endregion

        #region 被动训练
        /// <summary>
        /// 设置被动训练参数
        /// </summary>
        /// <param name="axis">训练方向，使用BeiDong_Train_Axis类</param>
        /// <param name="hLimit">正限位</param>
        /// <param name="lLimit">负限位(正数)</param>
        /// <param name="CycleTime">一个运动周期</param>
        /// <param name="CycleMs">插补周期</param>

        public void Set_BeiDong_Parameter(int axis, double hLimit, double lLimit, int CycleTime = 10, int CycleMs = 25)
        {
            HighLimit = hLimit;
            LowLimit = lLimit;
            CurrentTrainingDirection = axis;
            BeidongCycleTime = CycleTime;
            //BeidongCycleMs=CycleMs;

        }

        /// <summary>
        /// 开始被动训练
        /// </summary>
        public void BeiDong_Strat()
        {

            StopBtn.Enabled = true;
            StratBtn.Enabled = false;
            UIpanel.Enabled = false;

            HDTX.IPInit();
            HDTY.IPInit();
            HDTZ.IPInit();

            IsTrainCycle = true;
            Thread.Sleep(20);

            Thread mythread = new Thread(BeiDongTrain_ThreadEntry);
            mythread.Start();

        }
        /// <summary>
        /// 停止被动训练
        /// </summary>
        public void BeiDong_Stop()
        {
            IsTrainCycle = false;
            HDTX.IPStop();
            HDTY.IPStop();
            HDTZ.IPStop();

            StopBtn.Enabled = false;
            StratBtn.Enabled = true;
            UIpanel.Enabled = true;

        }

        
        /// <summary>
        /// 被动训练线程入口
        /// </summary>
        private void BeiDongTrain_ThreadEntry()
        {
            double Degree = 0;
            BeidongIndex = 0;

            double[] z = new double[3];

            while (IsTrainCycle)
            {
                //正弦函数生成
                Degree = Math.Sin(2 * Math.PI / (BeidongCycleTime * 1000 / BeidongCycleMs) * BeidongIndex);

                //通过设定的最大最小值来缩放正弦轨迹
                if (Degree > 0) { Degree = HighLimit * Degree; }
                else { Degree = LowLimit * Degree; }

                BeidongIndex++;

                //判断当前运动方向

                //z=eso.ESO_ADRC(z[0], z[1], z[2], DegreeSensor.degreeX, Degree);

                //Degree = Degree+( Degree - z[0])*0.3;
                //Console.WriteLine((Degree + (Degree - z[0]) * 0.3).ToString("0.000"));

                if (CurrentTrainingDirection == Train_Axis.BeishenZhiqu) { IdealDegreeSensor.degreeX=(float)Degree; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = 0; }
                else if (CurrentTrainingDirection == Train_Axis.NeishouWaizhan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = (float)Degree; }
                else if (CurrentTrainingDirection == Train_Axis.NeifanWaifan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = (float)Degree; IdealDegreeSensor.degreeZ = 0; }

                

                SendPos(KinematicsHelper.InverseSolution(IdealDegreeSensor.degreeX, IdealDegreeSensor.degreeY, IdealDegreeSensor.degreeZ));

                Thread.Sleep(BeidongCycleMs - 1);

            }
            if (!IsTrainCycle)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
        #endregion

        #region 经典主动等速训练
        private float Isotonic_T = 3.0f;//力矩阈值
        private float ZhuDong_increment = 0.0f;//位置增量
        private float ZhuDong_Degree = 0.0f;//位置增量
        private float Isotonic_V = 15.0f;//运动速度
        float Tanh(float T)
        {
            return (float)((float)Sgn(T) * (Arctan(Sgn(T) * 6 * T / Isotonic_T - 4) + 1) / 2);
        }
        float Sgn(float x)
        {
            if (x > 0)
            {
                return 1.0f;
            }
            else if (x < 0)
            {
                return -1.0f;
            }
            else
            {
                return 0.0f;
            }
        }
        float Arctan(float T)
        {
            float a = (float)Math.Exp(T);
            float b = (float)Math.Exp(-T);
            return (a - b) / (a + b);
        }

        /// <summary>
        /// 配置主动等速参数
        /// </summary>
        /// <param name="axis">训练方向，使用BeiDong_Train_Axis类</param>
        /// <param name="hLimit">正限位</param>
        /// <param name="lLimit">负限位(正数)</param>
        /// <param name="speed">运动速度</param>
        /// <param name="Thd">力矩阈值</param>
        public void Set_ZhuDong_Parameter(int axis, double hLimit, double lLimit, int speed = 15, int Thd = 3)
        {
            HighLimit = hLimit;
            LowLimit = lLimit;
            CurrentTrainingDirection = axis;
            Isotonic_V = speed;
            Isotonic_T = Thd;
        }
        /// <summary>
        /// 开始主动训练
        /// </summary>
        public void ZhuDongTrain_Start()
        {
            StopBtn.Enabled = true;
            StratBtn.Enabled = false;
            UIpanel.Enabled = false;

            HDTX.IPInit();
            HDTY.IPInit();
            HDTZ.IPInit();

            IsTrainCycle = true;
            Thread.Sleep(20);

            Thread mythread = new Thread(ZhuDongTrain_ThreadEntry);
            mythread.Start();
        }
        /// <summary>
        /// 主动训练线程入口
        /// </summary>
        private void ZhuDongTrain_ThreadEntry()
        {

            while (IsTrainCycle)
            {
                float Tq = 0.0f;

                if (CurrentTrainingDirection == Train_Axis.BeishenZhiqu) { Tq = FTSensor.torqueX; }
                else if (CurrentTrainingDirection == Train_Axis.NeishouWaizhan) { Tq = FTSensor.torqueZ; }
                else if (CurrentTrainingDirection == Train_Axis.NeifanWaifan) { Tq = FTSensor.torqueY; }

                ZhuDong_increment = 0;

                if (Math.Abs(Tq)<= Isotonic_T)
                {
                    ZhuDong_increment = Tanh(Tq) * Isotonic_V * 25 / 1000;
                }
                else 
                {
                    ZhuDong_increment = Sgn(Tq)*Isotonic_V * 25 / 1000;
                }

                ZhuDong_Degree = ZhuDong_Degree + ZhuDong_increment;
                if (ZhuDong_Degree>= HighLimit)
                {
                    ZhuDong_Degree = (float)HighLimit;
                }
                else if (ZhuDong_Degree <= -LowLimit)
                {
                    ZhuDong_Degree = -(float)LowLimit;
                }

                if (CurrentTrainingDirection == Train_Axis.BeishenZhiqu) { IdealDegreeSensor.degreeX = (float)ZhuDong_Degree; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = 0; }
                else if (CurrentTrainingDirection == Train_Axis.NeishouWaizhan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = (float)ZhuDong_Degree; }
                else if (CurrentTrainingDirection == Train_Axis.NeifanWaifan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = (float)ZhuDong_Degree; IdealDegreeSensor.degreeZ = 0; }
                SendPos(KinematicsHelper.InverseSolution(IdealDegreeSensor.degreeX, IdealDegreeSensor.degreeY, IdealDegreeSensor.degreeZ));

                Thread.Sleep(24);

            }
            if (!IsTrainCycle)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
        /// <summary>
        /// 停止主动训练
        /// </summary>
        public void ZhuDongTrain_Stop()
        {
            IsTrainCycle = false;
            HDTX.IPStop();
            HDTY.IPStop();
            HDTZ.IPStop();

            StopBtn.Enabled = false;
            StratBtn.Enabled = true;
            UIpanel.Enabled = true;
        }
        #endregion



        #region ADRC

        ADRCHelper eso = new ADRCHelper();

        public static class ESOType
        {
            /// <summary>
            /// 固定带宽
            /// </summary>
            public static int FixedESO = 0x01;
            /// <summary>
            /// 双带宽
            /// </summary>
            public static int BiESO = 0x02;
            /// <summary>
            /// 自适应带宽
            /// </summary>
            public static int ScaleESO = 0x03;
            /// <summary>
            /// 自割机制
            /// </summary>
            public static int AESO = 0x04;
        }



        int BetaType = 0x01;
        int track = 0x01;
        int Bandwidth = 6;
        bool IsSpeedChange = false;
        bool IsBandwidthChange = false;


        public void Set_ADRC_Parameter(int axis, int Type,int w0,double hLimit=15, double lLimit=15, int speed = 15, int Thd = 3)
        {
            BetaType = Type;
            Bandwidth = w0;
            HighLimit = hLimit;
            LowLimit = lLimit;
            CurrentTrainingDirection = axis;
            Isotonic_V = speed;
            Isotonic_T = Thd;
        }

        public void SetADRC_Parameter(int axis, int ESOType,int tracktype,double hLimit = 15, double lLimit = 15, int w0=6, int speed = 15, int Thd = 3)
        {
            BetaType= ESOType;
            track= tracktype;
            Bandwidth =w0;
            HighLimit = hLimit;
            LowLimit = lLimit;
            CurrentTrainingDirection = axis;
            Isotonic_V = speed;
            Isotonic_T = Thd;
        }

        public void ADRCTrain_Start()
        {
            //StopBtn.Enabled = true;
            //StratBtn.Enabled = false;
            //UIpanel.Enabled = false;

            HDTX.IPInit();
            HDTY.IPInit();
            HDTZ.IPInit();

            IsTrainCycle = true;
            Thread.Sleep(20);

            int StateCase = 0;

            if (track== TrajectoryHelper.TrackType.Static)
            {

            }
            else if (track == TrajectoryHelper.TrackType.Dynamics)
            {

            }

            if (BetaType==ESOType.FixedESO)
            {
                StateCase = 1;
            }
            else if (BetaType==ESOType.BiESO)
            {
                StateCase = 2;
            }
            else if (BetaType == ESOType.ScaleESO)
            {
                StateCase = 3;
            }
            else if (BetaType == ESOType.AESO)
            {
                StateCase = 4;
            }

            switch (StateCase)
            {
                case 1:
                    Bandwidth = 6;
                    Isotonic_V = 10;
                    Isotonic_T = 3;
                    IsBandwidthChange = false;
                    IsSpeedChange = false;
                    break;
                case 2:
                    Bandwidth = 6;
                    Isotonic_V = 15;
                    Isotonic_T = 3;
                    IsBandwidthChange = false;
                    IsSpeedChange = true;
                    break;
                case 3:
                    Bandwidth = 0;
                    Isotonic_V = 15;
                    Isotonic_T = 3;
                    IsBandwidthChange = false;
                    IsSpeedChange = false;
                    break;
                case 4:
                    Bandwidth = 0;
                    Isotonic_V = 15;
                    Isotonic_T = 3;
                    IsBandwidthChange = false;
                    IsSpeedChange = true;
                    break;
                case 5:
                    Bandwidth = 6;
                    Isotonic_V = 5;
                    Isotonic_T = 3;
                    IsBandwidthChange = true;
                    IsSpeedChange = false;
                    break;
                case 6:
                    Bandwidth = 6;
                    Isotonic_V = 15;
                    Isotonic_T = 3;
                    IsBandwidthChange = true;
                    IsSpeedChange = true;
                    break;
                default:
                    ADRCTrain_Stop();
                    break;
            }



            Thread mythread = new Thread(ADRCTrain_ThreadEntry);
            mythread.Start();
        }

        private void ADRCTrain_ThreadEntry()
        {

            double[] z = new double[3];
            double Degree = 0;

            while (IsTrainCycle)
            {
                float Tq = 0.0f;

                if (CurrentTrainingDirection == Train_Axis.BeishenZhiqu) { Tq = FTSensor.torqueX; }
                else if (CurrentTrainingDirection == Train_Axis.NeishouWaizhan) { Tq = FTSensor.torqueZ; }
                else if (CurrentTrainingDirection == Train_Axis.NeifanWaifan) { Tq = FTSensor.torqueY; }

                ZhuDong_increment = 0;

                if (Math.Abs(Tq) <= Isotonic_T)
                {
                    ZhuDong_increment = Tanh(Tq) * Isotonic_V * 25 / 1000;
                }
                else if (Math.Abs(Tq) <= 2 * Isotonic_T)
                {
                    ZhuDong_increment = Sgn(Tq)*Isotonic_V * 25 / 1000;
                }
                else if (!IsSpeedChange)
                {
                    ZhuDong_increment = Sgn(Tq) * Isotonic_V * 25 / 1000;
                }
                else if (IsSpeedChange)
                {
                    ZhuDong_increment = Sgn(Tq) * 2 * Isotonic_V * 25 / 1000;
                }

                Degree = Degree + ZhuDong_increment;
                if (Degree >= HighLimit)
                {
                    Degree = (float)HighLimit;
                }
                else if (Degree <= -LowLimit)
                {
                    Degree = -(float)LowLimit;
                }

                if (IsBandwidthChange)
                {
                    //if (Math.Abs(Tq) >= 5) { Bandwidth = 6; }
                    //else { Bandwidth = 2; }

                    if (Math.Abs(IdealDegreeSensor.degreeX - MainForm.TrackAngle) >= 3) { Bandwidth = 2; }
                    else { Bandwidth = 1; }

                    //z=eso.ESO_ADRC(z[0], z[1], z[2], DegreeSensor.degreeX, Degree);
                    z = eso.ESO_ADRC(z[0], z[1], z[2], IdealDegreeSensor.degreeX, Degree, Bandwidth);
                    Degree = Degree + (Degree - z[0]) * 0.3;
                    Console.WriteLine((Degree + (Degree - z[0]) * 0.3).ToString("0.000"));

                }
                else if (Bandwidth == 0)
                {

                }
                else 
                {
                    //z=eso.ESO_ADRC(z[0], z[1], z[2], DegreeSensor.degreeX, Degree);
                    z = eso.ESO_ADRC(z[0], z[1], z[2], IdealDegreeSensor.degreeX, Degree, Bandwidth);
                    Degree = Degree + (Degree - z[0]) * 0.3;
                    Console.WriteLine((Degree + (Degree - z[0]) * 0.3).ToString("0.000"));
                }


                
                

                if (CurrentTrainingDirection == Train_Axis.BeishenZhiqu) { IdealDegreeSensor.degreeX = (float)Degree; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = 0; }
                else if (CurrentTrainingDirection == Train_Axis.NeishouWaizhan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = 0; IdealDegreeSensor.degreeZ = (float)Degree; }
                else if (CurrentTrainingDirection == Train_Axis.NeifanWaifan) { IdealDegreeSensor.degreeX = 0; IdealDegreeSensor.degreeY = (float)Degree; IdealDegreeSensor.degreeZ = 0; }
                SendPos(KinematicsHelper.InverseSolution(IdealDegreeSensor.degreeX, IdealDegreeSensor.degreeY, IdealDegreeSensor.degreeZ));

                Thread.Sleep(24);

            }
            if (!IsTrainCycle)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }
        public void ADRCTrain_Stop()
        {
            IsTrainCycle = false;
            HDTX.IPStop();
            HDTY.IPStop();
            HDTZ.IPStop();
        }

            #endregion


        }
}
