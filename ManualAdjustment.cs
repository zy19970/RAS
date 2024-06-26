﻿/**
* @ClassName：ManualAdjustment
* @Author: Joey (zy19970@hotmail.com)
* @Date: 2024.05.02
* @Para: SetHDTDriver()
* @Rely: 私有类CANalystHelper、HDTDriver
* @Description: 实现手动操作单轴、单电机、多轴、多电机运动。
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RAS
{
    internal class ManualAdjustment
    {
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
        /// <summary>
        /// 机器人运动到指定位置
        /// </summary>
        /// <param name="x">末端位姿x</param>
        /// <param name="y">末端位姿y</param>
        /// <param name="z">末端位姿z</param>
        public void GoToPoint(int x, int y, int z)
        {
            HDTX.PositInit();
            HDTY.PositInit();
            HDTZ.PositInit();

            Thread.Sleep(100);

            int[] a = KinematicsHelper.InverseSolution(x, y, z);
            HDTX.SetPosition(a[0]);
            HDTY.SetPosition(a[1]);
            HDTZ.SetPosition(a[2]);

        }
        /// <summary>
        /// 机器人回到零点
        /// </summary>
        public void GoToZero()
        {
            HDTX.PositInit();
            HDTY.PositInit();
            HDTZ.PositInit();

            Thread.Sleep(100);


            HDTX.SetPosition(0);
            HDTY.SetPosition(0);
            HDTZ.SetPosition(0);
        }
        /// <summary>
        /// 机器人执行手动背伸跖屈操作
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_BeishenZhiqu(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(setValueThd);
            HDTY.SetVelocity(setValueThd);
            HDTZ.SetVelocity(0);
        }
        /// <summary>
        /// 机器人执行手动内收外展操作
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_NeishouWaizhan(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(0);
            HDTY.SetVelocity(0);
            HDTZ.SetVelocity(-setValueThd / 6);
        }
        /// <summary>
        /// 机器人执行内翻外翻操作
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_NeifanWaifan(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(-setValueThd);
            HDTY.SetVelocity(setValueThd);
            HDTZ.SetVelocity(0);
        }
        /// <summary>
        /// 机器人左端(患者角度)推杆运动
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_PushRod_Left(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(-setValueThd);
            HDTY.SetVelocity(0);
            HDTZ.SetVelocity(0);
        }
        /// <summary>
        /// 机器人右端(患者角度)推杆运动
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_PushRod_Right(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(0);
            HDTY.SetVelocity(-setValueThd);
            HDTZ.SetVelocity(0);
        }
        /// <summary>
        /// 机器人底部电机运动
        /// </summary>
        /// <param name="setValueThd">运动速度</param>
        public void Manual_Motor_Buttom(int setValueThd = 20)
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(0);
            HDTY.SetVelocity(0);
            HDTZ.SetVelocity(-setValueThd / 6);
        }
        /// <summary>
        /// 手动控制机器人停止
        /// </summary>
        public void Manual_Stop()
        {
            HDTX.VelocityInit();
            HDTY.VelocityInit();
            HDTZ.VelocityInit();

            HDTX.SetVelocity(0);
            HDTY.SetVelocity(0);
            HDTZ.SetVelocity(0);
        }
    }
}
