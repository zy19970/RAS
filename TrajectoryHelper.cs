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

        int HighLimit = 10;
        int LowLimit = -10;

        int MaxHighLimit = 15;
        int MaxLowLimit = -15;

        int CycleTime = 25;

        bool IsDynamicsTrackBar = false;

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

        public void SetRefTrackBar(double val)
        {
            if (val >= HighLimit) { val = HighLimit; }
            else if (val <= LowLimit) { val = LowLimit; }
            Ref_trackBar.Value = (int)(val * 100);
        }

        public void SetRealTrackBar(double val)
        {
            if (val >= HighLimit) { val = HighLimit; }
            else if (val <= LowLimit) { val = LowLimit; }
            Real_trackBar.Value = (int)(val * 100);
        }


        public void StartDynamicsTrackBar(int ms=50)
        {
            CycleTime = ms;

            IsDynamicsTrackBar = true;
            Thread.Sleep(20);

            Thread mythread = new Thread(StaticTrackingThreadEntry);
            mythread.Start();
        }

        public void DynamicsTrackBarThreadEntry()
        {
            while (IsDynamicsTrackBar)
            {
                SetRefTrackBar(MainForm.IdealDegreeSensor.degreeX);
                SetRealTrackBar(MainForm.DegreeSensor.degreeX);

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

        public void StaticTrackingThreadEntry()
        {
            int index = 0;
            while (IsDynamicsTrackBar)
            {
                if (index <= 50)
                {
                    SetRefTrackBar(10);
                }
                else if (index <= 100)
                {
                    SetRefTrackBar(-10);
                }
                else if (index <= 150)
                {
                    SetRefTrackBar(5);
                }
                else if (index <= 200)
                {
                    SetRefTrackBar(-5);
                }
                else
                { index = 0; }
                index++;
                Thread.Sleep(100);
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
