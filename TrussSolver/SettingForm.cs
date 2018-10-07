using System;
using System.Windows.Forms;

namespace TrussSolver
{
    public partial class SettingForm : Form
    {
        public SettingForm()
        {
            InitializeComponent();
            textBox1.Text = UnitSize.ToString();
            textBox2.Text = EA.ToString();
        }
        public static double UnitSize;
        public static double EA;
        
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                UnitSize = double.Parse(textBox1.Text);
                EA = double.Parse(textBox2.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            Close();

        }
    }
}
