using MultiMedia;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CaptureScreen
{
    public partial class Form1 : Form
    {
        public enum VirtualKeyStates : int
        {
            SHIFT = 0x10,
            CTRL = 0x11,
            ALT = 0x12,
            VK_SNAPSHOT = 0x2C,

            A = 0x41,
            B,
            C,
            D,
            E,
            F,
            G,
            H,
            I,
            J,
            K,
            L,
            M,
            N,
            O,
            P,
            Q,
            R,
            S,
            T,
            U,
            V,
            W,
            X,
            Y,
            Z,

            F1 = 0x70,
            F2,
            F3,
            F4,
            F5,
            F6,
            F7,
            F8,
            F9,
            F10,
            F11,
            F12
        }
        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);
        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);
        [DllImport("user32.dll")]
        public static extern short GetKeyState(VirtualKeyStates nVirtKey);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private Stopwatch sw = new Stopwatch();
        private CWaveRecord m_Wav;
        private SpyWindow spyWindow = new SpyWindow();
        private ImageList il = new ImageList();
        private System.Collections.Generic.List<IntPtr> Removed = new System.Collections.Generic.List<IntPtr>();

        string Config = "settings.ini";
        string[] Exceptions = { "sidebar", "Rainmeter", "RocketDock", "Xfire", "Client", "SignalIslandUi", "ACMON", "StikyNot", "AmIcoSinglun64", "ATKOSD2", "Rainlendar2", "bsplayer", "SystemExplorer", "Soluto", "mbam", "SonicMaster", "KMCONFIG", "csrss", "HDDScan", "uniws" };
        string[] MultipleWindows = { "explorer", "firefox", "iexplore" };
        bool forced_realtime = false;
        long captureCount;
        short threadCount = 1;
        bool mt = false;
        object key = new object();
        Size defaultSize;
        const Int32 CURSOR_SHOWING = 0x00000001;

        public static bool OpenFolder = false;
        public static bool TrayIcon = true;
        public static bool Debug = false;
        public static bool AutoName = false;
        public static bool AppFilter = true;
        public static bool UseFixedSize = false;
        public static bool MouseCursor = true;
        public static bool ForcedCapture = true;
        public static bool PrintScreenKey = true;
        public static bool AutoUpdate = true;
        public static short FPS = 20;
        public static short SelectedAudioSource = -1;
        public static short ABits = 16;
        public static int ASample = 44100;
        public static Size FixedSize = new Size(1, 1);
        public static Size NewSize = new Size(1, 1);
        public static string[] AudioSource;
        public static string ImageExt = "JPG";
        public static string VideoExt = "AVI";
        public static string ImageDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); //Environment.GetEnvironmentVariable("userprofile") + "\\Pictures\\";
        public static string VideoDir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos); //Environment.GetEnvironmentVariable("userprofile") + "\\Videos\\";

        string tempCapturePath = Environment.GetEnvironmentVariable("TEMP") + "\\captured";
        string tempImage = Environment.GetEnvironmentVariable("TEMP") + "\\prtsc.png";
        string MSPaintPath = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\mspaint.exe";
        string FFmpegPath = Application.StartupPath + "\\ffmpeg.exe";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            il.ImageSize = new System.Drawing.Size(24, 24);
            il.ColorDepth = ColorDepth.Depth32Bit;
            listView1.SmallImageList = il;
            listView1.LargeImageList = il;
            listView1.StateImageList = il;
            defaultSize = pictureBox1.Size;
            kryptonButton3.Image = saveToolStripMenuItem.Image;
            spyWindow._pictureBox = pictureBox2;
            spyWindow._pictureBox2 = pictureBox1;
            spyWindow.text = this.Text;

            WaveInCaps[] devs = CWaveRecord.GetDevices();
            AudioSource = new string[devs.Length];
            for (int i = 0; i < devs.Length; i++)
                AudioSource[i] = devs[i].szPname;

            Icon ico = Icon.ExtractAssociatedIcon(MSPaintPath);
            openInMSPaintToolStripMenuItem.Image = ico.ToBitmap();
            ReadCFG();

            if (AutoUpdate)
            {
                Thread upd = new Thread(update);
                upd.IsBackground = true;
                upd.Start(true);
            }

            if (!File.Exists(FFmpegPath))
                streamingToolStripMenuItem.Visible = false;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            string[] Args = Environment.GetCommandLineArgs();

            foreach (string str in Args)
            {
                if (str.ToLower() == "/tray") WindowState = FormWindowState.Minimized;
                if (str.ToLower() == "/nofilter") AppFilter = false;
                if (str.ToLower() == "/debug") Debug = true;
                if (str.ToLower() == "/mt") mt = true;
                if (str.ToLower() == "/noupdate") AutoUpdate = false;
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (!TrayIcon) return;

            if (this.WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                Visible = false;
                this.Hide();
            }
            else if (this.WindowState == FormWindowState.Normal)
            {
                this.Show();
                Visible = true;
                notifyIcon1.Visible = false;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            using (StreamWriter file = new StreamWriter(Config, false))
            {
                if (Debug && !isStringInArray(Environment.GetCommandLineArgs(), "/debug", false)) file.WriteLine("debug 1"); else file.WriteLine("debug 0");
                if (ForcedCapture) file.WriteLine("force 1"); else file.WriteLine("force 0");
                if (findActiveWindowToolStripMenuItem.Checked) file.WriteLine("active window 1"); else file.WriteLine("active window 0");
                if (PrintScreenKey) file.WriteLine("printscreen 1"); else file.WriteLine("printscreen 0");
                if (TrayIcon) file.WriteLine("tray 1"); else file.WriteLine("tray 0");
                if (realtimeCapturingToolStripMenuItem.Checked && !forced_realtime) file.WriteLine("realtime 1"); else file.WriteLine("realtime 0");
                if (MouseCursor) file.WriteLine("mouse 1"); else file.WriteLine("mouse 0");
                if (AutoName) file.WriteLine("autoname 1"); else file.WriteLine("autoname 0");
                if (OpenFolder) file.WriteLine("open folder 1"); else file.WriteLine("open folder 0");
                if (AutoUpdate) file.WriteLine("autoupdate 1"); else file.WriteLine("autoupdate 0");
                if (UseFixedSize) file.WriteLine("FixedSize 1"); else file.WriteLine("FixedSize 0");

                file.WriteLine("FixedW " + FixedSize.Width);
                file.WriteLine("FixedH " + FixedSize.Height);
                file.WriteLine("FPS " + FPS);
                file.WriteLine("Audio " + SelectedAudioSource);
                file.WriteLine("Bits " + ABits);
                file.WriteLine("Sampling " + ASample);
                file.WriteLine("Image " + ImageExt);
                file.WriteLine("Video " + VideoExt);
                file.WriteLine("ImageDir " + ImageDir);
                file.WriteLine("VideoDir " + VideoDir);
                file.WriteLine("Threads " + threadCount);
            }

            try
            {
                if (File.Exists(tempImage))
                    File.Delete(tempImage);

                if (Directory.Exists(tempCapturePath))
                    Directory.Delete(tempCapturePath, true);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            listView1.Size = new Size(listView1.Size.Width, Size.Height - 87);
            listView1.Location = new Point(Size.Width - listView1.Size.Width - 26, listView1.Location.Y);
            pictureBox1.Size = new Size(Size.Width - 232, Size.Height - 143);
            panel1.Location = new Point(Size.Width - 554, panel1.Location.Y);
        }

        private void ReadCFG()
        {
            bool Realtime = false;

            if (!File.Exists(Config))
            {
                LogEvent("Config file not found.");
                return;
            }

            string[] lines = File.ReadAllLines(Config);
            foreach (string line in lines)
            {
                if (line.ToLower().Contains("force 0")) ForcedCapture = false;
                if (line.ToLower().Contains("active window 1")) findActiveWindowToolStripMenuItem.Checked = true;
                if (line.ToLower().Contains("printscreen 0")) PrintScreenKey = false;
                if (line.ToLower().Contains("tray 0")) TrayIcon = false;
                if (line.ToLower().Contains("realtime 1")) Realtime = true;
                if (line.ToLower().Contains("mouse 0")) MouseCursor = false;
                if (line.ToLower().Contains("debug 1")) Debug = true;
                if (line.ToLower().Contains("autoname 1")) AutoName = true;
                if (line.ToLower().Contains("autoupdate 0")) AutoUpdate = false;
                if (line.ToLower().Contains("fixedsize 1")) UseFixedSize = true;
                if (line.ToLower().Contains("open folder 1")) OpenFolder = true;               
                if (line.ToLower().Contains("image ")) ImageExt = line.ToLower().Replace("image ", "").ToUpper();
                if (line.ToLower().Contains("video ")) VideoExt = line.ToLower().Replace("video ", "").ToUpper();
                if (line.ToLower().Contains("imagedir "))
                {
                    ImageDir = line.ToLower().Replace("imagedir ", "");
                    if (ImageDir.EndsWith("\\")) ImageDir = ImageDir.Remove(ImageDir.Length - 1);
                }
                if (line.ToLower().Contains("videodir "))
                {
                    VideoDir = line.ToLower().Replace("videodir ", "");
                    if (VideoDir.EndsWith("\\")) VideoDir = VideoDir.Remove(VideoDir.Length - 1);
                }
                if (line.ToLower().Contains("fixedw "))
                    try
                    {
                        FixedSize.Width = Convert.ToInt32(line.ToLower().Replace("fixedw ", ""));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("fixedh "))
                    try
                    {
                        FixedSize.Height = Convert.ToInt32(line.ToLower().Replace("fixedh ", ""));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("fps "))
                    try
                    {
                        FPS = Convert.ToInt16(line.ToLower().Replace("fps ", ""));
                        if (FPS < 1 || FPS > 1000) FPS = 20;
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("audio "))
                    try
                    {
                        SelectedAudioSource = Convert.ToInt16(line.ToLower().Replace("audio ", ""));
                        if (SelectedAudioSource > -1) m_Wav = new CWaveRecord(SelectedAudioSource);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("bits "))
                    try
                    {
                        ABits = Convert.ToInt16(line.ToLower().Replace("bits ", ""));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("sampling "))
                    try
                    {
                        ASample = Convert.ToInt32(line.ToLower().Replace("sampling ", ""));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                if (line.ToLower().Contains("threads "))
                    try
                    {
                        threadCount = Convert.ToInt16(line.ToLower().Replace("threads ", ""));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
            }

            if (Realtime)
                realtimeCapturingToolStripMenuItem.PerformClick();
        }

        /*=================================================================================================================*/

        private void timer1_Tick(object sender, EventArgs e)
        {
            Process[] procs = Process.GetProcesses();

            for (int i=0; i < procs.Length; i++)
            {
                try
                {
                    if (procs[i].MainWindowHandle != IntPtr.Zero)
                    {
                        if (listView1.Items.ContainsKey(procs[i].Id.ToString()))
                        {
                            if (isStringInArray(MultipleWindows, procs[i].ProcessName, true))
                            {
                                bool found = false;

                                for (int j = 0; j < listView1.Items.Count; j++)
                                    if ((IntPtr)listView1.Items[j].Tag == procs[i].MainWindowHandle)
                                        found = true;

                                if (!found && !Removed.Contains(procs[i].MainWindowHandle))
                                {
                                    listView1.Items.Add(procs[i].ProcessName);
                                    listView1.Items[listView1.Items.Count - 1].Name = procs[i].Id.ToString();
                                    listView1.Items[listView1.Items.Count - 1].Tag = procs[i].MainWindowHandle;
                                    try
                                    {
                                        il.Images.Add(Icon.ExtractAssociatedIcon(procs[i].MainModule.FileName));
                                    }
                                    catch
                                    {
                                        il.Images.Add(CaptureScreen.Properties.Resources.App);
                                    }
                                    listView1.Items[listView1.Items.Count - 1].ImageIndex = il.Images.Count - 1;
                                    listView1.Items[listView1.Items.Count - 1].ToolTipText = procs[i].MainWindowTitle;
                                }
                            }
                            else listView1.Items[listView1.Items.IndexOfKey(procs[i].Id.ToString())].Tag = procs[i].MainWindowHandle;
                        }
                        else if (!Removed.Contains(procs[i].MainWindowHandle))
                        {
                            if (AppFilter && isStringInArray(Exceptions, procs[i].ProcessName, true)) continue;

                            listView1.Items.Add(procs[i].ProcessName);
                            listView1.Items[listView1.Items.Count - 1].Name = procs[i].Id.ToString();
                            listView1.Items[listView1.Items.Count - 1].Tag = procs[i].MainWindowHandle;
                            try
                            {
                                il.Images.Add(Icon.ExtractAssociatedIcon(procs[i].MainModule.FileName));
                            }
                            catch
                            {
                                il.Images.Add(CaptureScreen.Properties.Resources.App);
                            }
                            listView1.Items[listView1.Items.Count - 1].ImageIndex = il.Images.Count - 1;
                            listView1.Items[listView1.Items.Count - 1].ToolTipText = procs[i].MainWindowTitle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }

            for (int i = 0; i < listView1.Items.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < procs.Length; j++)
                    if (procs[j].Id.ToString() == listView1.Items[i].Name)
                    {
                        if (isStringInArray(MultipleWindows, procs[j].ProcessName, true)) found = true;
                        else if (procs[j].MainWindowHandle == (IntPtr)listView1.Items[i].Tag) found = true;
                        else found = false;
                    }
                if (!found)
                {
                    listView1.Items[i].Remove();
                    break;
                }

                if (startToolStripMenuItem.Enabled) return;
                label1.Text = sw.Elapsed.Hours + ":" + String.Format("{0:00}", sw.Elapsed.Minutes) + ":" + String.Format("{0:00}", sw.Elapsed.Seconds);
            }

            if (findActiveWindowToolStripMenuItem.Checked)
            {
                FindActiveWindow();
            }

            procs = null;
        }

        private void FindActiveWindow()
        {
            IntPtr hWnd = GetForegroundWindow();
            int a = -1;
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (hWnd == (IntPtr)listView1.Items[i].Tag)
                    a = i;
            }

            if (a != -1)
            {
                if (!listView1.Items[a].Selected)
                {
                    listView1.SelectedItems.Clear();
                    listView1.Items[a].Selected = true;
                }
            }
            else listView1.SelectedItems.Clear();
        }

        private static bool isStringInArray(string[] strArray, string key, bool CaseSensitive)
        {
            if (CaseSensitive)
            {
                for (int i = 0; i <= strArray.Length - 1; i++)
                    if (strArray[i] == key) return true;
            }
            else
            {
                for (int i = 0; i <= strArray.Length - 1; i++)
                    if (strArray[i].ToLower() == key.ToLower()) return true;
            }
            return false;
        }

        private Bitmap GetScreen()
        {
            Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

                if (MouseCursor)
                {
                    CURSORINFO pci;
                    pci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));

                    if (GetCursorInfo(out pci) && pci.flags == CURSOR_SHOWING)
                    {
                        DrawIcon(g.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                        g.ReleaseHdc();
                    }
                }
            }
            if (UseFixedSize)
                return new Bitmap(bmp, FixedSize);
            else
                return bmp;
        }

        private void kryptonButton1_Click(object sender, EventArgs e)
        {
            try
            {
                pictureBox1.Image = GetScreen();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void kryptonButton2_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0) return;

            try
            {
                if (ForcedCapture && (GetWindowLong((IntPtr)listView1.SelectedItems[0].Tag, (-16)) & 0x20000000) == 0x20000000)
                {
                    if (e == null && (realtimeCapturingToolStripMenuItem.Checked || ForcedCapture)) return;
                    ShowWindow((IntPtr)listView1.SelectedItems[0].Tag, 1);
                    Thread.Sleep(500);
                    if (UseFixedSize) pictureBox1.Image = new Bitmap(ScreenCapturing.GetWindowCaptureAsBitmap((IntPtr)listView1.SelectedItems[0].Tag), FixedSize);//new Bitmap(ScreenCapture.CaptureWindow((IntPtr)listView1.SelectedItems[0].Tag), FixedSize);
                    else pictureBox1.Image = ScreenCapturing.GetWindowCaptureAsBitmap((IntPtr)listView1.SelectedItems[0].Tag);
                    ShowWindow((IntPtr)listView1.SelectedItems[0].Tag, 2);
                }
                else
                {
                    if (UseFixedSize) pictureBox1.Image = new Bitmap(ScreenCapturing.GetWindowCaptureAsBitmap((IntPtr)listView1.SelectedItems[0].Tag), FixedSize);
                    else pictureBox1.Image = ScreenCapturing.GetWindowCaptureAsBitmap((IntPtr)listView1.SelectedItems[0].Tag);
                }

                if ((pictureBox1.Image == null || (pictureBox1.Image.Width == 1 && pictureBox1.Image.Height == 1)) && Exceptions.Length > 0)
                    removeItemToolStripMenuItem.PerformClick(); //listView1.SelectedItems[0].Remove();
            }
            catch (Exception ex)
            {
                if (isStringInArray(MultipleWindows, listView1.SelectedItems[0].Text, true))
                    listView1.Items.RemoveAt(listView1.SelectedIndices[0]);
                LogError(ex);
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                kryptonButton2.PerformClick();
                kryptonButton2.Enabled = true;
            }
            else kryptonButton2.Enabled = false;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AutoName)
            {
                string str = "";

                if (listView1.SelectedItems.Count > 0)
                    str = ImageDir + "\\" + listView1.SelectedItems[0].Text + "_" + DateTimeString() + "." + ImageExt.ToLower();
                else
                    str = ImageDir + "\\desktop_" + DateTimeString() + "." + ImageExt.ToLower();
                try
                {
                    pictureBox1.Image.Save(str);
                    if (OpenFolder) Process.Start("explorer.exe", "/select, " + str);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
            else if (saveFileDialog1.FileName == String.Empty)
            {
                saveAsToolStripMenuItem.PerformClick();
            }
            else if (pictureBox1.Image != null)
            {
                try
                {
                    SaveToFile(pictureBox1.Image, saveFileDialog1.FileName);
                    if (OpenFolder)
                        Process.Start("explorer.exe", saveFileDialog1.FileName);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = String.Empty;
            saveFileDialog1.Filter = "BMP|*.bmp|JPG|*.jpg|PNG|*.png|GIF|*.gif|ICO|*.ico|TIFF|*.tiff|EMF|*.emf|Exif|*.exif|WMF|*.wmf|All files|*.*";

            if (pictureBox1.Image != null && saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    SaveToFile(pictureBox1.Image, saveFileDialog1.FileName);

                    if (OpenFolder)
                        Process.Start("explorer.exe", "/select, " + saveFileDialog1.FileName);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
        }

        private void resizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;

            NewSize = pictureBox1.Image.Size;
            Form2 form2 = new Form2();

            if (form2.ShowDialog() == DialogResult.OK)
            {
                Image newBitmap = (Image)pictureBox1.Image.Clone();
                pictureBox1.Image = new Bitmap(newBitmap, new Size(NewSize.Width, NewSize.Height));
            }
            form2.Dispose();
        }

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null && printDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                printDocument1.Print();
        }

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
                printPreviewDialog1.ShowDialog();
        }

        private void printDocument1_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            e.Graphics.DrawImage(pictureBox1.Image, 0, 0);
        }

        void OnQueryPageSettings(object obj, System.Drawing.Printing.QueryPageSettingsEventArgs e)
        {
            if (e.PageSettings.PrinterSettings.LandscapeAngle != 0)
                e.PageSettings.Landscape = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private static void SaveToFile(Image image, string fileName, ImageFormat format)
        {
            DirectoryInfo dir = new FileInfo(fileName).Directory;

            if (!dir.Exists)
                dir.Create();

            SaveToFileFast(image, fileName, format);
        }

        private static void SaveToFile(Image image, string fileName)
        {
            ImageFormat format;

            if (fileName.ToLower().EndsWith(".jpg") || fileName.ToLower().EndsWith(".jpeg")) format = ImageFormat.Jpeg;
            else if (fileName.ToLower().EndsWith(".bmp")) format = ImageFormat.Bmp;
            else if (fileName.ToLower().EndsWith(".png")) format = ImageFormat.Png;
            else if (fileName.ToLower().EndsWith(".gif")) format = ImageFormat.Gif;
            else if (fileName.ToLower().EndsWith(".tiff")) format = ImageFormat.Tiff;
            else if (fileName.ToLower().EndsWith(".ico")) format = ImageFormat.Icon;
            else if (fileName.ToLower().EndsWith(".emf")) format = ImageFormat.Emf;
            else if (fileName.ToLower().EndsWith(".wmf")) format = ImageFormat.Wmf;
            else throw new Exception("Illegal image extension");

            SaveToFile(image, fileName, format);
        }

        private static void SaveToFileFast(Image image, string fileName, ImageFormat format)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    image.Save(memory, format);
                    byte[] bytes = memory.ToArray();
                    fs.Write(bytes, 0, bytes.Length);
                }
            }
        }

        public static string DateTimeString()
        {
            return DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day + "_"
                + String.Format("{0:00}_{1:00}_{2:00}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        private void kryptonButton3_Click(object sender, EventArgs e)
        {
            if (AutoName)
            {
                string str;

                if (listView1.SelectedItems.Count > 0)
                    str = ImageDir + "\\" + listView1.SelectedItems[0].Text + "_" + DateTimeString() + "." + ImageExt.ToLower();
                else
                    str = ImageDir + "\\desktop_" + DateTimeString() + "." + ImageExt.ToLower();

                try
                {
                    SaveToFile(pictureBox1.Image, str);
                    if (OpenFolder) Process.Start("explorer.exe", "/select, " + str);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
            else saveAsToolStripMenuItem.PerformClick();
        }

        private static bool IsKeyPressed(VirtualKeyStates testKey)
        {
            switch (GetKeyState(testKey))
            {
                case 0: // Not pressed and not toggled on.
                    return false;

                case 1: // Not pressed, but toggled on
                    return false;

                default: // Pressed (and may be toggled on)
                    return true;
            }
        }

        public static void LogError(Exception ex)
        {
            if (Debug)
                using (StreamWriter file = new StreamWriter("DebugInfo.txt", true))
                    file.WriteLine(DateTime.Now.Day + "." + DateTime.Now.Month + "." + DateTime.Now.Year + " "
                        + DateTime.Now.Hour + ":" + String.Format("{0:00}", DateTime.Now.Minute) + ":" + String.Format("{0:00}", DateTime.Now.Second) + " - "
                        + ex.Message + "\n" + ex.StackTrace);
        }

        public static void LogEvent(string msg)
        {
            if (Debug)
                using (StreamWriter file = new StreamWriter("DebugInfo.txt", true))
                    file.WriteLine(DateTime.Now.Day + "." + DateTime.Now.Month + "." + DateTime.Now.Year + " "
                        + DateTime.Now.Hour + ":" + String.Format("{0:00}", DateTime.Now.Minute) + ":" + String.Format("{0:00}", DateTime.Now.Second) + " - "
                        + msg);
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (IsKeyPressed(VirtualKeyStates.VK_SNAPSHOT) && PrintScreenKey)
            {
                if (listView1.SelectedIndices.Count == 0)
                    kryptonButton1.PerformClick();
                else
                    kryptonButton2.PerformClick();
            }
            else if (IsKeyPressed(VirtualKeyStates.F10))
            {
                startToolStripMenuItem.PerformClick();
            }
            else if (IsKeyPressed(VirtualKeyStates.F11))
            {
                pauseToolStripMenuItem.PerformClick();
            }
            else if (IsKeyPressed(VirtualKeyStates.F12))
            {
                stopToolStripMenuItem.PerformClick();
            }
            else if (IsKeyPressed(VirtualKeyStates.K) && IsKeyPressed(VirtualKeyStates.CTRL))
            {
                try
                {
                    string dir = ImageDir + "\\desktop_" + DateTimeString() + "." + ImageExt.ToLower();
                    SaveToFile(GetScreen(), dir);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
            else if (IsKeyPressed(VirtualKeyStates.J) && IsKeyPressed(VirtualKeyStates.CTRL))
            {
                try
                {
                    FindActiveWindow();
                    Bitmap bmp = new Bitmap(ScreenCapturing.GetWindowCaptureAsBitmap((IntPtr)listView1.SelectedItems[0].Tag), FixedSize);
                    string dir = ImageDir + "\\" + listView1.SelectedItems[0].Text + "_" + DateTimeString() + "." + ImageExt.ToLower();
                    SaveToFile(bmp, dir);
                }
                catch (Exception ex)
                {
                    Text = "CaptureScreen (" + ex.Message + ")";
                    LogError(ex);
                }
            }
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread upd = new Thread(update);
            upd.IsBackground = true;
            upd.Start(false);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 aboutbox = new AboutBox1();
            aboutbox.ShowDialog();
            aboutbox.Dispose();
        }

        private void startUpdate()
        {
            try
            {
                string bits = (Environment.Is64BitOperatingSystem && Environment.Is64BitProcess) ? "64" : "32";
                Process.Start("Updater.exe", "/updr http://www.zone-x.tym.sk/download/CaptureScreen" + bits + ".exe");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void update(object args)
        {
            bool autoupdate = (bool)args;

            try
            {
                System.Net.WebClient update = new System.Net.WebClient();
                string new_ver = update.DownloadString("http://www.zone-x.tym.sk/update/PrtScr.upd");
                update.Dispose();

                if (new_ver == Assembly.GetExecutingAssembly().GetName().Version.ToString())
                {
                    if (!autoupdate)
                        MessageBox.Show("   There is no new version available.", "Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (autoupdate)
                    {
                        startUpdate();
                    }
                    else if (MessageBox.Show("   New version available.\n   Proceed to download?", "Updater", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        startUpdate();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void openInMSPaintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null)
                return;

            try
            {
                SaveToFile(pictureBox1.Image, tempImage);

                Process p = new Process();
                p.StartInfo.FileName = MSPaintPath;
                p.StartInfo.Arguments = tempImage;
                p.Start();
                p.Dispose();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }  
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            kryptonButton2_Click(sender, null);
        }

        private void refreshProcessListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
        }

        void T2_Elapsed(object sender)
        {
            int time = (int)(1000f / (float)FPS);
            int selectedItem = 0;
            CURSORINFO pci;
            Graphics g;
            IntPtr hWnd = (IntPtr)0;
            Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            Stopwatch sw2 = new Stopwatch();
            sw2.Start();

            if (sender != null) time = (int)sender;
            pci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));

            while (realtimeCapturingToolStripMenuItem.Checked)
            {
                bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
                sw2.Reset();
                sw2.Start();
                listView1.Invoke((MethodInvoker)delegate {
                    selectedItem = listView1.SelectedItems.Count;
                    if (selectedItem > 0) hWnd = (IntPtr)listView1.SelectedItems[0].Tag;
                });

                if (selectedItem == 0)
                {
                    using (g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);
                        if (MouseCursor && GetCursorInfo(out pci) && pci.flags == CURSOR_SHOWING)
                        {
                            DrawIcon(g.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                            g.ReleaseHdc();
                        }
                    }
                    if (UseFixedSize)
                        bmp = new Bitmap(bmp, FixedSize);
                }
                else
                {
                    if (ForcedCapture)
                    {
                        if ((GetWindowLong(hWnd, (-16)) & 0x20000000) == 0x20000000)
                            ShowWindow(hWnd, 1);
                    }
                    if (UseFixedSize)
                        bmp = new Bitmap(ScreenCapturing.GetWindowCaptureAsBitmap(hWnd), FixedSize);
                    else
                        bmp = (Bitmap)ScreenCapturing.GetWindowCaptureAsBitmap(hWnd);
                }

                if (!startToolStripMenuItem.Enabled)
                {
                    captureCount++;
                    //Bitmap bmp2 = new Bitmap(bmp);
                    Thread T3 = new Thread(() => SaveToFileFast(bmp, tempCapturePath + "\\img" + captureCount + ".jpg", ImageFormat.Jpeg) /*bmp2.Save(tempCapturePath + "\\img" + captureCount + ".jpg", ImageFormat.Jpeg)*/);
                    T3.IsBackground = true;
                    T3.Start();
                }

                pictureBox1.Image = bmp;

                //Text = (time - (int)sw2.ElapsedMilliseconds) + " " + sw2.ElapsedMilliseconds;
                if (sw2.ElapsedMilliseconds < time)
                    Thread.Sleep(time - (int)sw2.ElapsedMilliseconds);

                while (startToolStripMenuItem.Enabled && stopToolStripMenuItem.Enabled)
                    Thread.Sleep(time); // recording paused
            }
        }

        private void realtimeCapturingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (realtimeCapturingToolStripMenuItem.Checked)
            {
                Thread T2 = new Thread(T2_Elapsed);
                T2.IsBackground = true;

                if (threadCount == 1 || !mt )
                    T2.Start();
                else if (threadCount == 2)
                {
                    Thread T3 = new Thread(T2_Elapsed);
                    T3.IsBackground = true;

                    int ms = (int)(2000f / (float)FPS);

                    T3.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T2.Start(ms);
                }
                else if (threadCount == 3)
                {
                    Thread T4 = new Thread(T2_Elapsed);
                    T4.IsBackground = true;
                    Thread T3 = new Thread(T2_Elapsed);
                    T3.IsBackground = true;

                    int ms = (int)(3000f / (float)FPS);

                    T4.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T3.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T2.Start(ms);
                }
                else if (Environment.ProcessorCount > 4 && FPS > 20) // i7, PIIx6, FX8
                {
                    Thread T4 = new Thread(T2_Elapsed);
                    T4.IsBackground = true;
                    Thread T3 = new Thread(T2_Elapsed);
                    T3.IsBackground = true;

                    int ms = (int)(3000f / (float)FPS);

                    T4.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T3.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T2.Start(ms);
                }
                else if (Environment.ProcessorCount > 3 && FPS > 20)
                {
                    Thread T3 = new Thread(T2_Elapsed);
                    T3.IsBackground = true;

                    int ms = (int)(2000f / (float)FPS);

                    T3.Start(ms);
                    Thread.Sleep((int)(1000f / (float)FPS));
                    T2.Start(ms);
                }
                else T2.Start();
            }
        }

        private void OnFinderToolMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (realtimeCapturingToolStripMenuItem.Checked)
                    realtimeCapturingToolStripMenuItem.PerformClick();
                spyWindow.CaptureMouse(true);
            }
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Text = "CaptureScreen (Recording)";
            startToolStripMenuItem.Enabled = false;
            pauseToolStripMenuItem.Enabled = true;
            stopToolStripMenuItem.Enabled = true;

            LogEvent("Recording started");

            if (!Directory.Exists(tempCapturePath))
                Directory.CreateDirectory(tempCapturePath);

            if (!realtimeCapturingToolStripMenuItem.Checked)
            {
                realtimeCapturingToolStripMenuItem.PerformClick();
                forced_realtime = true;
            }
            realtimeCapturingToolStripMenuItem.Enabled = false;

            if (!stopToolStripMenuItem.Enabled)
            {
                captureCount = -1;
                if (m_Wav != null) m_Wav = new CWaveRecord(SelectedAudioSource);
                sw.Reset();
            }

            if (m_Wav != null)
            {
                if (!File.Exists(tempCapturePath + "\\sound.wav"))
                    m_Wav.CreateNew(tempCapturePath + "\\sound.wav", 1, ABits, ASample);
                else
                    m_Wav.AppendExisting(tempCapturePath + "\\sound.wav");
                m_Wav.Record();
            }
            sw.Start();
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_Wav != null) m_Wav.Pause();
            sw.Stop();
            if (forced_realtime)
            {
                realtimeCapturingToolStripMenuItem.Enabled = true;
                realtimeCapturingToolStripMenuItem.Checked = false;
            }

            Text = "CaptureScreen (Paused)";
            startToolStripMenuItem.Enabled = true;
            pauseToolStripMenuItem.Enabled = false;
            stopToolStripMenuItem.Enabled = true;
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_Wav != null) m_Wav.Close();
            sw.Stop();

            LogEvent("Recording stopped (" + ((float)sw.ElapsedMilliseconds / 1000f) + " seconds, " + captureCount + " frames, " + FPS + " FPS)");

            label1.Text = "";
            startToolStripMenuItem.Enabled = true;
            pauseToolStripMenuItem.Enabled = false;
            stopToolStripMenuItem.Enabled = false;
            realtimeCapturingToolStripMenuItem.Enabled = true;

            if (forced_realtime)
            {
                realtimeCapturingToolStripMenuItem.PerformClick();
                forced_realtime = false;
            }

            if (captureCount > 0)
            {
                Text = "CaptureScreen (Rendering video)";

                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.DoWork += new DoWorkEventHandler(Rendering);
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);

                if (AutoName)
                {
                    string dir = VideoDir + "\\video_" + DateTimeString() + "." + VideoExt.ToLower();
                    bw.RunWorkerAsync(dir);
                }
                else
                {
                    saveFileDialog1.FileName = String.Empty;
                    saveFileDialog1.Filter = "AVI|*.avi|FLV|*.flv|GIF|*.gif|MOV|*.mov|MKV|*.mkv|MP4|*.mp4|WEBM|*.webm|WMV|*.wmv|All files|*.*";

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                        bw.RunWorkerAsync(saveFileDialog1.FileName);
                }
            }
            else Text = "CaptureScreen";
        }

        private void Rendering(object sender, DoWorkEventArgs e)
        {
            try
            {
                string path = (string)e.Argument;
                Process p = new Process();

                if (path.ToLower().EndsWith(".gif"))
                    p.StartInfo.Arguments = "-r " + FPS + " -f image2 -y -i " + tempCapturePath + "\\img%d.jpg -qscale 2 -pix_fmt rgb24 " + path;
                else if (m_Wav == null)
                    p.StartInfo.Arguments = "-r " + FPS + " -f image2 -y -i " + tempCapturePath + "\\img%d.jpg -qscale 2 " + path;
                else
                    p.StartInfo.Arguments = "-r " + FPS + " -vframes " + FPS + " -f image2 -y -i " + tempCapturePath + "\\img%d.jpg -i " + tempCapturePath + "\\sound.wav -qscale 2 " + path;

                p.StartInfo.FileName = FFmpegPath;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.Start();

                LogEvent(p.StartInfo.FileName + " " + p.StartInfo.Arguments);

                p.WaitForExit();

                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                (sender as BackgroundWorker).ReportProgress(100, null);
                p.Dispose();
            }
            catch (Exception ex)
            {
                (sender as BackgroundWorker).ReportProgress(1, ex);
            }
        }

        private void bw_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 100)
            {
                Text = "CaptureScreen";
                if (OpenFolder) Process.Start("explorer.exe", "/select, " + saveFileDialog1.FileName);
            }
            else
            {
                Text = "CaptureScreen (" + ((Exception)e.UserState).Message + ")";
                LogError((Exception)e.UserState);
            }
        }

        private void removeItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Removed.Add((IntPtr)listView1.SelectedItems[0].Tag);
            listView1.SelectedItems[0].Remove();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            removeItemToolStripMenuItem.Enabled = (listView1.SelectedItems.Count == 1);
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form3 form3 = new Form3();
            if (form3.ShowDialog() == DialogResult.OK)
            {
                if (SelectedAudioSource > -1 && SelectedAudioSource < AudioSource.Length)
                    m_Wav = new CWaveRecord(SelectedAudioSource);
                else
                    m_Wav = null;
            }
            form3.Dispose();
        }
    }
}
