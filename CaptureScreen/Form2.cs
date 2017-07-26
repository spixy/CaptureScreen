using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaptureScreen
{
    public partial class Form2 : Form
    {
        Size OLDsize;
        float ratio = 1;

        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                Form1.NewSize.Width = (int)numericUpDown1.Value;
                Form1.NewSize.Height = (int)numericUpDown2.Value;
            }
            else if (radioButton2.Checked)
            {
                Form1.NewSize.Width = (int)(numericUpDown1.Value * OLDsize.Width / 100);
                Form1.NewSize.Height = (int)(numericUpDown2.Value * OLDsize.Height / 100);
            }
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            OLDsize = Form1.NewSize;
            numericUpDown1.Value = Form1.NewSize.Width;
            numericUpDown2.Value = Form1.NewSize.Height;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                numericUpDown2.ValueChanged -= numericUpDown2_ValueChanged;
                if (radioButton2.Checked) numericUpDown2.Value = numericUpDown1.Value;
                else numericUpDown2.Value = numericUpDown1.Value / (decimal)ratio;
                numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
            }
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                numericUpDown1.ValueChanged -= numericUpDown1_ValueChanged;
                if (radioButton1.Checked) numericUpDown1.Value = numericUpDown2.Value;
                else numericUpDown1.Value = numericUpDown2.Value * (decimal)ratio;
                numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                ratio = Form1.NewSize.Width / (float)Form1.NewSize.Height;
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (!radioButton1.Checked) return;

            numericUpDown1.ValueChanged -= numericUpDown1_ValueChanged;
            numericUpDown2.ValueChanged -= numericUpDown2_ValueChanged;

            numericUpDown1.Value = OLDsize.Width * numericUpDown1.Value / 100;
            numericUpDown2.Value = OLDsize.Height * numericUpDown2.Value / 100;

            numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
            numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (!radioButton2.Checked) return;
            //OLDsize = new System.Drawing.Size((int)numericUpDown1.Value, (int)numericUpDown2.Value);

            numericUpDown1.ValueChanged -= numericUpDown1_ValueChanged;
            numericUpDown2.ValueChanged -= numericUpDown2_ValueChanged;

            numericUpDown1.Value = 100 * numericUpDown1.Value / Form1.NewSize.Width;
            numericUpDown2.Value = 100 * numericUpDown2.Value / Form1.NewSize.Height;

            numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
            numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
        }
    }
}
