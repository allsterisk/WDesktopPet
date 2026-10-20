using System;
using System.Collections.Generic;
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

    sealed class Skin {
        public readonly string Id, Name, Resource, Call;
        public readonly Color Left, Right;
        public Skin(string id, string name, string resource, string call, Color left, Color right) {
            Id = id; Name = name; Resource = resource; Call = call; Left = left; Right = right;
        }
        public static readonly Skin[] All = {
            new Skin("cyclone-joker", "疾风王牌", "w-sprites.png", "Cyclone！Joker！", Color.FromArgb(23, 107, 67), Color.FromArgb(38, 33, 46)),
            new Skin("heat-metal", "炽热金属", "heat-metal.png", "Heat！Metal！", Color.FromArgb(172, 46, 50), Color.FromArgb(208, 213, 220)),
            new Skin("luna-trigger", "月神扳机", "luna-trigger.png", "Luna！Trigger！", Color.FromArgb(232, 195, 74), Color.FromArgb(22, 88, 171)),
            new Skin("fang-joker", "獠牙王牌", "fang-joker.png", "Fang！Joker！", Color.FromArgb(233, 237, 239), Color.FromArgb(40, 35, 47))
        };
        public static Skin Find(string id) {
            foreach (var skin in All) if (skin.Id == id) return skin;
            return All[0];
        }
        static double Linear(byte channel) { double v = channel / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        internal static double Luminance(Color color) { return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B); }
        internal static Color TextOn(Color background) { return Luminance(background) > 0.179 ? Color.Black : Color.White; }
    }

    sealed class PetSettings {
        public int X, Y, Scale = 2;
        public bool Wander = true, Placed;
        public string SkinId = Skin.All[0].Id;
        internal static PetSettings Parse(string[] lines) {
            var result = new PetSettings();
            int x, y, scale; bool wander;
            // Four-line settings from the original release remain valid.
            if (lines.Length >= 4 && Int32.TryParse(lines[0], out x) && Int32.TryParse(lines[1], out y) && Int32.TryParse(lines[2], out scale) && Boolean.TryParse(lines[3], out wander)) {
                result.X = x; result.Y = y; result.Scale = Math.Max(1, Math.Min(3, scale)); result.Wander = wander; result.Placed = true;
                if (lines.Length >= 5) result.SkinId = Skin.Find(lines[4]).Id;
            }
            return result;
        }
        internal string[] Serialize() { return new [] { X.ToString(), Y.ToString(), Scale.ToString(), Wander.ToString(), Skin.Find(SkinId).Id }; }
    }

    sealed class Sprites : IDisposable {
        readonly Dictionary<string, Bitmap> sheets = new Dictionary<string, Bitmap>();
        public Skin Current { get; private set; }
        public Bitmap Sheet { get { return sheets[Current.Id]; } }
        public Sprites() { Select(Skin.All[0].Id); }
        public void Select(string id) {
            var next = Skin.Find(id);
            if (!sheets.ContainsKey(next.Id)) {
                // Every skin is embedded so the executable needs no external files.
                using (var stream = typeof(Sprites).Assembly.GetManifestResourceStream(next.Resource)) {
                    if (stream == null) throw new InvalidDataException("Missing skin: " + next.Id);
                    using (var source = new Bitmap(stream)) sheets.Add(next.Id, new Bitmap(source));
                }
            }
            Current = next;
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
                    if (String.Equals(message, Current.Call, StringComparison.Ordinal)) {
                        int middle = box.Left + box.Width / 2;
                        var left = new Rectangle(box.Left, box.Top, middle - box.Left, box.Height);
                        var right = new Rectangle(middle, box.Top, box.Right - middle, box.Height);
                        using (var bg = new SolidBrush(Current.Left)) g.FillRectangle(bg, left);
                        using (var bg = new SolidBrush(Current.Right)) g.FillRectangle(bg, right);
                        using (var seam = new Pen(Color.FromArgb(185, 192, 197), scale)) g.DrawLine(seam, middle, box.Top, middle, box.Bottom);
                        // Each memory name owns its own padded layout. No letter crosses
                        // the seam and no glyph is redrawn through a clipping boundary.
                        string[] words = Current.Call.Split(new [] { '！' }, StringSplitOptions.RemoveEmptyEntries);
                        left.Inflate(-4 * scale, -2 * scale);
                        right.Inflate(-4 * scale, -2 * scale);
                        DrawLabel(g, words[0] + "！", left, Skin.TextOn(Current.Left), scale);
                        DrawLabel(g, words[1] + "！", right, Skin.TextOn(Current.Right), scale);
                    } else {
                        using (var bg = new SolidBrush(Color.FromArgb(22, 28, 30))) g.FillRectangle(bg, box);
                        var textBox = Rectangle.Inflate(box, -4 * scale, -2 * scale);
                        DrawLabel(g, message, textBox, Color.FromArgb(238, 247, 239), scale);
                    }
                    using (var border = new Pen(Color.FromArgb(185, 192, 197), scale)) g.DrawRectangle(border, box);
                }
            }
            return result;
        }
        static void DrawLabel(Graphics g, string text, Rectangle box, Color foreground, int scale) {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap }) {
                float pointSize = 6.3f * scale;
                using (var original = new Font("Microsoft YaHei UI", pointSize, FontStyle.Bold, GraphicsUnit.Point)) {
                    SizeF measured = g.MeasureString(text, original, Int32.MaxValue, format);
                    pointSize *= Math.Min(1f, Math.Min(box.Width / measured.Width, box.Height / measured.Height));
                }
                using (var font = new Font("Microsoft YaHei UI", pointSize, FontStyle.Bold, GraphicsUnit.Point))
                using (var fg = new SolidBrush(foreground)) g.DrawString(text, font, fg, box, format);
            }
        }
        public void Dispose() { foreach (var sheet in sheets.Values) sheet.Dispose(); sheets.Clear(); }
    }

    sealed class Pet : Form {
        readonly Sprites sprites = new Sprites();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Random random = new Random();
        readonly NotifyIcon tray;
        Icon petIcon;
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WDesktopPet", "settings.txt");
        bool wander = true, sleeping, dragging, pressed, userPlaced;
        int scale = 2, tick, direction = 1, moveUntil, nextAction = 90, reactUntil, talkUntil;
        Point pressMouse, pressLocation;
        string message = "";
        ToolStripMenuItem walkItem, sleepItem, titleItem, skinsMenu;

        public Pet() : this(null) {}
        public Pet(string settingsOverride) {
            if (settingsOverride != null) settingsPath = settingsOverride;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "假面骑士 W · 风都桌宠";
            LoadSettings();
            Text = "假面骑士 W · " + sprites.Current.Name;
            Size = new Size(160 * scale, 168 * scale);
            if (!userPlaced) Home();
            Clamp();
            RefreshIcon();
            ContextMenuStrip = BuildMenu();
            tray = new NotifyIcon { Icon = petIcon, Text = Text, ContextMenuStrip = ContextMenuStrip, Visible = true };
            tray.DoubleClick += delegate { sleeping = false; sleepItem.Checked = false; Home(); Say("风都，今天也很和平。", 120); };
            timer.Interval = 33;
            timer.Tick += Step;
            Shown += delegate { Say(sprites.Current.Call, 140); timer.Start(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }
        protected override CreateParams CreateParams {
            get { var p = base.CreateParams; p.ExStyle |= 0x00080000 | 0x00000080; return p; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        ContextMenuStrip BuildMenu() {
            var menu = new ContextMenuStrip();
            titleItem = new ToolStripMenuItem("假面骑士 W · " + sprites.Current.Name) { Enabled = false };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());
            skinsMenu = new ToolStripMenuItem("切换皮肤");
            foreach (var skin in Skin.All) {
                string id = skin.Id;
                var item = new ToolStripMenuItem(skin.Name) { Checked = sprites.Current.Id == id, Tag = id };
                item.Click += delegate { ChangeSkin(id); };
                skinsMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(skinsMenu);
            walkItem = new ToolStripMenuItem("自由散步") { Checked = wander, CheckOnClick = true };
            walkItem.CheckedChanged += delegate { wander = walkItem.Checked; moveUntil = 0; SaveSettings(); };
            menu.Items.Add(walkItem);
            sleepItem = new ToolStripMenuItem("休息一下") { CheckOnClick = true };
            sleepItem.CheckedChanged += delegate { sleeping = sleepItem.Checked; moveUntil = 0; reactUntil = 0; Say(sleeping ? "休息一下……Zzz" : "搭档，我们走！", 100); };
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
        void RefreshIcon() {
            Icon previous = petIcon;
            using (var image = sprites.Render(4, 1, 0, "")) {
                IntPtr handle = image.GetHicon();
                try { petIcon = (Icon)Icon.FromHandle(handle).Clone(); } finally { Native.DestroyIcon(handle); }
            }
            Icon = petIcon;
            if (tray != null) tray.Icon = petIcon;
            if (previous != null) previous.Dispose();
        }
        void ChangeSkin(string id) {
            sprites.Select(id);
            foreach (ToolStripMenuItem item in skinsMenu.DropDownItems) item.Checked = (string)item.Tag == sprites.Current.Id;
            titleItem.Text = "假面骑士 W · " + sprites.Current.Name;
            Text = titleItem.Text;
            tray.Text = "假面骑士 W · " + sprites.Current.Name;
            RefreshIcon();
            // Preserve sleep, size and position while changing the visual theme.
            if (!sleeping) { reactUntil = tick + 44; moveUntil = 0; nextAction = tick + 150; }
            Say(sprites.Current.Call, 140);
            SaveSettings();
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
            string[] quotes = { "来，细数你的罪恶吧！", "我们是二位一体的侦探。", sprites.Current.Call, "搭档，风都就交给我们了！" };
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
            // Keep even FangJoker's taller antenna below the dialogue box at the jump peak.
            int lift = tick < reactUntil ? (int)(Math.Abs(Math.Sin((reactUntil - tick) * Math.PI / 22)) * 4 * scale) : sleeping ? 0 : (tick / 18) % 2 * scale;
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
                var settings = PetSettings.Parse(File.ReadAllLines(settingsPath));
                scale = settings.Scale; wander = settings.Wander; userPlaced = settings.Placed;
                if (userPlaced) Location = new Point(settings.X, settings.Y);
                sprites.Select(settings.SkinId);
            } catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
        void SaveSettings() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                var settings = new PetSettings { X = Left, Y = Top, Scale = scale, Wander = wander, SkinId = sprites.Current.Id };
                File.WriteAllLines(settingsPath, settings.Serialize());
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
                using (var preview = new Bitmap(1280, 336, PixelFormat.Format32bppArgb))
                using (var gallery = Graphics.FromImage(preview)) {
                    gallery.Clear(Color.Transparent);
                    for (int i = 0; i < Skin.All.Length; i++) {
                        Skin skin = Skin.All[i]; sprites.Select(skin.Id);
                        Check(sprites.Current == skin, "Skin selection: " + skin.Id);
                        Check(sprites.Sheet.Width % 4 == 0 && sprites.Sheet.Height % 2 == 0, "Sprite grid: " + skin.Id);
                        Check(sprites.Sheet.GetPixel(0, 0).A == 0, "Sheet corner alpha: " + skin.Id);
                        foreach (var background in new [] { skin.Left, skin.Right }) {
                            double bg = Skin.Luminance(background), fg = Skin.Luminance(Skin.TextOn(background));
                            double contrast = (Math.Max(bg, fg) + 0.05) / (Math.Min(bg, fg) + 0.05);
                            Check(contrast >= 4.5, "Dialogue contrast: " + skin.Id);
                        }
                        for (int frame = 0; frame < 8; frame++) {
                            using (var bitmap = sprites.Render(frame, 2, frame == 6 ? 8 : 0, frame == 5 ? skin.Call : "")) {
                                Check(bitmap.Width == 320 && bitmap.Height == 336, "Render size invalid");
                                Check(bitmap.GetPixel(0, 0).A == 0, "Background must be transparent");
                                int visible = 0;
                                for (int y = 72; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 128) visible++;
                                Check(visible > 5000, "Missing character frame " + skin.Id + "/" + frame);
                                if (frame == 5) {
                                    Check(bitmap.GetPixel(12, 10).ToArgb() == skin.Left.ToArgb(), "Left dialogue color: " + skin.Id);
                                    Check(bitmap.GetPixel(308, 10).ToArgb() == skin.Right.ToArgb(), "Right dialogue color: " + skin.Id);
                                    gallery.DrawImageUnscaled(bitmap, i * 320, 0);
                                }
                                bitmap.Save(Path.Combine(output, skin.Id + "-frame-" + frame + ".png"), ImageFormat.Png);
                            }
                        }
                        foreach (int size in new [] { 1, 2, 3 }) {
                            using (var bitmap = sprites.Render(5, size, 0, skin.Call)) {
                                Check(bitmap.Width == 160 * size && bitmap.Height == 168 * size, "Scaled rendering");
                                Check(bitmap.GetPixel(6 * size, 5 * size).ToArgb() == skin.Left.ToArgb(), "Scaled left theme");
                                Check(bitmap.GetPixel(154 * size, 5 * size).ToArgb() == skin.Right.ToArgb(), "Scaled right theme");
                                // Text must leave a clean gutter on both sides of the seam.
                                for (int y = 4 * size; y < 29 * size; y++) {
                                    Check(bitmap.GetPixel(78 * size, y).ToArgb() == skin.Left.ToArgb(), "Left word crosses seam: " + skin.Id);
                                    Check(bitmap.GetPixel(82 * size, y).ToArgb() == skin.Right.ToArgb(), "Right word crosses seam: " + skin.Id);
                                }
                                bitmap.Save(Path.Combine(output, skin.Id + "-transform-size-" + size + ".png"), ImageFormat.Png);
                            }
                            foreach (string line in new [] { "来，细数你的罪恶吧！", "休息一下……Zzz", "搭档，我们走！", "风都，今天也很和平。" })
                            using (var bitmap = sprites.Render(4, size, 0, line)) {
                                int neutral = Color.FromArgb(22, 28, 30).ToArgb();
                                Check(bitmap.GetPixel(6 * size, 5 * size).ToArgb() == neutral, "Normal speech left must be neutral");
                                Check(bitmap.GetPixel(154 * size, 5 * size).ToArgb() == neutral, "Normal speech right must be neutral");
                                Check(bitmap.GetPixel(80 * size, 4 * size).ToArgb() == neutral, "Normal speech must not have a seam");
                                if (line == "来，细数你的罪恶吧！") bitmap.Save(Path.Combine(output, skin.Id + "-normal-size-" + size + ".png"), ImageFormat.Png);
                            }
                        }
                        var saved = new PetSettings { X = -900, Y = 100, Scale = 3, Wander = false, SkinId = skin.Id };
                        var loaded = PetSettings.Parse(saved.Serialize());
                        Check(loaded.Placed && loaded.X == -900 && loaded.Y == 100 && loaded.Scale == 3 && !loaded.Wander && loaded.SkinId == skin.Id, "Settings round trip: " + skin.Id);
                    }
                    preview.Save(Path.Combine(output, "skins-preview.png"), ImageFormat.Png);
                }
                // Switching back must restore the original resource and theme.
                sprites.Select("cyclone-joker"); Check(sprites.Current == Skin.All[0], "Switch back to original");
                sprites.Select("unknown-skin"); Check(sprites.Current == Skin.All[0], "Unknown skin fallback");
            }
            var legacy = PetSettings.Parse(new [] { "-500", "240", "2", "True" });
            Check(legacy.Placed && legacy.X == -500 && legacy.Y == 240 && legacy.Wander && legacy.SkinId == "cyclone-joker", "Legacy four-line settings");
            var unknown = PetSettings.Parse(new [] { "-500", "240", "2", "False", "removed-skin" });
            Check(unknown.Placed && !unknown.Wander && unknown.SkinId == "cyclone-joker", "Unknown persisted skin");
            Check(!PetSettings.Parse(new [] { "broken" }).Placed, "Corrupt settings fallback");
            Check(Pet.Constrain(new Point(-999, 9999), new Size(320, 336), new Rectangle(0, 0, 1920, 1040)) == new Point(0, 704), "Primary monitor bounds");
            Check(Pet.Constrain(new Point(-2200, -90), new Size(320, 336), new Rectangle(-1920, 0, 1920, 1080)) == new Point(-1920, 0), "Negative monitor bounds");
            Check(Pet.Constrain(new Point(20, 20), new Size(480, 504), new Rectangle(0, 0, 400, 300)) == Point.Empty, "Small display bounds");
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 4 skins x 8 frames; transparent sprites; transformation-only split themes at 3 sizes; separate memory labels with clean seam gutters; neutral normal/sleep/wake speech without seam; text contrast >= 4.5; skin switching and fallback; settings round trips and legacy migration; jump offset; primary/negative/small display bounds.\r\n");
        }
    }
}
