using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RAS
{
    class TrajectoryHelper
    {
        TrackBar Ref_trackBar;
        TrackBar Real_trackBar;

        int HighLimit = 15;
        int LowLimit = -15;

        int MaxHighLimit = 15;
        int MaxLowLimit = -15;

        int CycleTime = 25;//ms

        bool IsDynamicsTrackBar = false;


        public static class TrackType
        {
            /// <summary>
            /// 静态跟踪
            /// </summary>
            public static int Static = 0x01;
            /// <summary>
            /// 动态跟踪
            /// </summary>
            public static int Dynamics = 0x02;

        }

        public void SetTrackBar(TrackBar r, TrackBar real)
        {
            Ref_trackBar = r; Real_trackBar = real;
        }

        public void SetLimit(int h, int l)
        {

            if (h<=l) { throw new ArgumentException("错误值"); }

            if (h >= MaxHighLimit) { h = MaxHighLimit; }

            if (l <= MaxLowLimit) { l = MaxLowLimit; }

            HighLimit = h; LowLimit = l;
        }

        public void SetRefTrackBarVal(double val)
        {
            if (val >= HighLimit) { val = HighLimit; }
            else if (val <= LowLimit) { val = LowLimit; }
            Ref_trackBar.Value = (int)(val * 100);
        }

        public void SetRealTrackBarVal(double val)
        {
            if (val >= HighLimit) { val = HighLimit; }
            else if (val <= LowLimit) { val = LowLimit; }
            Real_trackBar.Value = (int)(val * 100);
        }


        public void StartDynamicsTrack(int ms=50)
        {
            CycleTime = ms;

            IsDynamicsTrackBar = true;
            Thread.Sleep(20);

            Thread mythread = new Thread(DynamicsTrackBarThreadEntry);
            mythread.Start();
        }

        public void StartStaticTrack(int ms = 50)
        {
            CycleTime = ms;

            IsDynamicsTrackBar = true;
            Thread.Sleep(20);

            Thread mythread = new Thread(StaticTrackingThreadEntry);
            mythread.Start();

        }

        public void DynamicsTrackBarThreadEntry()
        {
            UInt64 index = 0;
            double TrainCycleTime = 10;//10s

            double TrainROM = 14;
            
            while (IsDynamicsTrackBar)
            {

                double Degree = TrainROM * Math.Sin(2 * Math.PI / (TrainCycleTime * 1000 / CycleTime) * index);

                MainForm.TrackAngle = (float)Degree;

                SetRefTrackBarVal(Degree);
                SetRealTrackBarVal(MainForm.IdealDegreeSensor.degreeX);
                //SetRealTrackBarVal(MainForm.DegreeSensor.degreeX);

                index++;
                Thread.Sleep(CycleTime-1);

            }
            if (!IsDynamicsTrackBar)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }

        public void StopDynamicsTrackBar()
        {
            IsDynamicsTrackBar = false;
        }
        /// <summary>
        /// 静态跟踪线程入口，10~-10~5~-5~10
        /// </summary>
        public void StaticTrackingThreadEntry()
        {
            int index = 0;
            int InitTime = 100;
            double Degree = 0;
            while (IsDynamicsTrackBar)
            {
                if (index < InitTime)
                {
                    Degree = 0;
                }
                else if (index <= 100+ InitTime)
                {
                    Degree = 10;
                }
                else if (index <= 200+ InitTime)
                {
                    Degree = -10;
                }
                else if (index <= 300 + InitTime)
                {
                    Degree = 5;
                }
                else if (index <= 400 + InitTime)
                {
                    Degree = -5;           
                }
                else
                { index = InitTime; }

                
                MainForm.TrackAngle = (float)Degree;
                SetRefTrackBarVal(Degree);
                //SetRealTrackBarVal(MainForm.DegreeSensor.degreeX);
                SetRealTrackBarVal(MainForm.IdealDegreeSensor.degreeX);

                index++;
                Thread.Sleep(50-1);
            }
            if (!IsDynamicsTrackBar)
            {
                Thread.Sleep(100);
                Thread.CurrentThread.Interrupt();
                return;
            }
        }

    }
}
