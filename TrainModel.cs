using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RAS
{
    internal class TrainModel
    {

        public double RealDegree;

        public double M2Admittance = 0.1;
        public double B2Admittance = 0.5;
        public double K2Admittance = 0.8;

        public double HighLimit = 15;
        public double LowLimit = 15;

        public UInt32 BeidongIndex = 0;
        public double BeidongCycleTime = 10;//s
        public int BeidongCycleHz = 25;//ms

        public bool IsBeidongCycle = false;

        public double[] Poistion;//位置队列
        public double[] Response;//响应队列

        public static CANalystHelper Cnh = new CANalystHelper();
        public HDTDriver HDTX;
        public HDTDriver HDTY;
        public HDTDriver HDTZ;

        public void SetTrainParameter()
        {

        }

        public void SetHDTDriver(HDTDriver X, HDTDriver Y, HDTDriver Z)
        {
            HDTX = X;
            HDTY = Y;
            HDTZ = Z;
        }

        void BeiDongTrain_ThreadEntry()
        {
            while (IsBeidongCycle)
            {
                Thread mythread = new Thread(BeiDongTrain_Main);
                mythread.Start();


                Thread.Sleep(BeidongCycleHz - 1);

            }
            if (!IsBeidongCycle)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }

        void BeiDongTrain_Main()
        {

        }

    }
}
