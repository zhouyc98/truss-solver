using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrussSolver
{
    public class Bearing
    {
        /// <summary>
        /// 支承结点 (支承结点在Truss.Nodes中从零开始的索引)
        /// </summary>
        public int NodeIndex;
        
        public enum Constraint { X, Y, XY };
        public Constraint XY;

        public Bearing(int NodeIndex,Constraint XY )
        {
            this.NodeIndex = NodeIndex;
            this.XY = XY;
        }

    }
}
