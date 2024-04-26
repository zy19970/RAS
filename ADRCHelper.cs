using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAS
{
    internal class ADRCHelper
    {
        double T = 0.025;
        public double[] ESO_ADRC(double z01, double z02, double z03,double y,double u0)
        {
            double w0 = 6;

            double z1 = 0;
            double z2 = 0;
            double z3 = 0;

            double e = z01 - y;

            z1 = z01 + T * (z2 - 3 * w0 * e);
            z2 = z02 + T * (z3 - 3 * w0 * w0 * e +  u0);
            z3 = z03 - T * w0 * w0 * w0 * e;
            double[] a = { z1, z2, z3 };
            //Console.WriteLine(z1.ToString("0.000") + " \t" + z2.ToString("0.000") + " \t"+z3.ToString("0.000"));
            return a;
        }

        public double fal(double epec, double alfa, double delta)
        {
            if (Math.Abs(epec) > delta)
                return Math.Pow(Math.Abs(epec), alfa) * Math.Sign(epec);
            else
                return epec / (Math.Pow(delta, (1 - alfa)));
        }


    }
}
