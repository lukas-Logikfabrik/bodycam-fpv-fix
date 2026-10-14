using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BodycamFpvFix
{
    sealed partial class MainForm
    {
        readonly Dictionary<Control, Rectangle> positions = new Dictionary<Control, Rectangle>();
        readonly Dictionary<Control, float> textSizes = new Dictionary<Control, float>();
        readonly Dictionary<ColumnStyle, float> columnWidths = new Dictionary<ColumnStyle, float>();
        readonly Dictionary<ColumnStyle, Control> columnOwners = new Dictionary<ColumnStyle, Control>();
        readonly Dictionary<Control, Panel> pageOf = new Dictionary<Control, Panel>();   // every control inside a page
        readonly HashSet<Panel> stalePages = new HashSet<Panel>();   // hidden pages not yet at the current scale
        readonly Dictionary<Panel, List<Control>> preparing = new Dictionary<Panel, List<Control>>();   // held layouts
        readonly Queue<Control> windowsToCreate = new Queue<Control>();
        float layoutScale = 1f;
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        readonly Dictionary<string, Label> readouts = new Dictionary<string, Label>();
        readonly PrivateFontCollection titleFonts = new PrivateFontCollection();
        IntPtr fontMemory, nativeFont;
        Image room;
        Bitmap backgroundFrame;
        Label pageTitle, radioState, cockpitArm, cockpitAcro, outputTitle;
        StickScope leftScope, rightScope;
        string activePage = "CONTROLLER";
        bool arranging;
        readonly List<Font> ownedFonts = new List<Font>();
        readonly Dictionary<Control, Font> resizedFonts = new Dictionary<Control, Font>();
        Button minimizeWindow, maximizeWindow, closeWindow;

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr reserved, ref uint count);
        [DllImport("gdi32.dll")]
        static extern bool RemoveFontMemResourceEx(IntPtr handle);

        Font Heading(float size)
        {
            var font = new Font(titleFonts.Families.Length > 0 ? titleFonts.Families[0] : Font.FontFamily, size);
            ownedFonts.Add(font);
            return font;
        }

        void LoadVisuals()
        {
            var assembly = typeof(MainForm).Assembly;
            using (var stream = assembly.GetManifestResourceStream("BodycamFpvFix.room.png"))
            {
                if (stream != null) using (var source = Image.FromStream(stream)) room = new Bitmap(source);
            }
            using (var stream = assembly.GetManifestResourceStream("BodycamFpvFix.BebasNeue-Regular.ttf"))
            {
                if (stream == null) return;
                using (var bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    byte[] data = bytes.ToArray();
                    fontMemory = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, fontMemory, data.Length);
                    titleFonts.AddMemoryFont(fontMemory, data.Length);
                    uint count = 0;
                    nativeFont = AddFontMemResourceEx(fontMemory, (uint)data.Length, IntPtr.Zero, ref count);
                }
            }
        }

        void BuildLayout()
        {
            LoadVisuals();
            SuspendLayout();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(14, 15, 16);
            ForeColor = Color.Gainsboro;
            minimizeWindow = ActionButton("—", () => WindowState = FormWindowState.Minimized);
            maximizeWindow = ActionButton("□", () =>
            {
                // Windows reads the maximized position relative to the monitor, not the whole desktop (second monitor).
                var screen = Screen.FromControl(this);
                var area = screen.WorkingArea;
                MaximizedBounds = new Rectangle(area.X - screen.Bounds.X, area.Y - screen.Bounds.Y, area.Width, area.Height);
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            });
            closeWindow = ActionButton("×", Close);
            foreach (var button in new[] { minimizeWindow, maximizeWindow, closeWindow })
            {
                button.Font = new Font("Segoe UI", 13);
                button.FlatAppearance.BorderSize = 0;
                button.BackColor = Color.Transparent;
                button.MinimumSize = Size.Empty;
                button.Size = new Size(40, 30);
                Controls.Add(button);
                textSizes[button] = button.Font.Size;
            }
            minimizeWindow.AccessibleName = "Minimize window";
            maximizeWindow.AccessibleName = "Maximize or restore window";
            closeWindow.AccessibleName = "Close window";
            AddText(this, "BODYCAM", 52, 32, 250, 76, 49, true);
            AddText(this, "F P V   F I X", 56, 106, 220, 38, 21, true);
            AddText(this, "UNOFFICIAL TOOL", 57, 146, 220, 22, 10, true);
            string[] names = { "CONTROLLER", "CALIBRATION", "SWITCHES", "BUTTONS", "ADVANCED" };
            for (int i = 0; i < names.Length; i++)
            {
                string page = names[i];
                var nav = ActionButton(page, () => Navigate(page));
                nav.Name = "nav-" + page;
                nav.TextAlign = ContentAlignment.MiddleLeft;
                nav.Padding = new Padding(12, 0, 0, 0);
                nav.Font = Heading(21);
                Place(this, nav, 55, 205 + i * 59, 230, 44);
                navigation[page] = nav;
            }
            AddText(this, "INDEPENDENT FAN PROJECT", 57, 795, 238, 20, 10, true);
            AddText(this, DisplayVersion(), 57, 817, 238, 20, 9, false);

            pageTitle = AddText(this, "CONTROLLER", 342, 36, 660, 70, 44, true);
            AddText(this, "RADIO INPUT  /  XBOX OUTPUT", 344, 109, 610, 24, 11, true);
            AddText(this, "DEVICE", 344, 158, 90, 32, 19, true);
            deviceBox.FlatStyle = FlatStyle.Flat;
            deviceBox.BackColor = Color.FromArgb(23, 24, 25);
            deviceBox.ForeColor = Color.WhiteSmoke;
            deviceBox.Font = new Font("Segoe UI", 11);
            Place(this, deviceBox, 457, 158, 480, 32);
            StyleButton(refreshButton);
            refreshButton.Text = "REFRESH";
            Place(this, refreshButton, 948, 156, 90, 33);
            radioState = AddText(this, "NO RADIO", 458, 198, 215, 25, 12, true);
            driverLabel.Text = "CHECKING DRIVER";
            driverLabel.Font = Heading(12);
            Place(this, driverLabel, 700, 198, 280, 25);
            StyleButton(driverButton);
            driverButton.Text = "INSTALL DRIVER";
            Place(this, driverButton, 815, 197, 222, 29);

            var controller = NewPage("CONTROLLER");
            leftScope = new StickScope { Caption = "LEFT STICK", Font = Heading(18), Name = "axis-preview" };
            rightScope = new StickScope { Caption = "RIGHT STICK", Font = Heading(18), Name = "axis-preview" };
            Place(controller, leftScope, 0, 0, 263, 256);
            Place(controller, rightScope, 390, 0, 263, 256);
            PreviewRow(controller, StickThrottle, 0, 268);
            PreviewRow(controller, StickYaw, 0, 310);
            PreviewRow(controller, StickPitch, 390, 268);
            PreviewRow(controller, StickRoll, 390, 310);

            // Move the existing controls intact, so mapping and calibration keep their handlers.
            AdoptPage("CALIBRATION", SticksPage());
            AdoptPage("SWITCHES", SwitchesPage());
            AdoptPage("BUTTONS", ButtonsPage());
            var advanced = NewPage("ADVANCED");
            AddText(advanced, "RADIO DIAGNOSTICS", 0, 0, 680, 32, 19, true);
            rawLabel.Font = new Font("Consolas", 11);
            rawLabel.Text = "No radio input.";
            rawLabel.Padding = new Padding(12);
            Place(advanced, rawLabel, 0, 42, 684, 254);
            rawLabel.BackColor = Color.FromArgb(23, 24, 25);
            rawLabel.ForeColor = Color.WhiteSmoke;
            autoStartBox.Font = Heading(17);
            autoStartBox.Text = "START AUTOMATICALLY";
            Place(advanced, autoStartBox, 0, 310, 480, 30);
            Place(advanced, ActionButton("FONT LICENSE", ShowFontLicense), 496, 310, 176, 32);

            cockpitArm = AddText(this, "ARM  /  --", 352, 620, 198, 27, 17, true);
            cockpitAcro = AddText(this, "ACRO SWITCH  /  --", 577, 620, 258, 27, 17, true);
            var calibration = ActionButton("CALIBRATE", () => Navigate("CALIBRATION"));
            Place(this, calibration, 847, 615, 190, 35);
            outputTitle = AddText(this, "CONNECT YOUR RADIO", 344, 681, 420, 32, 20, true);
            Place(this, statusLabel, 344, 719, 685, 30);
            statusLabel.Text = "Connect a radio in USB joystick mode to begin.";
            statusLabel.Font = new Font("Segoe UI", 9);
            startButton.Name = "bridge-start";
            StyleButton(startButton);
            startButton.Font = Heading(25);
            startButton.BackColor = Color.FromArgb(238, 238, 233);
            startButton.ForeColor = Color.FromArgb(18, 19, 20);
            startButton.Enabled = false;
            Place(this, startButton, 827, 671, 210, 45);
            Place(this, promptLabel, 344, 751, 690, 34);
            promptLabel.ForeColor = Color.WhiteSmoke;
            promptLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            Place(this, warningLabel, 344, 787, 690, 60);
            warningLabel.ForeColor = Color.Gainsboro;
            warningLabel.Font = new Font("Segoe UI", 9);
            warningLabel.Text = "Keep ARM off when on foot.";
            Navigate("CONTROLLER");
            foreach (var page in pages.Values) MapPage(page, page);
            Resize += (s, e) => ArrangeCockpit();
            ResumeLayout(false);
            ArrangeCockpit();
        }

        // Holds the layout of a control and of every container below it, so many bound and font changes cost one
        // layout pass instead of re-measuring a whole table after each change.
        static List<Control> SuspendTree(Control root)
        {
            var containers = new List<Control>();
            CollectContainers(root, containers);
            foreach (var c in containers) c.SuspendLayout();
            return containers;
        }

        static void ResumeTree(List<Control> containers, bool performLayout)
        {
            foreach (var c in containers) c.ResumeLayout(performLayout);   // parents first, they size their children
        }

        static void CollectContainers(Control parent, List<Control> list)
        {
            list.Add(parent);
            foreach (Control c in parent.Controls) if (c.HasChildren) CollectContainers(c, list);
        }

        Panel NewPage(string name)
        {
            var panel = new ClearPanel { BackColor = Color.Transparent, AutoScroll = true };
            Place(this, panel, 344, 250, 694, 352);
            pages[name] = panel;
            return panel;
        }

        void AdoptPage(string name, TabPage oldPage)
        {
            var page = NewPage(name);
            foreach (Control control in oldPage.Controls.Cast<Control>().ToArray()) page.Controls.Add(control);
            oldPage.Dispose();
            var held = SuspendTree(page);
            ThemeTree(page);
            ResumeTree(held, false);   // ArrangeCockpit lays everything out once at the end of BuildLayout
        }

        void ThemeTree(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                c.ForeColor = Color.Gainsboro;
                if (c is Button button) StyleButton(button);
                else if (c is ComboBox box)
                {
                    box.FlatStyle = FlatStyle.Flat;
                    box.BackColor = Color.FromArgb(30, 31, 32);
                }
                else if (c is Panel || c is Label || c is CheckBox) c.BackColor = Color.Transparent;
                if (c is Label label && label.AutoSize) label.MaximumSize = new Size(620, 0);
                ThemeTree(c);
                textSizes[c] = c.Font.Size;
                if (c is TableLayoutPanel table)
                    foreach (ColumnStyle column in table.ColumnStyles)
                        if (column.SizeType == SizeType.Absolute) { columnWidths[column] = column.Width; columnOwners[column] = table; }
            }
        }

        void MapPage(Control parent, Panel page)
        {
            foreach (Control c in parent.Controls) { pageOf[c] = page; MapPage(c, page); }
        }

        // A page other than the active one, or a control on it. Resizing skips those, so it only re-measures what is on
        // screen; Navigate brings a page to the current size when it is opened.
        bool OnHiddenPage(Control c) => c is Panel panel && stalePages.Contains(panel) || pageOf.TryGetValue(c, out var page) && stalePages.Contains(page);

        Button ActionButton(string text, Action click)
        {
            var button = new Button { Text = text, Cursor = Cursors.Hand };
            StyleButton(button);
            button.Click += (s, e) => click();
            return button;
        }

        void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.FromArgb(24, 25, 26);
            button.ForeColor = Color.WhiteSmoke;
            button.FlatAppearance.BorderColor = Color.FromArgb(115, 117, 117);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 66, 67);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(90, 91, 92);
            button.Font = Heading(13);
            if (button.AutoSize) button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(78, 30);
        }

        Label AddText(Control parent, string text, int x, int y, int w, int h, float size, bool heading)
        {
            var label = new Label { Text = text, BackColor = Color.Transparent, ForeColor = Color.Gainsboro,
                Font = heading ? Heading(size) : new Font("Segoe UI", size), UseMnemonic = false };
            Place(parent, label, x, y, w, h);
            return label;
        }

        void Place(Control parent, Control control, int x, int y, int w, int h)
        {
            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.AutoSize = false;
            control.MaximumSize = Size.Empty;
            control.Bounds = new Rectangle(x, y, w, h);
            if (control is Label || control is CheckBox) control.BackColor = Color.Transparent;
            parent.Controls.Add(control);
            positions[control] = control.Bounds;
            textSizes[control] = control.Font.Size;
        }

        void PreviewRow(Panel parent, string stick, int x, int y)
        {
            AddText(parent, stick.ToUpperInvariant(), x, y + 5, 116, 28, 17, true);
            readouts[stick] = AddText(parent, "--", x + 118, y + 5, 63, 28, 13, true);
            var assign = ActionButton("ASSIGN", () => StartLearn("stick:" + stick));
            Place(parent, assign, x + 188, y, 75, 32);
        }

        void Navigate(string name)
        {
            if (activePage == name && pages[name].Visible) return;
            SuspendLayout();
            try
            {
            activePage = name;
            var target = pages[name];
            if (stalePages.Contains(target)) ArrangeCockpit();   // bring it to the current size before it shows
            FinishPreparing(target);
            pageTitle.Text = name;
            // If the page has no windows yet, Windows creates one per control as it shows; hold the layout meanwhile,
            // otherwise every new combo box and button re-measures the whole table (one second on BUTTONS). A page
            // that already has its windows is laid out at this size and needs no new layout pass.
            bool firstShow = !target.IsHandleCreated;
            var held = SuspendTree(target);
            foreach (var page in pages) page.Value.Visible = page.Value == target;
            ResumeTree(held, firstShow);
            foreach (var nav in navigation)
            {
                bool selected = nav.Key == name;
                nav.Value.BackColor = selected ? Color.FromArgb(236, 236, 232) : Color.Transparent;
                nav.Value.ForeColor = selected ? Color.FromArgb(15, 16, 17) : Color.Gainsboro;
                nav.Value.FlatAppearance.BorderSize = selected ? 1 : 0;
            }
            }
            finally { ResumeLayout(true); }
        }

        void ShowFontLicense()
        {
            using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("BodycamFpvFix.OFL.txt"))
            using (var reader = new StreamReader(stream))
            using (var dialog = new Form { Text = "Bebas Neue · SIL Open Font License", Size = new Size(680, 520), StartPosition = FormStartPosition.CenterParent })
            {
                dialog.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill, Text = reader.ReadToEnd(), BackColor = Color.FromArgb(24, 25, 26), ForeColor = Color.Gainsboro });
                dialog.ShowDialog(this);
            }
        }

        void ReplaceSizedFont(Control control, Font font)
        {
            control.Font = font;
            ownedFonts.Add(font);
            Font previous;
            if (resizedFonts.TryGetValue(control, out previous))
            {
                int index = ownedFonts.FindIndex(item => ReferenceEquals(item, previous));
                if (index >= 0) ownedFonts.RemoveAt(index);
                previous.Dispose();
            }
            resizedFonts[control] = font;
        }
        // Bounds, fixed column widths and fonts at one scale, for the controls that `include` accepts. Combo boxes are
        // collected in `last` instead of moved (see ArrangeCockpit).
        void ApplyScale(float scale, Func<Control, bool> include, List<KeyValuePair<Control, Rectangle>> last)
        {
            foreach (var item in positions)
            {
                if (!include(item.Key)) continue;
                var r = item.Value;
                var bounds = new Rectangle((int)(r.X * scale), (int)(r.Y * scale), (int)(r.Width * scale), (int)(r.Height * scale));
                if (item.Key is ComboBox) last.Add(new KeyValuePair<Control, Rectangle>(item.Key, bounds));
                else item.Key.Bounds = bounds;
            }
            foreach (var column in columnWidths)
                if (include(columnOwners[column.Key])) column.Key.Width = column.Value * scale;
            // Design sizes are points at 96 DPI. Each font gets the matching pixel size from the same scale as the
            // layout, so text always fits its box, whatever display scale Windows reports. Whole pixels, as Windows
            // draws them anyway: while dragging the window edge a font only changes every few steps, which matters
            // because a combo box re-measures and repaints on every font change. Containers show no text of their own.
            foreach (var item in textSizes)
            {
                if (item.Key is Panel || !include(item.Key)) continue;
                float size = Math.Max(1f, (float)Math.Round(item.Value * scale * 96f / 72f));
                var old = item.Key.Font;
                if (old.Unit != GraphicsUnit.Pixel || Math.Abs(old.Size - size) > .1f)
                    ReplaceSizedFont(item.Key, new Font(old.FontFamily, size, old.Style, GraphicsUnit.Pixel));
            }
        }

        // After start, the hidden pages get their sizes right away (cheap while they have no windows yet) and then their
        // windows a few at a time while the program idles (PrepareSomeWindows), so opening a page for the first time
        // only has to show it. BUTTONS alone has about 80 controls, a quarter of a second to create in one go.
        void PrepareHiddenPages()
        {
            foreach (var page in pages.Values.Where(p => stalePages.Contains(p) && !p.IsHandleCreated && !preparing.ContainsKey(p)).ToList())
            {
                var held = SuspendTree(page);   // one layout pass when the page is complete
                var last = new List<KeyValuePair<Control, Rectangle>>();
                ApplyScale(layoutScale, c => c == page || pageOf.TryGetValue(c, out var p) && p == page, last);
                foreach (var move in last) move.Key.Bounds = move.Value;
                stalePages.Remove(page);
                preparing[page] = held;
                QueueWindows(page);
            }
        }

        void QueueWindows(Control c)
        {
            windowsToCreate.Enqueue(c);
            foreach (Control child in c.Controls) QueueWindows(child);
        }

        // Called by the UI timer: creates windows for about 8 ms per tick, so the program stays responsive. The pages are
        // queued one after another; a page that is complete gets its layout pass in a tick of its own.
        void PrepareSomeWindows()
        {
            if (preparing.Count == 0) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (windowsToCreate.Count > 0 && watch.ElapsedMilliseconds < 8)
            {
                var c = windowsToCreate.Dequeue();
                CreateWindow(c);
                var page = PreparingPageOf(c);
                if (page != null && (windowsToCreate.Count == 0 || PreparingPageOf(windowsToCreate.Peek()) != page))
                {
                    FinishPreparing(page);
                    break;
                }
            }
        }

        Panel PreparingPageOf(Control c) =>
            c is Panel panel && preparing.ContainsKey(panel) ? panel : pageOf.TryGetValue(c, out var page) && preparing.ContainsKey(page) ? page : null;

        // Creates the windows still missing on this page and ends its held layout (also when it is opened early).
        void FinishPreparing(Panel page)
        {
            if (!preparing.TryGetValue(page, out var held)) return;
            preparing.Remove(page);
            foreach (var c in windowsToCreate.Where(c => c == page || pageOf.TryGetValue(c, out var p) && p == page).ToList()) CreateWindow(c);
            ResumeTree(held, true);
        }

        static void CreateWindow(Control c)
        {
            if (!c.IsDisposed && !c.IsHandleCreated) { var unused = c.Handle; }
        }

        void ArrangeCockpit()
        {
            // Minimize can report a zero client area. Keep the last valid layout
            // until restore, rather than constructing zero-point fonts.
            if (arranging || positions.Count == 0 || WindowState == FormWindowState.Minimized ||
                ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            arranging = true;
            float scale = Math.Min(ClientSize.Width / 1280f, ClientSize.Height / 850f);
            layoutScale = scale;
            stalePages.Clear();
            foreach (var page in pages) if (page.Key != activePage) stalePages.Add(page.Value);
            var held = SuspendTree(this);
            // Moving a combo box makes Windows repaint the whole composited window at once. Move those last, when every
            // other box and font is final, so that repaint already shows the finished layout and never old text in new boxes.
            var last = new List<KeyValuePair<Control, Rectangle>>();
            try
            {
                int w = (int)(40 * scale), h = (int)(30 * scale);
                closeWindow.SetBounds(ClientSize.Width - (int)(45 * scale), 0, w, h);
                maximizeWindow.SetBounds(ClientSize.Width - (int)(85 * scale), 0, w, h);
                minimizeWindow.SetBounds(ClientSize.Width - (int)(125 * scale), 0, w, h);
                ApplyScale(scale, c => !OnHiddenPage(c), last);
            }
            finally
            {
                ResumeTree(held, true);
                foreach (var move in last) move.Key.Bounds = move.Value;
                arranging = false;
                Invalidate();
            }
        }

        void UpdateCockpit()
        {
            if (leftScope == null) return;
            bool connected = bridge != null && bridge.DeviceConnected && bridge.Last != null;
            radioState.Text = connected ? "USB CONNECTED" : "NO RADIO";
            outputTitle.Text = !connected ? "CONNECT YOUR RADIO" : bridge.OutputActive ? "CONTROLLER RUNNING" : "READY TO CONNECT";
            var o = connected ? bridge.Output : null;
            leftScope.SetValues(o?.Yaw ?? 0, o?.Throttle ?? -1, connected);
            rightScope.SetValues(o?.Roll ?? 0, o?.Pitch ?? 0, connected);
            readouts[StickThrottle].Text = o == null ? "--" : ((o.Throttle + 1) * 50).ToString("0") + "%";
            readouts[StickYaw].Text = o == null ? "--" : (o.Yaw * 100).ToString("+0;-0;0") + "%";
            readouts[StickPitch].Text = o == null ? "--" : (o.Pitch * 100).ToString("+0;-0;0") + "%";
            readouts[StickRoll].Text = o == null ? "--" : (o.Roll * 100).ToString("+0;-0;0") + "%";
            cockpitArm.Text = "ARM  /  " + (!connected ? "--" : string.IsNullOrEmpty(profile.ArmSource) ? "UNASSIGNED" : o.Armed ? "ON" : "OFF");
            cockpitArm.ForeColor = o != null && o.Armed ? Color.Salmon : Color.Gainsboro;
            cockpitAcro.Text = "ACRO SWITCH  /  " + (!connected ? "--" : string.IsNullOrEmpty(profile.AcroSource) ? "UNASSIGNED" : Source.IsActive(profile.AcroSource, bridge.Last) ? "ON" : "OFF");
            if (!connected)
            {
                warningLabel.Text = "Keep ARM off when on foot.";
                foreach (var bar in bars.Values) { bar.Value = bar.FromLeft ? -1 : 0; bar.Dimmed = true; }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (IsDisposed || Disposing || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            if (backgroundFrame == null || backgroundFrame.Size != ClientSize)
            {
                backgroundFrame?.Dispose();
                // Premultiplied pixels copy several times faster, and every transparent label copies its part of this frame.
                backgroundFrame = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(backgroundFrame))
                    RenderBackground(graphics);
            }
            // Copy only the rectangle being painted: a transparent control asks for just its own bounds.
            var part = Rectangle.Intersect(e.ClipRectangle, new Rectangle(Point.Empty, backgroundFrame.Size));
            if (part.Width <= 0 || part.Height <= 0) return;
            var mode = e.Graphics.CompositingMode;
            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            e.Graphics.DrawImage(backgroundFrame, part, part, GraphicsUnit.Pixel);
            e.Graphics.CompositingMode = mode;
        }

        // Compose child windows together so navigation never exposes half-painted controls.
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return parameters;
            }
        }

        void RenderBackground(Graphics graphics)
        {
            graphics.Clear(Color.FromArgb(14, 15, 16));
            if (room != null)
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(room, ClientRectangle);
                using (var shade = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) graphics.FillRectangle(shade, ClientRectangle);
                using (var shade = new LinearGradientBrush(ClientRectangle, Color.FromArgb(60, 0, 0, 0), Color.Transparent, 0f))
                    graphics.FillRectangle(shade, ClientRectangle);
            }
            float scale = Math.Min(ClientSize.Width / 1280f, ClientSize.Height / 850f);
            using (var pen = new Pen(Color.FromArgb(130, 182, 182, 178)))
            {
                graphics.DrawLine(pen, 309 * scale, 30 * scale, 309 * scale, 830 * scale);
                foreach (int y in new[] { 142, 232, 607, 664 }) graphics.DrawLine(pen, 344 * scale, y * scale, 1038 * scale, y * scale);
                graphics.DrawLine(pen, 57 * scale, 187 * scale, 285 * scale, 187 * scale);
                graphics.DrawLine(pen, 57 * scale, 779 * scale, 285 * scale, 779 * scale);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x84 || (int)m.Result != 1) return;
            var p = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
            if (WindowState == FormWindowState.Normal)
            {
                bool left = p.X < 7, right = p.X >= ClientSize.Width - 7;
                bool top = p.Y < 7, bottom = p.Y >= ClientSize.Height - 7;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1;
                if (hit != 1) { m.Result = (IntPtr)hit; return; }
            }
            if (p.Y < (closeWindow?.Height ?? 30)) m.Result = (IntPtr)2;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                // Closing destroys the child windows first, which can still paint the background once.
                room?.Dispose();
                room = null;
                backgroundFrame?.Dispose();
                backgroundFrame = null;
            }
            base.Dispose(disposing);
            if (disposing)
            {
                titleFonts.Dispose();
                foreach (var font in ownedFonts) font.Dispose();
                if (nativeFont != IntPtr.Zero) { RemoveFontMemResourceEx(nativeFont); nativeFont = IntPtr.Zero; }
                if (fontMemory != IntPtr.Zero) { Marshal.FreeCoTaskMem(fontMemory); fontMemory = IntPtr.Zero; }
            }
        }
    }

    sealed class DarkComboBox : ComboBox
    {
        public DarkComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.FromArgb(23, 24, 25);
            ForeColor = Color.WhiteSmoke;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Color.FromArgb(65, 66, 67) : BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : "—";
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0xF || !IsHandleCreated) return;
            using (var g = Graphics.FromHwnd(Handle))
            using (var brush = new SolidBrush(BackColor))
            using (var border = new Pen(Color.Gray))
            {
                g.FillRectangle(brush, ClientRectangle);
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                TextRenderer.DrawText(g, SelectedItem == null ? "—" : GetItemText(SelectedItem), Font,
                    new Rectangle(6, 0, Math.Max(1, Width - 29), Height), ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                g.DrawLines(border, new[] { new Point(Width - 19, Height / 2 - 2), new Point(Width - 14, Height / 2 + 3), new Point(Width - 9, Height / 2 - 2) });
                if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 26, Height - 6), ForeColor, BackColor);
            }
        }
    }

    sealed class ClearPanel : Panel
    {
        public ClearPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    sealed class StickScope : Control
    {
        public string Caption;
        double x, y;
        bool connected;
        Font hintFont;   // "NO INPUT", half the caption size so it grows with the window
        public StickScope()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            AccessibleName = "Radio stick input preview";
            AccessibleRole = AccessibleRole.Indicator;
        }
        public void SetValues(double horizontal, double vertical, bool live)
        {
            if (Math.Abs(horizontal - x) < .001 && Math.Abs(vertical - y) < .001 && connected == live) return;
            x = Math.Max(-1, Math.Min(1, horizontal)); y = Math.Max(-1, Math.Min(1, vertical)); connected = live;
            Invalidate();
        }
        protected override void OnFontChanged(EventArgs e)
        {
            hintFont?.Dispose();
            hintFont = new Font("Segoe UI", Math.Max(1f, Font.Size / 2), Font.Unit);
            base.OnFontChanged(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { hintFont?.Dispose(); hintFont = null; }
            base.Dispose(disposing);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Width / 263f;
            TextRenderer.DrawText(g, Caption, Font, new Point(0, 0), Color.Gainsboro, TextFormatFlags.NoPadding);
            var r = new RectangleF(1, 36 * scale, Width - 3, Height - 38 * scale);
            using (var pen = new Pen(Color.FromArgb(175, 178, 178)))
            {
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                float half = Math.Min(r.Width, r.Height) / 2 - 24 * scale;
                g.DrawLine(pen, cx - half, cy, cx + half, cy);
                g.DrawLine(pen, cx, cy - half, cx, cy + half);
                if (connected) g.FillEllipse(Brushes.WhiteSmoke, cx + (float)x * half - 6 * scale, cy - (float)y * half - 6 * scale, 12 * scale, 12 * scale);
                else TextRenderer.DrawText(g, "NO INPUT", hintFont ?? Font, new Rectangle((int)r.X, (int)(cy + 20 * scale), (int)r.Width, (int)(30 * scale)), Color.Gray, TextFormatFlags.HorizontalCenter);
            }
        }
    }
}
