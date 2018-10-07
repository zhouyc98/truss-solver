using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace TrussSolver
{
    public class NForce
    {
        /// <summary>
        /// 大小 (以kN为单位)
        /// </summary>
        public double Size;
        /// <summary>
        /// 方向 (该力矢量与x轴正方向的夹角,逆时针为正,以弧度为单位)
        /// </summary>
        public double Direction;
        /// <summary>
        /// 作用点 (作用结点在Truss.Nodes中从零开始的索引)
        /// </summary>
        public int NodeIndex;

        /// <summary>
        /// 该力矢量与x轴正方向夹角的正弦值
        /// </summary>
        public double SinA;
        /// <summary>
        /// 该力矢量与x轴正方向夹角的余弦值
        /// </summary>
        public double CosA;
        
        public NForce(double Size, double Direction, int NodeIndex)
        {
            this.Size = Size;
            this.Direction = Direction;
            this.NodeIndex = NodeIndex;
            SinA = Math.Sin(Direction);
            CosA = Math.Cos(Direction);
        }

        /// <summary>
        /// 用力的作用点与端点坐标，NodeIndex，UnitSize（在Grid坐标中每单位长度代表的kN数）初始化 NForce 类的新实例
        /// </summary>
        /// <param name="TS"></param>
        /// <param name="StartP"></param>
        /// <param name="EndP"></param>
        /// <param name="UnitSize">在Grid坐标中每单位长度代表的kN数</param>
        public NForce(PointF StartP, PointF EndP, int NodeIndex, double UnitSize)
        {
            float dx = EndP.X - StartP.X, dy = EndP.Y - StartP.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            Size = length * UnitSize;
            Direction = Math.Atan2(StartP.Y - EndP.Y, StartP.X - EndP.X);
            this.NodeIndex = NodeIndex;
            SinA = Math.Sin(Direction);
            CosA = Math.Cos(Direction);
        }

    }
}
