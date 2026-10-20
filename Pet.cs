using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WDesktopPet {
    static class Program {
        [STAThread] static void Main(string[] args) {
            if (args.Length > 0 && args[0] == "--self-test") { SelfTest.Run(); return; }
            bool created;
            using (var mutex = new Mutex(true, "Local\\WDesktopPet.CycloneJoker", out created)) {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Pet());
            }
        }
    }

    sealed class Sprites : IDisposable {
        public readonly Bitmap Sheet;
        public Sprites() {
            // Embedded resource makes the distributed executable self-contained.
            using (var stream = typeof(Sprites).Assembly.GetManifestResourceStream("w-sprites.png"))
            using (var source = new Bitmap(stream)) Sheet = new Bitmap(source);
        }
        public Bitmap Render(int frame, int scale, int lift, string message) {
            var result = new Bitmap(160 * scale, 168 * scale, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result)) {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int cw = Sheet.Width / 4, ch = Sheet.Height / 2;
                g.DrawImage(Sheet, new Rectangle(32 * scale, 36 * scale - lift, 96 * scale, 128 * scale),
                    new Rectangle(frame % 4 * cw, frame / 4 * ch, cw, ch), GraphicsUnit.Pixel);
                if (!String.IsNullOrEmpty(message)) {
                    var box = new Rectangle(4 * scale, 2 * scale, 152 * scale, 29 * scale);
                    using (var bg = new SolidBrush(Color.FromArgb(242, 22, 28, 30))) g.FillRectangle(bg, box);
                    using (var line = new Pen(Color.FromArgb(89, 218, 138), scale)) g.DrawRectangle(line, box);
                    using (var font = new Font("Microsoft YaHei UI", 6.3f * scale, FontStyle.Bold, GraphicsUnit.Point))
                    using (var fg = new SolidBrush(Color.FromArgb(238, 247, 239)))
                    using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(message, font, fg, box, format);
                }
            }
            return result;
        }
        public void Dispose() { Sheet.Dispose(); }
    }

    sealed class Pet : Form {
        readonly Sprites sprites = new Sprites();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Random random = new Random();
        readonly NotifyIcon tray;
        readonly Icon petIcon;
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WDesktopPet", "settings.txt");
        bool wander = true, sleeping, dragging, pressed, userPlaced;
        int scale = 2, tick, direction = 1, moveUntil, nextAction = 90, reactUntil, talkUntil;
        Point pressMouse, pressLocation;
        string message = "";
        ToolStripMenuItem walkItem, sleepItem;

        public Pet() {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "假面骑士 W · 风都桌宠";
            LoadSettings();
            Size = new Size(160 * scale, 168 * scale);
            if (!userPlaced) Home();
            Clamp();
            using (var iconImage = sprites.Render(4, 1, 0, "")) {
                IntPtr handle = iconImage.GetHicon();
                try { petIcon = (Icon)Icon.FromHandle(handle).Clone(); } finally { Native.DestroyIcon(handle); }
            }
            Icon = petIcon;
            ContextMenuStrip = BuildMenu();
            tray = new NotifyIcon { Icon = petIcon, Text = "假面骑士 W · 右键打开菜单", ContextMenuStrip = ContextMenuStrip, Visible = true };
            tray.DoubleClick += delegate { sleeping = false; sleepItem.Checked = false; Home(); Say("风都，今天也很和平。", 120); };
            timer.Interval = 33;
            timer.Tick += Step;
            Shown += delegate { Say("搭档，开始今天的调查吧！", 140); timer.Start(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }
        protected override CreateParams CreateParams {
            get { var p = base.CreateParams; p.ExStyle |= 0x00080000 | 0x00000080; return p; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        ContextMenuStrip BuildMenu() {
            var menu = new ContextMenuStrip();
            menu.Items.Add("假面骑士 W · 疾风王牌").Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            walkItem = new ToolStripMenuItem("自由散步") { Checked = wander, CheckOnClick = true };
            walkItem.CheckedChanged += delegate { wander = walkItem.Checked; moveUntil = 0; SaveSettings(); };
            menu.Items.Add(walkItem);
            sleepItem = new ToolStripMenuItem("休息一下") { CheckOnClick = true };
            sleepItem.CheckedChanged += delegate { sleeping = sleepItem.Checked; moveUntil = 0; Say(sleeping ? "休息一下……Zzz" : "搭档，我们走！", 100); };
            menu.Items.Add(sleepItem);
            menu.Items.Add("来一句经典台词", null, delegate { React(); });
            var sizes = new ToolStripMenuItem("桌宠大小");
            for (int i = 1; i <= 3; i++) {
                int value = i;
                var item = new ToolStripMenuItem(i == 1 ? "小 · 96 × 128" : i == 2 ? "中 · 192 × 256" : "大 · 288 × 384") { Checked = scale == i };
                item.Click += delegate {
                    int bottom = Bottom;
                    scale = value; Size = new Size(160 * scale, 168 * scale); Top = bottom - Height;
                    foreach (ToolStripMenuItem sibling in sizes.DropDownItems) sibling.Checked = sibling == item;
                    Clamp(); SaveSettings(); Draw();
                };
                sizes.DropDownItems.Add(item);
            }
            menu.Items.Add(sizes);
            menu.Items.Add("回到屏幕右下角", null, delegate { Home(); SaveSettings(); Draw(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出桌宠", null, delegate { Close(); });
            return menu;
        }
        void DisplayChanged(object sender, EventArgs e) { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { Clamp(); Draw(); }); }
        void Home() { var a = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(a.Right - Width - 24, a.Bottom - Height); }
        internal static Point Constrain(Point point, Size size, Rectangle area) {
            return new Point(Math.Max(area.Left, Math.Min(point.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(point.Y, area.Bottom - size.Height)));
        }
        void Clamp() { Location = Constrain(Location, Size, Screen.FromRectangle(Bounds).WorkingArea); }
        void Say(string text, int duration) { message = text; talkUntil = tick + duration; Draw(); }
        void React() {
            sleeping = false; sleepItem.Checked = false; moveUntil = 0;
            reactUntil = tick + 44;
            string[] quotes = { "来，细数你的罪恶吧！", "我们是二位一体的侦探。", "Cyclone！Joker！", "搭档，风都就交给我们了！" };
            Say(quotes[random.Next(quotes.Length)], 125);
        }
        void Step(object sender, EventArgs e) {
            tick++;
            if (!pressed && !sleeping && tick >= reactUntil && !ContextMenuStrip.Visible) {
                if (tick >= nextAction) {
                    direction = random.Next(2) == 0 ? -1 : 1;
                    moveUntil = wander ? tick + random.Next(60, 150) : 0;
                    nextAction = tick + random.Next(200, 360);
                }
                if (wander && tick < moveUntil) {
                    var area = Screen.FromRectangle(Bounds).WorkingArea;
                    int x = Left + direction * scale;
                    if (x < area.Left || x + Width > area.Right) { direction = -direction; x = Left + direction * scale; }
                    Location = Constrain(new Point(x, Top), Size, area);
                }
            }
            Draw();
        }
        void Draw() {
            if (!IsHandleCreated || IsDisposed) return;
            int frame = sleeping ? 7 : tick < reactUntil ? (tick % 20 < 10 ? 5 : 6) : !pressed && wander && tick < moveUntil ? (tick / 5) % 4 : 4;
            int lift = tick < reactUntil ? (int)(Math.Abs(Math.Sin((reactUntil - tick) * Math.PI / 22)) * 10 * scale) : sleeping ? 0 : (tick / 18) % 2 * scale;
            using (var image = sprites.Render(frame, scale, lift, tick < talkUntil ? message : "")) Native.Present(this, image);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = true; dragging = false; pressMouse = Cursor.Position; pressLocation = Location; Capture = true; moveUntil = 0;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (!pressed) { Cursor = Cursors.Hand; return; }
            Point delta = new Point(Cursor.Position.X - pressMouse.X, Cursor.Position.Y - pressMouse.Y);
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 5) dragging = true;
            if (dragging) Location = new Point(pressLocation.X + delta.X, pressLocation.Y + delta.Y);
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pressed) return;
            pressed = false; Capture = false;
            if (dragging) { Clamp(); SaveSettings(); } else React();
            dragging = false; nextAction = tick + 150;
        }
        void LoadSettings() {
            try {
                var values = File.ReadAllLines(settingsPath);
                int x, y, s; bool w;
                if (values.Length == 4 && Int32.TryParse(values[0], out x) && Int32.TryParse(values[1], out y) && Int32.TryParse(values[2], out s) && Boolean.TryParse(values[3], out w)) {
                    scale = Math.Max(1, Math.Min(3, s)); wander = w; Location = new Point(x, y); userPlaced = true;
                }
            } catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
        void SaveSettings() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllLines(settingsPath, new [] { Left.ToString(), Top.ToString(), scale.ToString(), wander.ToString() });
            } catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
        protected override void OnFormClosed(FormClosedEventArgs e) {
            timer.Stop(); SaveSettings();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            tray.Visible = false; tray.Dispose(); timer.Dispose(); sprites.Dispose(); petIcon.Dispose();
            base.OnFormClosed(e);
        }
    }

    static class Native {
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; public POINT(int a, int b) { x = a; y = b; } }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; public SIZE(int a, int b) { cx = a; cy = b; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, format; }
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT at, ref SIZE size, IntPtr src, ref POINT origin, int key, ref BLEND blend, int flags);
        internal static void Present(Form form, Bitmap bitmap) {
            IntPtr screen = GetDC(IntPtr.Zero), dc = CreateCompatibleDC(screen), handle = IntPtr.Zero, previous = IntPtr.Zero;
            try {
                handle = bitmap.GetHbitmap(Color.FromArgb(0)); previous = SelectObject(dc, handle);
                var at = new POINT(form.Left, form.Top); var size = new SIZE(bitmap.Width, bitmap.Height); var origin = new POINT(0, 0);
                var blend = new BLEND { op = 0, alpha = 255, format = 1 };
                if (!UpdateLayeredWindow(form.Handle, screen, ref at, ref size, dc, ref origin, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            } finally {
                if (previous != IntPtr.Zero) SelectObject(dc, previous);
                if (handle != IntPtr.Zero) DeleteObject(handle);
                DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }

    static class SelfTest {
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
        public static void Run() {
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verification");
            Directory.CreateDirectory(output);
            using (var sprites = new Sprites()) {
                Check(sprites.Sheet.Width % 4 == 0 && sprites.Sheet.Height % 2 == 0, "Sprite grid invalid");
                for (int frame = 0; frame < 8; frame++) {
                    using (var bitmap = sprites.Render(frame, 2, frame == 6 ? 20 : 0, frame == 5 ? "来，细数你的罪恶吧！" : "")) {
                        Check(bitmap.Width == 320 && bitmap.Height == 336, "Render size invalid");
                        Check(bitmap.GetPixel(0, 0).A == 0, "Background must be transparent");
                        int visible = 0;
                        for (int y = 72; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 128) visible++;
                        Check(visible > 5000, "Missing character frame " + frame);
                        bitmap.Save(Path.Combine(output, "frame-" + frame + ".png"), ImageFormat.Png);
                    }
                }
            }
            Check(Pet.Constrain(new Point(-999, 9999), new Size(320, 336), new Rectangle(0, 0, 1920, 1040)) == new Point(0, 704), "Primary monitor bounds");
            Check(Pet.Constrain(new Point(-2200, -90), new Size(320, 336), new Rectangle(-1920, 0, 1920, 1080)) == new Point(-1920, 0), "Negative monitor bounds");
            Check(Pet.Constrain(new Point(20, 20), new Size(480, 504), new Rectangle(0, 0, 400, 300)) == Point.Empty, "Small display bounds");
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 8 sprite frames, alpha transparency, rendered speech, jump offset, primary/negative/small display bounds.\r\n");
        }
    }
}
