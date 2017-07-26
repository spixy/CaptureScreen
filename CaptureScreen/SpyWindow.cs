using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CaptureScreen
{
	public class SpyWindow : System.Windows.Forms.Form
	{
        public delegate void DisplayImageEventHandler(Image image, bool autoDecideOnSizing, PictureBoxSizeMode manualSizeMode);
		public event DisplayImageEventHandler ImageReadyForDisplay;
		private bool _capturing;
		private Image _finderHome;
		private Image _finderGone;		
		private Cursor _cursorDefault;
		private Cursor _cursorFinder;
		private IntPtr _hPreviousWindow;

        public PictureBox _pictureBox;
        public PictureBox _pictureBox2;
        public string text;

		public SpyWindow()
		{
			_cursorDefault = Cursor.Current;
            _cursorFinder = LoadCursor();
            _finderHome = CaptureScreen.Properties.Resources.FinderHome;
            _finderGone = CaptureScreen.Properties.Resources.FinderGone;
		}

        public static Cursor LoadCursor()
        {
            using (var memoryStream = new MemoryStream(Properties.Resources.Finder))
            {
                return new Cursor(memoryStream);
            }
        }

		protected override void WndProc(ref Message m)
		{									
			switch(m.Msg)
			{
				/*
				 * stop capturing events as soon as the user releases the left mouse button
				 * */
				case (int)Win32.WindowMessages.WM_LBUTTONUP:
					this.CaptureMouse(false);
					break;
				/*
				 * handle all the mouse movements
				 * */
				case (int)Win32.WindowMessages.WM_MOUSEMOVE:
					this.HandleMouseMovements();
					break;			
			};

			base.WndProc (ref m);
		}

		protected virtual void OnImageReadyForDisplay(Image image)
		{
			try
			{
				if (this.ImageReadyForDisplay != null) this.ImageReadyForDisplay(image, false, PictureBoxSizeMode.StretchImage);
			}
			catch(Exception ex)
			{
                text = "CaptureScreen (" + ex.Message + ")";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter("DebugInfo.txt", true))
                    file.WriteLine(DateTime.Now.Day + "." + DateTime.Now.Month + "." + DateTime.Now.Year + " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + " - " + ex.Message);
			}
		}

        public void CaptureMouse(bool captured)
		{
			// if we're supposed to capture the window
			if (captured)
			{
				// capture the mouse movements and send them to ourself
				Win32.SetCapture(this.Handle);

				// set the mouse cursor to our finder cursor
				Cursor.Current = _cursorFinder;

				// change the image to the finder gone image
				_pictureBox.Image = _finderGone;
			}
			// otherwise we're supposed to release the mouse capture
			else
			{
				// so release it
				Win32.ReleaseCapture();

				// put the default cursor back
				Cursor.Current = _cursorDefault;

				// change the image back to the finder at home image
				_pictureBox.Image = _finderHome;

				// and finally refresh any window that we were highlighting
				if (_hPreviousWindow != IntPtr.Zero)
				{
					WindowHighlighter.Refresh(_hPreviousWindow);
					_hPreviousWindow = IntPtr.Zero;
				}
			}

			// save our capturing state
			_capturing = captured;
		}

		private void HandleMouseMovements()
		{
			if (!_capturing) return;

            try
            {
                // capture the window under the cursor's position
                IntPtr hWnd = Win32.WindowFromPoint(Cursor.Position);

                // if the window we're over, is not the same as the one before, and we had one before, refresh it
                if (_hPreviousWindow != IntPtr.Zero && _hPreviousWindow != hWnd) WindowHighlighter.Refresh(_hPreviousWindow);

                // if we didn't find a window.. that's pretty hard to imagine. lol

                // save the window we're over
                _hPreviousWindow = hWnd;

                Win32.Rect rc = new Win32.Rect();
                Win32.GetWindowRect(hWnd, ref rc);

                // highlight the window
                WindowHighlighter.Highlight(hWnd);

                Image image = ScreenCapturing.GetWindowCaptureAsBitmap(hWnd);

                // fire our image read event, which the main window will display for us
                this.OnImageReadyForDisplay(image);

                _pictureBox2.Image = image;
            }
            catch (Exception ex)
            {
                text = "CaptureScreen (" + ex.Message + ")";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter("DebugInfo.txt", true))
                    file.WriteLine(DateTime.Now.Day + "." + DateTime.Now.Month + "." + DateTime.Now.Year + " " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + " - " + ex.Message);
            }
		}
	}
}
