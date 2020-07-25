using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace TrussSolver
{
    public class Truss
    {
        public List<PointF> Nodes;      //桁架结点坐标
        public List<NForce> Loads;      //桁架所受结点力荷载
        public List<Bearing> Bears;     //桁架支座
        public List<int[]> NodesConnect;    //表示结点之间连接情况（杆件）的二维数组
        public double[,] FN;                //表示内力
        public double[,] RE;                //表示支座反力
        public double EA;

        public int NCount { get { return Nodes.Count; } }
        public int FCount { get { return Loads.Count; } }
        public int BCount { get { return Bears.Count; } }

        public Truss()
        {
            Nodes = new List<PointF>();
            Loads = new List<NForce>();
            Bears = new List<Bearing>();
            NodesConnect = new List<int[]>();
            FN = null;
            RE = null;
            EA = 1;
        }

        public int[] AddMember(PointF StartP, PointF EndP)//添加杆件，返回startP，endP从零开始的索引
        {
            if (StartP == EndP)
                return new int[] { -1, -1 };

            if (Nodes.Count == 0)
            {
                Nodes.Add(StartP); Nodes.Add(EndP);
                NodesConnect.Add(new int[] { 0 });
                NodesConnect.Add(new int[] { 1, 0 });
                return new int[] { 0, 1 };
            }
            else
            {
                int NC = NCount;
                int[] Ind = new int[] { NC, NC + 1 };
                for (int i = 0; i < NC; i++)
                {
                    if (StartP == Nodes[i])
                        Ind[0] = i;
                    if (EndP == Nodes[i])
                        Ind[1] = i;
                }
                if (Ind[0] < NC && Ind[1] > NC)
                    Ind[1] = NC;

                int Imin = Math.Min(Ind[0], Ind[1]), Imax = Math.Max(Ind[0], Ind[1]);
                if (Imax < NC)//startP,endP均为已有结点
                {
                    NodesConnect[Imax][Imin] = 1;
                }
                else if (Imax == NC)//startP,endP其中一个为已有结点，另一个为新增结点
                {
                    if (Ind[0] == NC)
                        Nodes.Add(StartP);
                    else
                        Nodes.Add(EndP);
                    NodesConnect.Add(new int[NC + 1]);
                    NodesConnect[Imax][Imin] = 1;
                }
                else//startP,endP均为新增结点
                {
                    Nodes.Add(StartP); Nodes.Add(EndP);
                    NodesConnect.Add(new int[NC + 1]);
                    NodesConnect.Add(new int[NC + 2]);
                    NodesConnect[Imax][Imin] = 1;
                }

                return Ind;
            }
        }
        public bool DelMember(int NInd1, int NInd2)//删除杆件，视情况删除结点
        {
            if (!ContainsMember(NInd1, NInd2))
                return false;
            int Imin = Math.Min(NInd1, NInd2), Imax = Math.Max(NInd1, NInd2);

            int cc1 = GetConnectedCount(NInd1), cc2 = GetConnectedCount(NInd2);
            if (cc1 <= 0 || cc2 <= 0)//可以注释掉
                return false;

            if (cc1 == 1 && cc2 == 1)//要删除的杆件是孤立的
            {
                for (int i = NInd1; i < NCount; i++)
                    NodesConnect[i][NInd1] = -1;
                for (int i = NInd2; i < NCount; i++)
                    NodesConnect[i][NInd2] = -1;

                Nodes.RemoveAt(Imax); Nodes.RemoveAt(Imin);
                NodesConnect.RemoveAt(Imax); NodesConnect.RemoveAt(Imin);
                for (int i = 0; i < NCount; i++)
                {
                    var r = from c in NodesConnect[i] where c >= 0 select c;
                    NodesConnect[i] = r.ToArray();
                }

                Loads.RemoveAll(f => f.NodeIndex == NInd1);//若结点上有荷载，要删除
                Loads.RemoveAll(f => f.NodeIndex == NInd2);
                Bears.RemoveAll(b => b.NodeIndex == NInd1);//若结点上有支座，要删除
                Bears.RemoveAll(b => b.NodeIndex == NInd2);


                return true;

            }
            else if (cc1 == 1 || cc2 == 1)//要删除的杆件是半孤立的(有一个结点的ConnectedCount=1)
            {
                int delInd;
                if (cc1 == 1)
                    delInd = NInd1;
                else
                    delInd = NInd2;

                Nodes.RemoveAt(delInd); NodesConnect.RemoveAt(delInd);
                for (int i = delInd; i < NCount; i++)//标记要删除的地方
                    NodesConnect[i][delInd] = -1;

                for (int i = 0; i < NCount; i++)
                {
                    var r = from c in NodesConnect[i] where c >= 0 select c;
                    NodesConnect[i] = r.ToArray();
                }

                Loads.RemoveAll(f => f.NodeIndex == delInd);//若结点上有荷载，要删除
                Bears.RemoveAll(b => b.NodeIndex == delInd);//若结点上有支座，要删除

                return true;
            }
            else//要删除的杆件是非孤立的(两个结点的ConnectedCount>=2)
            {
                NodesConnect[Imax][Imin] = 0;
                return true;
            }

        }
        public int AddForce(NForce F)//添加荷载，返回荷载在Loads中从零开始的索引
        {
            if (F.NodeIndex < 0 || F.NodeIndex >= NCount)
                return -1;

            Loads.Add(F);
            return FCount - 1;
        }
        public bool DelForce(int FInd)//删除荷载
        {
            if (FInd < 0 || FInd >= FCount)
                return false;
            Loads.RemoveAt(FInd);
            return true;
        }
        public int AddBearing(Bearing B)//添加支座，返回支座在Bears中从零开始的索引
        {
            if (B.NodeIndex < 0 || B.NodeIndex >= NCount)
                return -1;

            Bears.Add(B);
            return BCount - 1;
        }
        public bool DelBearing(int NInd)
        {
            if (NInd < 0 || NInd > NCount)
                return false;
            int BInd = -1;
            for (int i = 0; i < BCount; i++)
                if (Bears[i].NodeIndex == NInd)
                { BInd = i; break; }

            if (BInd == -1)
                return false;

            Bears.RemoveAt(BInd);
            return true;
        }

        public void ClearAll()//删除所有杆件，荷载和支座
        {
            Nodes.Clear();
            Loads.Clear();
            Bears.Clear();
            NodesConnect.Clear();
            FN = null;
            RE = null;
        }
        public bool ContainsMember(int NInd1, int NInd2)
        {
            int Imin = Math.Min(NInd1, NInd2), Imax = Math.Max(NInd1, NInd2);
            if (NInd1 == NInd2 || Imin < 0 || Imax >= NCount)//索引相等或超出范围
                return false;
            if (NodesConnect[Imax][Imin] != 1)//两结点之间没有连杆
                return false;
            return true;
        }
        public bool ContainsForce(int NInd)//返回值为true时，该结点可能有多个NForce
        {
            foreach (var F in Loads)
                if (F.NodeIndex == NInd)
                    return true;
            return false;

        }
        public int ContainsBearing(int NInd)//若给定结点索引有支座，则返回BI，否则返回-1
        {
            for (int i = 0; i < BCount; i++)
                if (Bears[i].NodeIndex == NInd)
                    return i;

            return -1;
        }

        public int GetConnectedCount(int NInd)//返回指定结点与多少结点相连，Ind为指定结点在Nodes中从零开始的索引
        {
            if (NInd < 0 || NInd >= NCount)
                return -1;

            int N = NodesConnect[NInd].Sum();
            for (int i = NInd; i < NCount; i++)
                N += NodesConnect[i][NInd];

            return N;
        }
        public int GetNodeIndex(PointF ptf)//返回指定结点在Nodes中从零开始的索引，若不存在则返回-1
        {
            return Nodes.FindIndex(n => n == ptf);
        }
        public PointF GetNearestNode(PointF ptf)
        {
            if (NCount == 0)
                return PointF.Empty;

            double[] D = new double[NCount];
            double dx, dy;
            for (int i = 0; i < NCount; i++)
            {
                dx = Nodes[i].X - ptf.X;
                dy = Nodes[i].Y - ptf.Y;
                D[i] = dx * dx + dy * dy;
            }

            int Ind = 0;
            for (int i = 1; i < D.Length; i++)
            {
                if (D[i] < D[Ind])
                    Ind = i;
            }
            return Nodes[Ind];
        }

        public bool OpenFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.InitialDirectory = Application.StartupPath;//

            ofd.Multiselect = false;
            ofd.Title = "请选择文件";
            ofd.Filter = "Xml文件（*.xml）|*.xml";
            if (ofd.ShowDialog() != DialogResult.OK)
                return false;

            ClearAll();

            try
            {
                XmlDocument fileXml = new XmlDocument();
                fileXml.Load(ofd.FileName);

                string[] sn = fileXml.SelectSingleNode(@"/Truss/Nodes").InnerText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                string[] n1 = sn[0].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] n2 = sn[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < n1.Length; i++)
                    Nodes.Add(new PointF(Convert.ToSingle(n1[i]), Convert.ToSingle(n2[i])));

                string[] sf = fileXml.SelectSingleNode(@"/Truss/Loads").InnerText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                string[] f1 = sf[0].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] f2 = sf[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] f3 = sf[2].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < f1.Length; i++)
                    Loads.Add(new NForce(Convert.ToDouble(f1[i]), Convert.ToDouble(f2[i]), Convert.ToInt32(Convert.ToDouble(f3[i]))));

                string[] sb = fileXml.SelectSingleNode(@"/Truss/Bears").InnerText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                string[] b1 = sb[0].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] b2 = sb[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < b1.Length; i++)
                    Bears.Add(new Bearing(int.Parse(b1[i]), (Bearing.Constraint)int.Parse(b2[i])));

                string[] snc = fileXml.SelectSingleNode(@"/Truss/NodesConnect").InnerText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in snc)
                {
                    string[] nc = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    int[] nc1 = new int[nc.Length];
                    for (int i = 0; i < nc.Length; i++)
                        nc1[i] = Convert.ToInt32(nc[i]);
                    NodesConnect.Add(nc1);
                }

                EA = Convert.ToInt32(fileXml.SelectSingleNode(@"/Truss/EA").InnerText);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }

            return true;
        }
        public bool SaveFile()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            //sfd.InitialDirectory = Application.StartupPath;//

            sfd.Filter = "Xml文件（*.xml）|*.xml";
            sfd.FilterIndex = 1;
            sfd.RestoreDirectory = true;

            if (sfd.ShowDialog() != DialogResult.OK)
                return false;

            string filePath = sfd.FileName; //获得文件路径
            string fileName = filePath.Substring(filePath.LastIndexOf("\\") + 1); //获取文件名，不带路径

            XmlDocument fileXml = new XmlDocument();
            XmlDeclaration decl = fileXml.CreateXmlDeclaration("1.0", "utf-8", null);
            fileXml.AppendChild(decl);
            XmlElement rootEle = fileXml.CreateElement("Truss");
            fileXml.AppendChild(rootEle);

            XmlElement nodesEle = fileXml.CreateElement("Nodes"); XmlElement loadsEle = fileXml.CreateElement("Loads");
            XmlElement bearsEle = fileXml.CreateElement("Bears"); XmlElement nodesConnectEle = fileXml.CreateElement("NodesConnect");
            XmlElement EAEle = fileXml.CreateElement("EA");

            List<double[]> _NodesConnect = new List<double[]>(),
                _Nodes = new List<double[]>(),
                _Loads = new List<double[]>(),
                _Bears = new List<double[]>();
            for (int i = 0; i < NCount; i++)
            {
                double[] nc = new double[NCount];
                for (int j = 0; j < NCount; j++)
                {
                    if (i >= j)
                        nc[j] = NodesConnect[i][j];
                    else
                        nc[j] = NodesConnect[j][i];
                }
                _NodesConnect.Add(nc);
            }

            var n1 = Nodes.Select(n => (double)n.X).ToArray(); _Nodes.Add(n1);
            var n2 = Nodes.Select(n => (double)n.Y).ToArray(); _Nodes.Add(n2);
            var f1 = Loads.Select(f => f.Size).ToArray(); _Loads.Add(f1);
            var f2 = Loads.Select(f => f.Direction).ToArray(); _Loads.Add(f2);
            var f3 = Loads.Select(f => (double)(f.NodeIndex)).ToArray(); _Loads.Add(f3);
            var b1 = Bears.Select(b => (double)b.NodeIndex).ToArray(); _Bears.Add(b1);
            var b2 = Bears.Select(b => (double)b.XY).ToArray(); _Bears.Add(b2);

            string x;
            x = "";
            for (int i = 0; i < NCount; i++)
                x += string.Join(",", _NodesConnect[i].Select(nc => nc.ToString())) + ";";
            nodesConnectEle.InnerText = x;
            x = "";
            for (int i = 0; i < 2; i++)
                x += string.Join(",", _Nodes[i].Select(n => n.ToString())) + ";";
            nodesEle.InnerText = x;
            x = "";
            for (int i = 0; i < 3; i++)
                x += string.Join(",", _Loads[i].Select(f => f.ToString("f4"))) + ";";
            loadsEle.InnerText = x;
            x = "";
            for (int i = 0; i < 2; i++)
                x += string.Join(",", _Bears[i].Select(b => b.ToString())) + ";";
            bearsEle.InnerText = x;
            EAEle.InnerText = EA.ToString();

            rootEle.AppendChild(nodesEle); rootEle.AppendChild(loadsEle);
            rootEle.AppendChild(bearsEle); rootEle.AppendChild(nodesConnectEle);
            rootEle.AppendChild(EAEle);

            fileXml.Save(filePath);

            return true;
        }

        public int Solve()//求解内力(返回值为1时表示求解成功，<=0时表示失败)
        {
            if (NCount == 0)
                return -1;
            if (FCount == 0)
                return -2;
            if (BCount == 0)
                return -3;

            List<double[]> _NodesConnect = new List<double[]>(),
                _Nodes = new List<double[]>(),
                _Loads = new List<double[]>(),
                _Bears = new List<double[]>();
            for (int i = 0; i < NCount; i++)
            {
                double[] nc = new double[NCount];
                for (int j = 0; j < NCount; j++)
                {
                    if (i >= j)
                        nc[j] = NodesConnect[i][j];
                    else
                        nc[j] = NodesConnect[j][i];
                }
                _NodesConnect.Add(nc);
            }

            var n1 = Nodes.Select(n => (double)n.X).ToArray(); _Nodes.Add(n1);
            var n2 = Nodes.Select(n => (double)n.Y).ToArray(); _Nodes.Add(n2);

            var f1 = Loads.Select(f => f.Size).ToArray(); _Loads.Add(f1);
            var f2 = Loads.Select(f => f.Direction).ToArray(); _Loads.Add(f2);
            var f3 = Loads.Select(f => (double)(f.NodeIndex)).ToArray(); _Loads.Add(f3);

            var b1 = Bears.Select(b => (double)b.NodeIndex).ToArray(); _Bears.Add(b1);
            var b2 = Bears.Select(b => (double)b.XY).ToArray(); _Bears.Add(b2);

            #region 写入信息到TS_Matrix.txt
            //* 取消注释后，TrussSolver.exe文件同目录位置会产生TS_Matrix.txt
            StreamWriter sw = new StreamWriter("TS_Matrix.txt");
            sw.WriteLine("NodesConnect=[");
            for (int i = 0; i < NCount; i++)
                sw.WriteLine(string.Join(",", _NodesConnect[i].Select(nc => nc.ToString())));
            sw.Write("]\r\n");

            sw.WriteLine("\r\nNodes=[");
            for (int i = 0; i < 2; i++)
                sw.WriteLine(string.Join(",", _Nodes[i].Select(n => n.ToString())));
            sw.Write("]\r\n");

            sw.WriteLine("\r\nLoads=[");
            for (int i = 0; i < 3; i++)
                sw.WriteLine(string.Join(",", _Loads[i].Select(f => f.ToString("f4"))));
            sw.Write("]\r\n");

            sw.WriteLine("\r\nBears=[");
            for (int i = 0; i < 2; i++)
                sw.WriteLine(string.Join(",", _Bears[i].Select(b => b.ToString())));
            sw.Write("]\r\n");

            sw.Flush();
            sw.Close();
            //*/
            #endregion

            SolveFR(new Matrix(_NodesConnect), new Matrix(_Nodes), new Matrix(_Loads), new Matrix(_Bears), EA);

            int ans = 1;
            double FX = 0, FY = 0, RX = 0, RY = 0;// Abs
            foreach (var f in Loads)
            {
                FX += Math.Abs(f.Size * Math.Cos(f.Direction));
                FY += Math.Abs(f.Size * Math.Sin(f.Direction));
            }
            for (int i = 0; i < BCount; i++)
                RX += Math.Abs(RE[0, i]);
            for (int i = 0; i < BCount; i++)
                RY += Math.Abs(RE[1, i]);

            if (RX + RY > 1e4 * (FX + FY))
                ans = -11;

            foreach (double f in FN)
                if (Double.IsNaN(f))
                    ans = -12;

            foreach (double f in RE)
                if (Double.IsNaN(f))
                    ans = -13;

            if (ans < -10)
            { FN = null; RE = null; }

            return ans;
        }
        private void SolveFR(Matrix nodes_connect, Matrix nodes, Matrix loads, Matrix bears, double EA = 1)
        {
            Matrix K0 = new Matrix(new double[2 * NCount, 2 * NCount]);

            for (int i = 0; i < NCount - 1; i++)
            {
                for (int j = i + 1; j < NCount; j++)
                {
                    if (nodes_connect[i, j] > 0)
                    {
                        double L, a;
                        GetLa(nodes, i, j, out L, out a);
                        double c = Math.Cos(a);
                        double s = Math.Sin(a);
                        Matrix ke11 = new Matrix(new double[2, 2] { { c * c, s * c }, { s * c, s * s } });
                        ke11 = ke11 * (EA / L);

                        int indi = 2 * i, indj = 2 * j;

                        K0[indi, indi] += ke11[0, 0]; K0[indi, indi + 1] += ke11[0, 1]; K0[indi + 1, indi] += ke11[1, 0]; K0[indi + 1, indi + 1] += ke11[1, 1];
                        K0[indi, indj] -= ke11[0, 0]; K0[indi, indj + 1] -= ke11[0, 1]; K0[indi + 1, indj] -= ke11[1, 0]; K0[indi + 1, indj + 1] -= ke11[1, 1];
                        K0[indj, indi] -= ke11[0, 0]; K0[indj, indi + 1] -= ke11[0, 1]; K0[indj + 1, indi] -= ke11[1, 0]; K0[indj + 1, indi + 1] -= ke11[1, 1];
                        K0[indj, indj] += ke11[0, 0]; K0[indj, indj + 1] += ke11[0, 1]; K0[indj + 1, indj] += ke11[1, 0]; K0[indj + 1, indj + 1] += ke11[1, 1];

                    }
                }
            }

            for (int i = 0; i < K0.Rows; i++)
                for (int j = 0; j < K0.Cols; j++)
                    if (Math.Abs(K0[i, j] / EA) < 1e-16)
                        K0[i, j] = 0;

            Matrix K = K0.DeepCopy();
            Matrix F = new Matrix(new double[2 * NCount, 1]);

            for (int i = 0; i < FCount; i++)
            {
                double[] f1 = new double[] { loads[0, i], loads[1, i], loads[2, i] };
                double f1x = f1[0] * Math.Cos(f1[1]);
                double f1y = f1[0] * Math.Sin(f1[1]);
                F[2 * (int)(f1[2]), 0] = f1x;
                F[2 * (int)(f1[2]) + 1, 0] = f1y;
            }

            for (int i = 0; i < BCount; i++)// 置大数法，将F向量中对应的量置为0；将K矩阵中对应的量置为 (EA * 10**32)
            {
                double[] br = new double[] { bears[0, i], bears[1, i] };
                int ind = (int)br[0];
                double BigNumber = EA * Math.Pow(10, 32);

                if (br[1] == 0 || br[1] == 2)
                {
                    F[2 * ind, 0] = 0;
                    K[2 * ind, 2 * ind] = BigNumber;
                }
                if (br[1] == 1 || br[1] == 2)
                {
                    F[2 * ind + 1, 0] = 0;
                    K[2 * ind + 1, 2 * ind + 1] = BigNumber;
                }
            }

            Matrix D = K.Inv() * F;
            D.Round(6);//保留小数后6位

            FN = new double[NCount, NCount];
            RE = new double[2, BCount];

            for (int i = 0; i < NCount - 1; i++)// 获取杆件内力
            {
                for (int j = i + 1; j < NCount; j++)
                {
                    if (nodes_connect[i, j] > 0)
                    {
                        double L, a;
                        GetLa(nodes, i, j, out L, out a);
                        double c = Math.Cos(a);
                        double s = Math.Sin(a);

                        List<List<double>> m = new List<List<double>>();
                        m.Add(new List<double>(new double[] { c * c, s * c, -c * c, -s * c }));
                        m.Add(new List<double>(new double[] { s * c, s * s, -s * c, -s * s }));
                        m.Add(new List<double>(new double[] { -c * c, -s * c, c * c, s * c }));
                        m.Add(new List<double>(new double[] { -s * c, -s * s, s * c, s * s }));
                        Matrix Ke = new Matrix(m) * (EA / L);
                        Matrix T = new Matrix(new double[4, 4] { { c, s, 0, 0 }, { -s, c, 0, 0 }, { 0, 0, c, s }, { 0, 0, -s, c } });
                        Matrix D4 = new Matrix(new double[4, 1] { { D[2 * i, 0] }, { D[2 * i + 1, 0] }, { D[2 * j, 0] }, { D[2 * j + 1, 0] } });
                        Matrix f = T * Ke * D4;
                        FN[i, j] = Math.Round(f[0, 0], 6);//+-
                    }
                }
            }

            List<int> ind_a = new List<int>(2 * NCount);
            List<int> ind_b = new List<int>();
            for (int j = 0; j < bears.Cols; j++)
            {
                ind_b.Add(2 * (int)bears[0, j]);
                ind_b.Add(2 * (int)bears[0, j] + 1);
            }
            for (int i = 0; i < 2 * NCount; i++)
            {
                if (!ind_b.Contains(i))
                    ind_a.Add(i);
            }

            Matrix F_RE = K0.GetRows(ind_b) * D;

            for (int i = 0; i < BCount; i++)
            {
                RE[0, i] = Math.Round(F_RE[2 * i, 0], 6);
                RE[1, i] = Math.Round(F_RE[2 * i + 1, 0], 6);
            }

            // FN & RE 求解完毕
        }
        private void GetLa(Matrix nodes, int i, int j, out double l, out double a)
        {
            l = a = 0.0;
            try
            {
                double dx = nodes[0, i] - nodes[0, j];
                double dy = nodes[1, i] - nodes[1, j];
                l = Math.Sqrt(dx * dx + dy * dy);
                a = Math.Atan2(dy, dx);
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
}