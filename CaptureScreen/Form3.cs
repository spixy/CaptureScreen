using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace CaptureScreen
{
    public partial class Form3 : Form
    {
        public Form3()
        {
            InitializeComponent();
        }

        private void Form3_Load(object sender, System.EventArgs e)
        {
            comboBox1.Items.AddRange(Form1.AudioSource);
            comboBox1.SelectedIndex = Form1.SelectedAudioSource + 1;
            comboBox2.SelectedIndex = comboBox2.Items.IndexOf(Form1.ImageExt);
            comboBox3.SelectedIndex = comboBox3.Items.IndexOf(Form1.VideoExt);
            comboBox4.SelectedIndex = comboBox4.Items.IndexOf(Form1.ABits.ToString());
            comboBox5.SelectedText = Form1.ASample.ToString();

            checkBox2.Checked = Form1.TrayIcon;
            checkBox3.Checked = Form1.Debug;
            checkBox4.Checked = Form1.MouseCursor;
            checkBox5.Checked = Form1.ForcedCapture;
            checkBox6.Checked = Form1.PrintScreenKey;
            checkBox7.Checked = Form1.AutoName;
            checkBox8.Checked = Form1.AppFilter;
            checkBox9.Checked = Form1.UseFixedSize;
            checkBox10.Checked = Form1.OpenFolder;
            checkBox11.Checked = Form1.AutoUpdate;

            numericUpDown1.Value = Form1.FPS;
            numericUpDown2.Value = Form1.FixedSize.Width;
            numericUpDown3.Value = Form1.FixedSize.Height;
            textBox1.Text = Form1.ImageDir;
            textBox2.Text = Form1.VideoDir;

            MaximumSize = Size;
            MinimumSize = Size;

            try
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\\");
                if (rk.GetValue("CaptureScreen") != null) checkBox1.Checked = true;
            }
            catch (Exception ex)
            {
                Form1.LogError(ex);
            }
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (checkBox1.Checked)
            {
                try
                {
                    rk.SetValue("CaptureScreen", Application.ExecutablePath.ToString() + " /tray");
                }
                catch (Exception ex)
                {
                    Form1.LogError(ex);
                }
            }
            else
            {
                try
                {
                    rk.DeleteValue("CaptureScreen", false);
                }
                catch (Exception ex)
                {
                    Form1.LogError(ex);
                }
            }
            
            Form1.TrayIcon = checkBox2.Checked;
            Form1.Debug = checkBox3.Checked;
            Form1.MouseCursor = checkBox4.Checked;
            Form1.ForcedCapture = checkBox5.Checked;
            Form1.PrintScreenKey = checkBox6.Checked;
            Form1.AutoName = checkBox7.Checked;
            Form1.AppFilter = checkBox8.Checked;
            Form1.UseFixedSize = checkBox9.Checked;
            Form1.OpenFolder = checkBox10.Checked;
            Form1.AutoUpdate = checkBox11.Checked;

            Form1.FPS = (short)numericUpDown1.Value;
            Form1.FixedSize.Width = (int)numericUpDown2.Value;
            Form1.FixedSize.Height = (int)numericUpDown3.Value;
            Form1.SelectedAudioSource = (short)(comboBox1.SelectedIndex -1);
            Form1.ImageDir = textBox1.Text;
            Form1.VideoDir = textBox2.Text;
            Form1.ImageExt = comboBox2.SelectedItem.ToString();
            Form1.VideoExt = comboBox3.SelectedItem.ToString();
            Form1.ABits = Convert.ToInt16(comboBox4.SelectedItem.ToString());
            Form1.ASample = Convert.ToInt32(comboBox5.Text);
        }

        private void button3_Click(object sender, System.EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox1.Text = folderBrowserDialog1.SelectedPath;
        }

        private void button4_Click(object sender, System.EventArgs e)
        {
            Process.Start("explorer.exe", textBox1.Text);
        }

        private void button6_Click(object sender, System.EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                textBox2.Text = folderBrowserDialog1.SelectedPath;
        }

        private void button5_Click(object sender, System.EventArgs e)
        {
            Process.Start("explorer.exe", textBox2.Text);
        }

        private void checkBox9_CheckedChanged(object sender, System.EventArgs e)
        {
            numericUpDown2.Enabled = checkBox9.Checked;
            numericUpDown3.Enabled = checkBox9.Checked;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox4.Enabled = (comboBox1.SelectedIndex != 0);
            comboBox5.Enabled = (comboBox1.SelectedIndex != 0);
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            comboBox2.Enabled = checkBox7.Checked;
            comboBox3.Enabled = checkBox7.Checked;
        }
    }
}
