using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TrussSolver
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Width = 900; Height = 600;
            AutoScroll = true;
            pictureBox1.Enabled = false;
            groupBox5.Enabled = false;
            TS = new Truss();
        }
        Truss TS;
        public enum EditMode { None, GanJian, HeZai, ZhiZuo };//编辑模式
        private EditMode EM;
        private Bitmap originBmp, currentBmp, destBmp;//originBmp:坐标网格图像；currentBmp:桁架+内力图
        private PointF StartPoint, EndPoint;//鼠标左键Down，Up时对应的点
        private float R = 4.5f;             //铰接点处绘制的圆的半径大小
        private double UnitSize = 5.0;      //表示Grid坐标系上单位长度代表的荷载大小数
        private bool StartEdit, HaveDrawnFN;//DrawnFN=true表示currentBmp上绘制有内力图

        private void Form1_Load(object sender, EventArgs e)
        {
            DrawGrid();
            currentBmp = (Bitmap)originBmp.Clone();
            destBmp = (Bitmap)currentBmp.Clone();
            pictureBox1.Image = currentBmp;
            pictureBox1.Enabled = true;
            radioButton1.Select();
        }
        private void DrawGrid()
        {
            pictureBox1.BackColor = Color.White;
            int width = pictureBox1.Width, height = pictureBox1.Height;//520,520
            originBmp = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(originBmp);
            Pen pen1 = Pens.SkyBlue;//new Pen(Color.FromArgb(120, 120, 120), 1);
            Brush brush1 = Brushes.SkyBlue;//new SolidBrush(Color.FromArgb(60, 60, 60));
            Font AxisFont = new Font("Arial", 10f, FontStyle.Regular);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int XYOffset = 20, XYLength = 40, N = 13;
            for (int i = 0, X = 0; i < N; i++)// 画纵线
            {
                g.DrawLine(pen1, X + XYOffset, 0, X + XYOffset, height);
                if (i > 0)
                    g.DrawString(i.ToString(), AxisFont, brush1, X + XYOffset - 12, height - XYOffset + 2);
                X += XYLength;
            }
            for (int i = 0, Y = 0; i < N; i++)// 画横线
            {
                g.DrawLine(pen1, 0, Y + XYOffset, width, Y + XYOffset);
                g.DrawString((N - i - 1).ToString(), AxisFont, brush1, XYOffset - 12, Y + XYOffset + 2);
                Y += XYLength;
            }

            g.Dispose(); AxisFont.Dispose();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (EM == EditMode.None || e.Button != MouseButtons.Left)
                return;
            if (EM == EditMode.GanJian)
            {
                StartPoint = GetNearestValidPoint(e);
                StartEdit = true;
            }
            else if (EM == EditMode.HeZai)
            {
                StartPoint = GetNearestValidPoint(e);
                if (TS.GetNodeIndex(ToGridPointF(StartPoint)) != -1)
                    StartEdit = true;
            }
            else
            {
                StartPoint = GetNearestValidPoint(e);
                if (TS.GetNodeIndex(ToGridPointF(StartPoint)) != -1)
                    StartEdit = true;
            }

        }
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (EM == EditMode.None)
                return;

            EndPoint = GetNearestValidPoint(e);
            if (!StartEdit && EM != EditMode.GanJian && TS.GetNodeIndex(ToGridPointF(EndPoint)) == -1)
            { pictureBox1.Image = currentBmp; return; }

            destBmp.Dispose();
            destBmp = (Bitmap)currentBmp.Clone();
            Graphics g = Graphics.FromImage(destBmp);

            if (!StartEdit)
            {
                DrawPointCircle(ref g, EndPoint);
            }
            else
            {
                if (EM == EditMode.GanJian)
                {
                    g.DrawLine(Pens.DimGray, StartPoint, EndPoint);
                    DrawPointCircle(ref g, EndPoint);
                }
                else if (EM == EditMode.HeZai)
                {
                    Pen p = new Pen(Color.Red, 1.5f);
                    System.Drawing.Drawing2D.AdjustableArrowCap lineArrow =
                        new System.Drawing.Drawing2D.AdjustableArrowCap(5, 5, false);
                    p.CustomStartCap = lineArrow;
                    g.DrawLine(p, StartPoint, EndPoint);
                    p.Dispose();
                }
                else
                {
                    if (TS.GetNodeIndex(ToGridPointF(EndPoint)) != -1)
                        DrawPointCircle(ref g, EndPoint);
                }
            }
            pictureBox1.Image = destBmp;
            g.Dispose();
        }
        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (EM == EditMode.None || !StartEdit)
                return;
            if (e.Button != MouseButtons.Left)
            { StartEdit = false; return; }

            if (EM == EditMode.GanJian)
            {
                EndPoint = GetNearestValidPoint(e);
                DrawAndAddBar(StartPoint, EndPoint);
                StartEdit = false;
            }
            else if (EM == EditMode.HeZai)
            {
                EndPoint = GetNearestValidPoint(e);
                //float dx = EndPoint.X - StartPoint.X, dy = EndPoint.Y - StartPoint.Y;
                //double distance = Math.Sqrt(dx * dx + dy * dy);
                if (StartPoint != EndPoint)
                    DrawAndAddForce(StartPoint, EndPoint);
                StartEdit = false;
            }
            else //(EM == EditMode.ZhiZuo)
            {
                EndPoint = GetNearestValidPoint(e);
                if (EndPoint != StartPoint)
                    return;
                int NI = TS.GetNodeIndex(ToGridPointF(StartPoint));
                if (NI == -1)
                    return;
                int BI = TS.ContainsBearing(NI);
                if (BI == -1)
                {
                    TS.AddBearing(new Bearing(NI, Bearing.Constraint.X));
                    DrawBearing(StartPoint, Bearing.Constraint.X);
                }
                else
                {
                    int XY = (int)TS.Bears[BI].XY;
                    if (XY < 2)
                        TS.Bears[BI].XY = (Bearing.Constraint)(++XY);
                    else
                        TS.DelBearing(NI);
                    RefreshByTs();
                }
            }

        }

        private void RefreshByTs()//(TS->currentBmp->pictureBox)
        {
            currentBmp.Dispose();
            currentBmp = (Bitmap)originBmp.Clone();
            for (int i = 0; i < TS.NCount; i++)//仅使用下三角
                for (int j = 0; j < i; j++)
                    if (TS.NodesConnect[i][j] == 1)
                        DrawBar(TS.Nodes[i], i + 1, TS.Nodes[j], j + 1, true);

            for (int i = 0; i < TS.FCount; i++)
                DrawForce(TS.Nodes[TS.Loads[i].NodeIndex], Point.Empty, i + 1, TS.Loads[i].Size, true);

            for (int i = 0; i < TS.BCount; i++)
                DrawBearing(TS.Nodes[TS.Bears[i].NodeIndex], TS.Bears[i].XY, true);

            pictureBox1.Image = currentBmp;
            HaveDrawnFN = false;
        }
        private void DrawPointCircle(ref Graphics g, PointF ptf)
        {
            g.DrawEllipse(Pens.Green, ptf.X - R, ptf.Y - R, 2 * R, 2 * R);
        }
        private PointF GetNearestValidPoint(MouseEventArgs e)//返回PictureBox坐标下的PointF
        {
            //float x = (float)Math.Round((e.X - 20) / 40.0);
            //float y = (float)Math.Round((500 - e.Y) / 40.0);
            //PointF ptf1 = new PointF(40f * x + 20f, 500f - 40f * y);

            PointF ptf1 = ToPictureBoxPointF(Point.Round(ToGridPointF(e.Location)));
            PointF ptf2 = TS.GetNearestNode(ToGridPointF(e.Location));//ptf2此时是Grid坐标下的PointF
            if (ptf2.IsEmpty)
                return ptf1;
            ptf2 = ToPictureBoxPointF(ptf2);//将ptf2转为PictureBox坐标下的PointF
            double d1 = Math.Pow(ptf1.X - e.X, 2) + Math.Pow(ptf1.Y - e.Y, 2);
            double d2 = Math.Pow(ptf2.X - e.X, 2) + Math.Pow(ptf2.Y - e.Y, 2);
            if (d1 < d2)
                return ptf1;
            return ptf2;

        }
        private PointF ToGridPointF(PointF ptf)
        {
            return new PointF((ptf.X - 20f) / 40f, (500f - ptf.Y) / 40f);
        }
        private PointF ToPictureBoxPointF(PointF ptf)
        {
            return new PointF(ptf.X * 40f + 20f, 500f - ptf.Y * 40f);
        }

        private void DrawAndAddBar(PointF spf, PointF epf, bool IsGridPointF = false)//绘制杆件，并添加至TS中
        {
            if (spf == epf)
                return;
            if (!IsGridPointF)
            {
                spf = ToGridPointF(spf);
                epf = ToGridPointF(epf);
            }
            if (TS.ContainsBar(TS.GetNodeIndex(spf), TS.GetNodeIndex(epf)))
                return;
            int[] Ind = TS.AddBar(spf, epf);
            DrawBar(spf, Ind[0] + 1, epf, Ind[1] + 1, true);
        }
        private void DrawAndAddForce(PointF spf, PointF epf, bool IsGridPointF = false)
        {
            if (spf == epf)
                return;
            int NI = TS.GetNodeIndex(ToGridPointF(spf));
            if (NI == -1)
                return;
            if (!IsGridPointF)
            {
                spf = ToGridPointF(spf);
                epf = ToGridPointF(epf);
            }

            NForce nf = new NForce(spf, epf, NI, UnitSize);
            int FI = TS.AddForce(nf);
            DrawForce(spf, epf, FI + 1, nf.Size, true);
        }
        private void DrawBar(PointF spf, int spNum, PointF epf, int epNum, bool IsGridPointF = false)
        {
            if (spf == epf)
                return;
            if (IsGridPointF)
            {
                spf = ToPictureBoxPointF(spf);
                epf = ToPictureBoxPointF(epf);
            }

            Graphics g = Graphics.FromImage(currentBmp);

            double theta = Math.Atan2(epf.Y - spf.Y, epf.X - spf.X);
            float XOffset = R * (float)Math.Cos(theta), YOffset = R * (float)Math.Sin(theta);
            Pen p = new Pen(Color.DimGray, 3f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLine(p, spf.X + XOffset, spf.Y + YOffset, epf.X - XOffset, epf.Y - YOffset);
            //g.DrawLine(p, spf.X + XOffset, spf.Y + YOffset, epf.X - XOffset, epf.Y - YOffset);//画两遍更粗

            p = new Pen(Color.Orange, 1.8f);
            g.DrawEllipse(p, spf.X - R, spf.Y - R, 2 * R, 2 * R); g.DrawEllipse(p, epf.X - R, epf.Y - R, 2 * R, 2 * R);
            g.DrawEllipse(p, spf.X - R, spf.Y - R, 2 * R, 2 * R); g.DrawEllipse(p, epf.X - R, epf.Y - R, 2 * R, 2 * R);//画两遍更粗

            Font NodeIndFont = new Font("Arial", 9f, FontStyle.Regular);
            g.DrawString(spNum.ToString(), NodeIndFont, Brushes.Black, spf.X - 16, spf.Y);
            g.DrawString(epNum.ToString(), NodeIndFont, Brushes.Black, epf.X - 16, epf.Y);

            pictureBox1.Image = currentBmp;
            g.Dispose(); NodeIndFont.Dispose();
        }
        private void DrawForce(PointF spf, PointF epf, int FNum, double FSize, bool IsGridPointF = false)
        {
            if ((spf == epf && !epf.IsEmpty) || FNum <= 0 || FNum > TS.FCount)
                return;
            if (epf.IsEmpty)
            {
                if (!IsGridPointF)
                { spf = ToGridPointF(spf); epf = ToGridPointF(epf); }
                epf.X = spf.X - (float)(TS.Loads[FNum - 1].CosA * FSize / UnitSize);
                epf.Y = spf.Y - (float)(TS.Loads[FNum - 1].SinA * FSize / UnitSize);
                IsGridPointF = true;
            }

            if (IsGridPointF)
            {
                spf = ToPictureBoxPointF(spf);
                epf = ToPictureBoxPointF(epf);
            }

            Graphics g = Graphics.FromImage(currentBmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Pen p = new Pen(Color.Red, 1.4f);
            p.CustomStartCap = new System.Drawing.Drawing2D.AdjustableArrowCap(5, 5, false);
            g.DrawLine(p, spf, epf);
            Font ForceIndFont = new Font("Arial", 8f, FontStyle.Regular);
            string str = "(" + FNum.ToString() + ")" + FSize.ToString("f1") + "kN";
            g.DrawString(str, ForceIndFont, Brushes.Red, (spf.X + epf.X) / 2f + 2f, (spf.Y + epf.Y) / 2f);

            pictureBox1.Image = currentBmp;
            g.Dispose(); ForceIndFont.Dispose();
        }
        private void DrawBearing(PointF ptf, Bearing.Constraint XY, bool IsGridPointF = false)
        {
            if (IsGridPointF)
                ptf = ToPictureBoxPointF(ptf);

            float L1 = R + 2.5f * R;
            float L2 = 2f * R;
            Pen p1 = new Pen(Color.FromArgb(127, Color.Sienna), 3f);

            Graphics g = Graphics.FromImage(currentBmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            switch (XY)
            {
                case Bearing.Constraint.X:
                    g.DrawLine(p1, ptf.X - R, ptf.Y, ptf.X - L1, ptf.Y);
                    g.DrawLine(p1, ptf.X - L1, ptf.Y - L2, ptf.X - L1, ptf.Y + L2);
                    DrawBearingSlash(ref g, Pens.Black, ptf.X - L1, ptf.Y - L2, 2 * L2, true);
                    break;
                case Bearing.Constraint.Y:
                    g.DrawLine(p1, ptf.X, ptf.Y + R, ptf.X, ptf.Y + L1);
                    g.DrawLine(p1, ptf.X - L2, ptf.Y + L1, ptf.X + L2, ptf.Y + L1);
                    DrawBearingSlash(ref g, Pens.Black, ptf.X - L2, ptf.Y + L1, 2 * L2);
                    break;
                case Bearing.Constraint.XY:
                    g.DrawLine(p1, ptf.X, ptf.Y + R, ptf.X, ptf.Y + L1);
                    g.DrawLine(p1, ptf.X - L2, ptf.Y + L1, ptf.X + L2, ptf.Y + L1);
                    DrawBearingSlash(ref g, Pens.Black, ptf.X - L2, ptf.Y + L1, 2 * L2);
                    g.DrawLine(p1, ptf.X - R, ptf.Y, ptf.X - L1, ptf.Y);
                    g.DrawLine(p1, ptf.X - L1, ptf.Y - L2, ptf.X - L1, ptf.Y + L2);
                    DrawBearingSlash(ref g, Pens.Black, ptf.X - L1, ptf.Y - L2, 2 * L2, true);
                    break;
                default:
                    break;
            }
            pictureBox1.Image = currentBmp;
            g.Dispose();
        }
        private void DrawBearingSlash(ref Graphics g, Pen p, float X, float Y, float length, bool XY_X = false)
        {
            int N = 5;
            float dL = length / N, offset = 5f;

            if (XY_X)
                for (int i = 0; i <= N; i++)
                { g.DrawLine(p, X, Y, X - offset, Y - offset); Y += dL; }
            else
                for (int i = 0; i <= N; i++)
                { g.DrawLine(p, X, Y, X - offset, Y + offset); X += dL; }

        }
        private void DrawFN(bool TuXing, bool ShuZi)
        {
            int N = TS.FN.GetLength(0);
            double UnitFn = 0.05;//Grid坐标系上单位长度代表的荷载大小数
            double FnOffset = 10;//PictureBox坐标系中，轴力图相邻两垂直于杆件的竖线之间的距离

            double FNMax = 0;//!!!
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    if (Math.Abs(TS.FN[i, j]) > FNMax)
                        FNMax = Math.Abs(TS.FN[i, j]);

            Graphics g = Graphics.FromImage(currentBmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Pen pFn = new Pen(Color.FromArgb(128, Color.DarkRed));
            Font FNFont = new Font("Arial", 10f, FontStyle.Bold);
            for (int i = 0; i < N; i++)
                for (int j = i + 1; j < N; j++)
                    if (Math.Abs(TS.FN[i, j]) > 0.5e-2)
                    {
                        PointF spf = TS.Nodes[i], epf = TS.Nodes[j];
                        if (TuXing)
                        {
                            double theta = Math.Atan2(epf.Y - spf.Y, epf.X - spf.X) + Math.PI / 2;
                            double fSize = UnitFn * TS.FN[i, j];
                            PointF spf1 = new PointF(spf.X + (float)(fSize * Math.Cos(theta)), spf.Y + (float)(fSize * Math.Sin(theta)));
                            PointF epf1 = new PointF(epf.X + (float)(fSize * Math.Cos(theta)), epf.Y + (float)(fSize * Math.Sin(theta)));
                            spf = ToPictureBoxPointF(spf); epf = ToPictureBoxPointF(epf);
                            spf1 = ToPictureBoxPointF(spf1); epf1 = ToPictureBoxPointF(epf1);

                            g.DrawLine(pFn, spf, spf1); g.DrawLine(pFn, epf, epf1); g.DrawLine(pFn, spf1, epf1);
                            theta = Math.Atan2(epf.Y - spf.Y, epf.X - spf.X);
                            float L = (float)Math.Sqrt(Math.Pow(epf.X - spf.X, 2) + Math.Pow(epf.Y - spf.Y, 2));
                            int NOffset = (int)(L / FnOffset);
                            float Offset = L / NOffset;
                            float XOffset = (float)(Offset * Math.Cos(theta)), YOffset = (float)(Offset * Math.Sin(theta));
                            for (int k = 1; k < NOffset; k++)
                                g.DrawLine(pFn, spf.X + k * XOffset, spf.Y + k * YOffset, spf1.X + k * XOffset, spf1.Y + k * YOffset);
                        }
                        if (ShuZi)
                        {
                            if (!TuXing)
                            { spf = ToPictureBoxPointF(spf); epf = ToPictureBoxPointF(epf); }
                            g.DrawString(TS.FN[i, j].ToString("f2"), FNFont, Brushes.DarkRed, (spf.X + epf.X) / 2f - 28f, (spf.Y + epf.Y) / 2f);
                        }
                    }
            pictureBox1.Image = currentBmp;
            g.Dispose(); FNFont.Dispose();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0)
            {
                pictureBox1.Enabled = true;
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                pictureBox1.Enabled = false; pictureBox1.Image = currentBmp; StartEdit = false;
                richTextBox1.Clear(); richTextBox1.Text += "[杆件]\t  X\t\tY";
                int i = 0;
                foreach (var n in TS.Nodes)
                    richTextBox1.Text += "\n结点 " + (++i).ToString() + "\t  " + n.X.ToString("f2") + "\t\t" + n.Y.ToString("f2");

                i = 0;
                richTextBox1.Text += "\n\n[荷载]\t  大小(kN)  方向(°)  作用结点";
                foreach (var f in TS.Loads)
                {
                    richTextBox1.Text += "\n荷载 " + (++i).ToString() + "\t  " + f.Size.ToString("f2") + "\t    "
                        + (f.Direction * 180 / Math.PI).ToString("f2") + "  \t " + (f.NodeIndex + 1).ToString();
                }

                i = 0;
                richTextBox1.Text += "\n\n[支座]\t  作用结点\t约束方向";
                foreach (var b in TS.Bears)
                    richTextBox1.Text += "\n支座 " + (++i).ToString() + "\t  " + (b.NodeIndex + 1).ToString() + "\t\t" + b.XY.ToString();

            }
            else if (tabControl1.SelectedIndex == 2)
            {
                richTextBox2.Clear();
                if (TS.FN == null || TS.RE == null)
                { richTextBox2.Text += "\t    [暂无数据]"; return; }

                richTextBox2.Text += "[杆件轴力]";
                int N1 = TS.FN.GetLength(0);
                for (int i = 0; i < N1; i++)
                    for (int j = i + 1; j < N1; j++)
                        if (Math.Abs(TS.FN[i, j]) > 0.5e-4)
                            richTextBox2.Text += "\n杆件[" + (i + 1).ToString() + "," + (j + 1).ToString() + "]\t"
                                + (TS.FN[i, j] < 0 ? "" : " ") + TS.FN[i, j].ToString("f4") + " kN";

                richTextBox2.Text += "\n\n[支座反力]";
                int N2 = TS.RE.GetLength(1);
                for (int i = 0; i < N2; i++)
                    richTextBox2.Text += "\n支座[" + (i + 1).ToString() + "]   X = "
                        + TS.RE[0, i].ToString("g4") + " kN\tY = " + TS.RE[1, i].ToString("g4") + " kN";

            }
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
                radioButton1.Select();
            else if (e.KeyCode == Keys.F2)
                radioButton2.Select();
            else if (e.KeyCode == Keys.F3)
                radioButton3.Select();
            else if (e.KeyCode == Keys.F4)
                radioButton4.Select();
            else if (e.KeyCode == Keys.Escape)
            { StartEdit = false; RefreshByTs(); }

        }

        #region Button_Click
        private void button1_Click(object sender, EventArgs e)//添加杆件
        {
            string sp = textBox1.Text.Replace(" ", ""), ep = textBox2.Text.Replace(" ", "");
            var p1 = sp.Split(new char[] { ',', '，' });
            var p2 = ep.Split(new char[] { ',', '，' });
            if (p1[0] == "")
            { MessageBox.Show("请输入要添加杆件的起点坐标"); textBox1.Focus(); return; }
            if (p2[0] == "")
            { MessageBox.Show("请输入要添加杆件的终点坐标"); textBox2.Focus(); return; }
            if (p1.Length != 2 || p2.Length != 2)
            { MessageBox.Show("输入格式不正确"); textBox1.Focus(); return; }

            float p11 = 0f, p12 = 0f, p21 = 0f, p22 = 0f;
            try
            {
                p11 = float.Parse(p1[0]); p12 = float.Parse(p1[1]);
                p21 = float.Parse(p2[0]); p22 = float.Parse(p2[1]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                textBox1.Focus(); return;
            }
            PointF spf = new PointF(p11, p12), epf = new PointF(p21, p22);
            if (spf == epf)
            { MessageBox.Show("终点与起点坐标不能相等！"); textBox2.Focus(); return; }

            if (TS.ContainsBar(TS.GetNodeIndex(spf), TS.GetNodeIndex(epf)))
            { MessageBox.Show("该杆件已存在!"); textBox1.Focus(); return; }

            DrawAndAddBar(spf, epf, true);
            //textBox1.Clear(); textBox2.Clear();
            textBox1.Text = textBox2.Text; textBox2.Clear(); textBox2.Focus();
        }
        private void button2_Click(object sender, EventArgs e)//删除杆件
        {
            string[] bar = textBox3.Text.Replace(" ", "").Split(new char[] { ',', '，' });
            if (bar.Length != 2)
            { MessageBox.Show("输入格式不正确！"); textBox3.Focus(); return; }
            int NInd1 = 0, NInd2 = 0;
            try
            {
                NInd1 = int.Parse(bar[0]);
                NInd2 = int.Parse(bar[1]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                textBox3.Focus(); return;
            }
            NInd1--; NInd2--;//转为从零开始的索引
            if (NInd1 == NInd2)
            { MessageBox.Show("杆件两端结点须不同！"); textBox3.Focus(); return; }
            if (!TS.ContainsBar(NInd1, NInd2))
            { MessageBox.Show("该杆件不存在！"); textBox3.Focus(); return; }

            TS.DelBar(NInd1, NInd2);
            RefreshByTs();
            textBox3.Clear();
        }
        private void button3_Click(object sender, EventArgs e)//添加荷载
        {
            double size = 0, direction = 0; int NI = 0;
            try
            {
                size = double.Parse(textBox4.Text);
                direction = double.Parse(textBox5.Text);
                NI = int.Parse(textBox6.Text) - 1;//转为从零开始的索引
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                textBox4.Focus(); return;
            }
            int FI = TS.AddForce(new NForce(size, direction * Math.PI / 180.0, NI));
            if (FI == -1)
            { MessageBox.Show("该位置不存在桁架结点！"); textBox6.Focus(); return; }

            //textBox4.Clear(); textBox5.Clear();
            textBox6.Clear();
            DrawForce(TS.Nodes[NI], Point.Empty, FI + 1, size, true);
            textBox6.Focus();
        }
        private void button4_Click(object sender, EventArgs e)//删除荷载
        {
            int FI;
            if (!int.TryParse(textBox7.Text, out FI))
            { MessageBox.Show("字符串格式不正确"); textBox7.Focus(); return; }
            FI--;//转为从零开始的索引
            if (!TS.DelForce(FI))
            { MessageBox.Show("该荷载不存在！"); textBox7.Focus(); return; }

            RefreshByTs();
            textBox7.Clear();
        }
        private void button5_Click(object sender, EventArgs e)//添加支座
        {
            int NI;
            if (!int.TryParse(textBox8.Text, out NI))
            { MessageBox.Show("字符串格式不正确"); textBox8.Focus(); return; }
            NI--;//转为从零开始的索引
            if (TS.ContainsBearing(NI) >= 0)
            { MessageBox.Show("该支座已存在！"); textBox8.Focus(); return; }
            if (comboBox1.SelectedIndex == -1)
            { MessageBox.Show("请选择约束条件！"); comboBox1.Focus(); return; }

            int BI = TS.AddBearing(new Bearing(NI, (Bearing.Constraint)comboBox1.SelectedIndex));
            if (BI == -1)
            { MessageBox.Show("该位置不存在桁架结点！"); textBox8.Focus(); return; }

            DrawBearing(TS.Nodes[NI], TS.Bears[BI].XY, true);
            textBox8.Clear(); comboBox1.SelectedIndex = -1;
        }
        private void button6_Click(object sender, EventArgs e)//删除支座
        {
            int NI;
            if (!int.TryParse(textBox8.Text, out NI))
            { MessageBox.Show("字符串格式不正确"); textBox8.Focus(); return; }
            NI--;//转为从零开始的索引
            if (TS.ContainsBearing(NI) == -1)
            { MessageBox.Show("该位置结点处无支座！"); textBox8.Focus(); return; }
            if (!TS.DelBearing(NI))
            { MessageBox.Show("该位置结点处无支座！"); textBox8.Focus(); return; }

            RefreshByTs();
            textBox8.Clear();
        }
        private void button9_Click(object sender, EventArgs e)//修改荷载
        {
            if (textBox7.Text == "")
            { MessageBox.Show("请输入要修改的荷载编号！"); return; }

            int FI = -1;
            if (!int.TryParse(textBox7.Text, out FI))
            { MessageBox.Show("请输入正确的荷载编号！"); return; }
            FI--;//转为从零开始的索引

            double size = 0, direction = 0; int NI = 0;
            try
            {
                if (textBox4.Text != "") size = double.Parse(textBox4.Text);
                else size = TS.Loads[FI].Size;

                if (textBox5.Text != "") direction = double.Parse(textBox5.Text) * Math.PI / 180.0;
                else direction = TS.Loads[FI].Direction;

                if (textBox6.Text != "") NI = int.Parse(textBox6.Text) - 1;
                else NI = TS.Loads[FI].NodeIndex;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                textBox7.Focus(); return;
            }

            TS.Loads[FI] = new NForce(size, direction, NI);
            textBox4.Clear(); textBox5.Clear(); textBox6.Clear(); textBox7.Clear();
            RefreshByTs();
        }

        private void button8_Click(object sender, EventArgs e)//求解内力
        {
            int sl = TS.Solve();
            switch (sl)
            {
                case -1: MessageBox.Show("请先添加节点\n(Error -1)"); break;
                case -2: MessageBox.Show("请先添加荷载\n(Error -2)"); break;
                case -3: MessageBox.Show("请先添加支座\n(Error -3)"); break;
                case -11: MessageBox.Show("求解错误，输入可能为机构\n(Error -11)"); break;
                case -12: MessageBox.Show("求解错误，输入可能为机构\n(Error -12)"); break;
                case -13: MessageBox.Show("求解错误，输入可能为机构\n(Error -13)"); break;
                case 0: MessageBox.Show("无内力"); break;
                default: break;
            }
            if (sl > 0)
            {
                groupBox5.Enabled = true;
                if (radioButton5.Checked)
                {
                    if (HaveDrawnFN)
                        RefreshByTs();
                    DrawFN(true, true);
                    HaveDrawnFN = true;
                }
                else
                    radioButton5.Select();

            }
            else
                groupBox5.Enabled = false;

        }

        #endregion

        #region 内力显示模式选择
        private void radioButton5_CheckedChanged(object sender, EventArgs e)//图形与数字
        {
            if (HaveDrawnFN)
                RefreshByTs();
            DrawFN(true, true);
            HaveDrawnFN = true;
        }
        private void radioButton6_CheckedChanged(object sender, EventArgs e)//仅图形
        {
            if (HaveDrawnFN)
                RefreshByTs();
            DrawFN(true, false);
            HaveDrawnFN = true;
        }
        private void radioButton7_CheckedChanged(object sender, EventArgs e)//仅数字
        {
            if (HaveDrawnFN)
                RefreshByTs();
            DrawFN(false, true);
            HaveDrawnFN = true;
        }
        private void radioButton8_CheckedChanged(object sender, EventArgs e)//隐藏
        {
            if (HaveDrawnFN)
            {
                RefreshByTs();
                HaveDrawnFN = false;
            }
        }

        #endregion

        #region 编辑模式选择
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            EM = EditMode.GanJian;
            StartEdit = false;
        }
        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            EM = EditMode.HeZai;
            pictureBox1.Image = currentBmp;
            StartEdit = false;
        }
        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            EM = EditMode.ZhiZuo;
            pictureBox1.Image = currentBmp;
            StartEdit = false;
        }
        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            EM = EditMode.None;
            StartEdit = false;
            pictureBox1.Image = currentBmp;
        }
        #endregion
        
        private void 打开文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TS.OpenFile() == true)
                RefreshByTs();
        }
        private void 另存为ToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TS.SaveFile();
        }
        private void 清空绘制区ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定删除所有杆件，荷载和支座？", "确认", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;
            TS.ClearAll();
            destBmp.Dispose(); currentBmp.Dispose();
            currentBmp = (Bitmap)originBmp.Clone();
            destBmp = (Bitmap)currentBmp.Clone();
            pictureBox1.Image = currentBmp;
            radioButton1.Select();
            groupBox5.Enabled = false;
            HaveDrawnFN = false;
        }
        private void 设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingForm.UnitSize = this.UnitSize;
            SettingForm.EA = TS.EA;
            SettingForm uff = new SettingForm();
            uff.ShowDialog();
            this.UnitSize = SettingForm.UnitSize;
            TS.EA = SettingForm.EA;
        }

    }
}
