using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BodycamFpvFix
{
    sealed class MainForm : Form
    {
        enum Learn { None, Throttle, Yaw, Pitch, Roll, Arm, Acro }

        readonly AppSettings settings = AppSettings.Load();
        Bridge bridge;
        DeviceProfile profile;
        bool loading;

        readonly ComboBox deviceBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly Button refreshButton = new Button { AutoSize = true };
        readonly Label driverLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Button driverButton = new Button { AutoSize = true, Visible = false };

        readonly Dictionary<Learn, ComboBox> axisBoxes = new Dictionary<Learn, ComboBox>();
        readonly Dictionary<Learn, CheckBox> invertBoxes = new Dictionary<Learn, CheckBox>();
        readonly Dictionary<Learn, Bar> bars = new Dictionary<Learn, Bar>();
        readonly Label armSourceLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label acroSourceLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label armStateLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly CheckBox lockBox = new CheckBox { AutoSize = true };
        readonly Label rawLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 8.5f) };

        readonly Button startButton = new Button { Height = 36, Dock = DockStyle.Fill };
        readonly CheckBox autoStartBox = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label statusLabel = new Label { AutoSize = true, MaximumSize = new Size(600, 0) };
        readonly Label warningLabel = new Label { AutoSize = true, MaximumSize = new Size(600, 0), ForeColor = Color.Firebrick };
        readonly Label learnLabel = new Label { AutoSize = true, MaximumSize = new Size(600, 0), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        readonly Timer timer = new Timer { Interval = 40 };

        Learn learning = Learn.None;
        DateTime learnStarted;
        InputState learnBase;
        string learnCandidate;
        DateTime learnCandidateSince;
        DateTime lastRawUpdate;

        public MainForm()
        {
            Text = "Bodycam FPV Fix";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(640, 760);
            MinimumSize = new Size(560, 640);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildLayout();

            deviceBox.SelectedIndexChanged += (s, e) => SelectDevice(deviceBox.SelectedItem as HidDeviceInfo);
            refreshButton.Click += (s, e) => RefreshDevices();
            driverButton.Click += async (s, e) => await InstallDriver();
            startButton.Click += (s, e) => ToggleOutput();
            autoStartBox.CheckedChanged += (s, e) => { settings.AutoStart = autoStartBox.Checked; settings.Save(); };
            lockBox.CheckedChanged += (s, e) => { if (!loading && profile != null) { profile.LockThrottleWhenDisarmed = lockBox.Checked; settings.Save(); } };
            timer.Tick += (s, e) => Tick();

            autoStartBox.Checked = settings.AutoStart;
            Shown += async (s, e) =>
            {
                RefreshDevices();
                await CheckDriver();
                if (settings.AutoStart && bridge != null && driverButton.Visible == false) bridge.OutputWanted = true;
                timer.Start();
            };
            FormClosing += (s, e) => { timer.Stop(); settings.Save(); bridge?.Dispose(); };
        }

        // ---------- Layout ----------

        void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), AutoScroll = true };
            Controls.Add(root);

            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.Controls.Add(new Label { Text = "Radio:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            top.Controls.Add(deviceBox, 1, 0);
            refreshButton.Text = "Refresh";
            top.Controls.Add(refreshButton, 2, 0);
            var driverRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            driverRow.Controls.Add(driverLabel);
            driverButton.Text = "Install driver (one admin prompt)";
            driverRow.Controls.Add(driverButton);
            top.Controls.Add(driverRow, 0, 1);
            top.SetColumnSpan(driverRow, 3);
            root.Controls.Add(top);

            // Sticks
            var sticks = new GroupBox { Text = "Sticks (Mode 2)", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            var st = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5 };
            st.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            st.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            st.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            st.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            st.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            AddStickRow(st, 0, Learn.Throttle, "Throttle");
            AddStickRow(st, 1, Learn.Yaw, "Yaw");
            AddStickRow(st, 2, Learn.Pitch, "Pitch");
            AddStickRow(st, 3, Learn.Roll, "Roll");
            sticks.Controls.Add(st);
            root.Controls.Add(sticks);

            // Switches
            var switches = new GroupBox { Text = "Switches", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
            var sw = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4 };
            sw.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sw.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sw.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sw.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            AddSwitchRow(sw, 0, Learn.Arm, "Arm (RB)", armSourceLabel);
            AddSwitchRow(sw, 1, Learn.Acro, "Acro mode (LB)", acroSourceLabel);
            lockBox.Text = "Lock throttle while the arm switch is off (your character does not walk backwards)";
            sw.Controls.Add(lockBox, 0, 2);
            sw.SetColumnSpan(lockBox, 4);
            sw.Controls.Add(armStateLabel, 0, 3);
            sw.SetColumnSpan(armStateLabel, 4);
            switches.Controls.Add(sw);
            root.Controls.Add(switches);

            // Raw input
            var raw = new GroupBox { Text = "Raw input from the radio", Dock = DockStyle.Top, Height = 110, Padding = new Padding(8) };
            raw.Controls.Add(rawLabel);
            root.Controls.Add(raw);

            // Start / status
            var bottom = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(startButton, 0, 0);
            autoStartBox.Text = "Start automatically";
            bottom.Controls.Add(autoStartBox, 1, 0);
            root.Controls.Add(bottom);
            root.Controls.Add(learnLabel);
            root.Controls.Add(statusLabel);
            root.Controls.Add(warningLabel);

            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(600, 0),
                ForeColor = SystemColors.GrayText,
                Text = "In Bodycam: take out the drone, flip the Acro switch (LB), throttle down, then switch Arm on (RB). " +
                       "Keep Arm on while flying, switch it off when you are back on foot. " +
                       "Tip: the default drone RC Rate in Bodycam is 2.0, which is very twitchy; around 1.0 feels like a normal quad."
            };
            root.Controls.Add(hint);
        }

        void AddStickRow(TableLayoutPanel t, int row, Learn kind, string title)
        {
            t.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left, Width = 70 }, 0, row);
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            box.SelectedIndexChanged += (s, e) => { if (!loading && profile != null) { StickOf(kind).Axis = box.SelectedItem as string ?? ""; settings.Save(); } };
            axisBoxes[kind] = box;
            t.Controls.Add(box, 1, row);
            var inv = new CheckBox { Text = "invert", AutoSize = true, Anchor = AnchorStyles.Left };
            inv.CheckedChanged += (s, e) => { if (!loading && profile != null) { StickOf(kind).Invert = inv.Checked; settings.Save(); } };
            invertBoxes[kind] = inv;
            t.Controls.Add(inv, 2, row);
            var bar = new Bar { Dock = DockStyle.Fill, Height = 18, Margin = new Padding(6, 6, 6, 6), FromLeft = kind == Learn.Throttle };
            bars[kind] = bar;
            t.Controls.Add(bar, 3, row);
            var learn = new Button { Text = "Learn", AutoSize = true };
            learn.Click += (s, e) => StartLearn(kind);
            t.Controls.Add(learn, 4, row);
        }

        void AddSwitchRow(TableLayoutPanel t, int row, Learn kind, string title, Label sourceLabel)
        {
            t.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            t.Controls.Add(sourceLabel, 1, row);
            var learn = new Button { Text = "Learn", AutoSize = true };
            learn.Click += (s, e) => StartLearn(kind);
            t.Controls.Add(learn, 2, row);
            var clear = new Button { Text = "Clear", AutoSize = true };
            clear.Click += (s, e) =>
            {
                if (profile == null) return;
                if (kind == Learn.Arm) profile.ArmSource = ""; else profile.AcroSource = "";
                settings.Save();
                ShowProfile();
            };
            t.Controls.Add(clear, 3, row);
        }

        StickMap StickOf(Learn kind)
        {
            switch (kind)
            {
                case Learn.Throttle: return profile.Throttle;
                case Learn.Yaw: return profile.Yaw;
                case Learn.Pitch: return profile.Pitch;
                default: return profile.Roll;
            }
        }

        // ---------- Devices and driver ----------

        void RefreshDevices()
        {
            var current = deviceBox.SelectedItem as HidDeviceInfo;
            var list = HidJoystick.FindJoysticks();
            deviceBox.BeginUpdate();
            deviceBox.Items.Clear();
            foreach (var d in list) deviceBox.Items.Add(d);
            deviceBox.EndUpdate();
            if (list.Count == 0)
            {
                SelectDevice(null);
                statusLabel.Text = "No radio found. Connect it by USB and choose USB joystick (HID) mode on the radio, then press Refresh.";
                return;
            }
            string want = current?.Key ?? settings.LastDeviceKey;
            var pick = list.FirstOrDefault(d => d.Key == want)
                       ?? list.FirstOrDefault(d => d.Name.IndexOf("BETAFPV", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? list[0];
            deviceBox.SelectedItem = pick;
        }

        void SelectDevice(HidDeviceInfo device)
        {
            if (bridge != null && device != null && bridge.Profile == settings.ProfileFor(device) && bridge.DeviceConnected) return;
            bool wasRunning = bridge?.OutputWanted ?? false;
            bridge?.Dispose();
            bridge = null;
            profile = null;
            if (device == null) return;
            profile = settings.ProfileFor(device);
            settings.LastDeviceKey = device.Key;
            settings.Save();
            bridge = new Bridge(device, profile) { OutputWanted = wasRunning };
            ShowProfile();
        }

        async Task CheckDriver()
        {
            bool ok = await Task.Run(() => VirtualPad.DriverInstalled());
            driverLabel.Text = ok ? "ViGEmBus driver: installed"
                                  : "ViGEmBus driver missing (needed for the virtual Xbox controller).";
            driverLabel.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
            driverButton.Visible = !ok;
            startButton.Enabled = ok;
        }

        async Task InstallDriver()
        {
            driverButton.Enabled = false;
            driverLabel.Text = "Downloading and starting the driver installer ...";
            string error = await Task.Run(() => DriverSetup.Install());
            driverButton.Enabled = true;
            await CheckDriver();
            if (error != null) MessageBox.Show(this, error, "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (driverButton.Visible)
                MessageBox.Show(this, "The driver is still not reachable. If the installer asked for it, restart Windows and open this program again.",
                                "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ToggleOutput()
        {
            if (bridge == null) return;
            bridge.OutputWanted = !bridge.OutputWanted;
        }

        // ---------- Profile display ----------

        void ShowProfile()
        {
            if (profile == null) return;
            loading = true;
            var names = bridge?.Axes.Select(a => a.Name).ToList() ?? new List<string>();
            foreach (var kind in new[] { Learn.Throttle, Learn.Yaw, Learn.Pitch, Learn.Roll })
            {
                var box = axisBoxes[kind];
                var m = StickOf(kind);
                box.BeginUpdate();
                box.Items.Clear();
                foreach (var n in names) box.Items.Add(n);
                if (!string.IsNullOrEmpty(m.Axis) && !names.Contains(m.Axis)) box.Items.Add(m.Axis);
                box.SelectedItem = m.Axis;
                box.EndUpdate();
                invertBoxes[kind].Checked = m.Invert;
            }
            armSourceLabel.Text = Source.Describe(profile.ArmSource);
            acroSourceLabel.Text = Source.Describe(profile.AcroSource);
            lockBox.Checked = profile.LockThrottleWhenDisarmed;
            loading = false;
        }

        // ---------- Live update ----------

        int shownAxisCount = -1;

        void Tick()
        {
            if (bridge == null)
            {
                startButton.Text = "Start";
                startButton.Enabled = false;
                rawLabel.Text = "";
                return;
            }
            if (bridge.Axes.Count != shownAxisCount) { shownAxisCount = bridge.Axes.Count; ShowProfile(); }

            startButton.Enabled = !driverButton.Visible && bridge.DeviceConnected;
            startButton.Text = bridge.OutputWanted ? "Stop" : "Start";
            startButton.BackColor = bridge.OutputActive ? Color.FromArgb(200, 235, 205) : SystemColors.Control;
            if (learning == Learn.None) statusLabel.Text = bridge.Status;
            warningLabel.Text = bridge.Warning;

            var o = bridge.Output;
            bars[Learn.Throttle].Value = o.ThrottleLocked ? 0 : o.Throttle;
            bars[Learn.Throttle].Dimmed = o.ThrottleLocked;
            bars[Learn.Yaw].Value = o.Yaw;
            bars[Learn.Pitch].Value = o.Pitch;
            bars[Learn.Roll].Value = o.Roll;
            armStateLabel.Text = string.IsNullOrWhiteSpace(profile?.ArmSource) ? ""
                : o.Armed ? "Arm switch: ON, throttle free"
                          : "Arm switch: OFF" + (o.ThrottleLocked ? ", throttle locked at center" : "");

            var s = bridge.Last;
            if (s != null && (DateTime.Now - lastRawUpdate).TotalMilliseconds > 120)
            {
                lastRawUpdate = DateTime.Now;
                var parts = bridge.Axes.Select(a => $"{a.Name}={(s.Axes.TryGetValue(a.Name, out int v) ? v : 0)}");
                string axes = string.Join("  ", parts);
                string buttons = s.Buttons.Count == 0 ? "-" : string.Join(", ", s.Buttons.OrderBy(b => b));
                rawLabel.Text = axes + Environment.NewLine + "Buttons pressed: " + buttons;
            }

            if (learning != Learn.None) LearnTick(s);
        }

        // ---------- Learn ----------

        void StartLearn(Learn kind)
        {
            if (bridge?.Last == null || profile == null)
            {
                learnLabel.Text = "Connect the radio first.";
                return;
            }
            learning = kind;
            learnStarted = DateTime.Now;
            learnBase = Copy(bridge.Last);
            learnCandidate = null;
            switch (kind)
            {
                case Learn.Throttle: learnLabel.Text = "Push the THROTTLE stick fully UP and hold it."; break;
                case Learn.Yaw: learnLabel.Text = "Move the LEFT stick fully RIGHT and hold it."; break;
                case Learn.Pitch: learnLabel.Text = "Push the RIGHT stick fully UP (forward) and hold it."; break;
                case Learn.Roll: learnLabel.Text = "Move the RIGHT stick fully RIGHT and hold it."; break;
                case Learn.Arm: learnLabel.Text = "Flip your ARM switch to the ON position."; break;
                case Learn.Acro: learnLabel.Text = "Flip the switch you want for Acro mode."; break;
            }
        }

        void LearnTick(InputState s)
        {
            if ((DateTime.Now - learnStarted).TotalSeconds > 12)
            {
                learning = Learn.None;
                learnLabel.Text = "Nothing detected. Press Learn and try again.";
                return;
            }
            if (s == null) return;

            string found = null;
            if (learning == Learn.Arm || learning == Learn.Acro) found = DetectSwitch(s);
            else found = DetectStick(s);

            if (found == null) { learnCandidate = null; return; }
            if (found != learnCandidate) { learnCandidate = found; learnCandidateSince = DateTime.Now; return; }
            double holdMs = learning == Learn.Arm || learning == Learn.Acro ? 150 : 350;
            if ((DateTime.Now - learnCandidateSince).TotalMilliseconds < holdMs) return;

            var kind = learning;
            learning = Learn.None;
            if (kind == Learn.Arm) profile.ArmSource = found;
            else if (kind == Learn.Acro) profile.AcroSource = found;
            else
            {
                // found = "<axis>|+" or "<axis>|-": "+" means the raw value rises towards up/right.
                var parts = found.Split('|');
                StickOf(kind).Axis = parts[0];
                StickOf(kind).Invert = parts[1] == "-";
            }
            settings.Save();
            ShowProfile();
            learnLabel.Text = "Learned: " + found.Replace("|+", "").Replace("|-", " (inverted)");
        }

        string DetectStick(InputState s)
        {
            string best = null;
            double bestDev = 0;
            foreach (var a in bridge.Axes)
            {
                if (!s.Axes.TryGetValue(a.Name, out int v) || !learnBase.Axes.TryGetValue(a.Name, out int b)) continue;
                double dev = (double)(v - b) / Math.Max(1, a.Max - a.Min);
                if (Math.Abs(dev) > Math.Abs(bestDev)) { bestDev = dev; best = a.Name; }
            }
            if (best == null || Math.Abs(bestDev) < 0.35) return null;
            return best + (bestDev > 0 ? "|+" : "|-");
        }

        string DetectSwitch(InputState s)
        {
            foreach (int b in s.Buttons) if (!learnBase.Buttons.Contains(b)) return "Button " + b;
            foreach (int b in learnBase.Buttons) if (!s.Buttons.Contains(b)) return "!Button " + b;

            var stickAxes = new HashSet<string> { profile.Throttle.Axis, profile.Yaw.Axis, profile.Pitch.Axis, profile.Roll.Axis };
            foreach (var a in bridge.Axes)
            {
                if (stickAxes.Contains(a.Name)) continue;
                if (!s.Axes.TryGetValue(a.Name, out int v) || !learnBase.Axes.TryGetValue(a.Name, out int b)) continue;
                if (Math.Abs(v - b) < (a.Max - a.Min) * 0.35) continue;
                int limit = (v + b) / 2;   // halfway between old and new position also works for 3-position switches
                return v > b ? $"{a.Name}>{limit}" : $"{a.Name}<{limit}";
            }
            return null;
        }

        static InputState Copy(InputState s)
        {
            var c = new InputState();
            foreach (var kv in s.Axes) c.Axes[kv.Key] = kv.Value;
            foreach (var b in s.Buttons) c.Buttons.Add(b);
            return c;
        }
    }

    /// <summary>Horizontal bar: -1..1 from the center, or 0..1 from the left for the throttle.</summary>
    sealed class Bar : Control
    {
        double value;
        public bool FromLeft;
        public bool Dimmed;

        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public double Value
        {
            get => value;
            set { if (Math.Abs(value - this.value) > 0.001) { this.value = value; Invalidate(); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = ClientRectangle;
            g.Clear(Color.FromArgb(235, 235, 235));
            using (var fill = new SolidBrush(Dimmed ? Color.Silver : Color.FromArgb(70, 130, 200)))
            {
                if (FromLeft)
                {
                    int w = (int)Math.Round((value + 1) / 2 * r.Width);
                    g.FillRectangle(fill, 0, 0, w, r.Height);
                }
                else
                {
                    int mid = r.Width / 2;
                    int w = (int)Math.Round(Math.Abs(value) * mid);
                    g.FillRectangle(fill, value >= 0 ? mid : mid - w, 0, w, r.Height);
                    g.DrawLine(Pens.Gray, mid, 0, mid, r.Height);
                }
            }
            string label = FromLeft ? $"{(value + 1) * 50:0}%" : $"{value * 100:+0;-0;0}%";
            TextRenderer.DrawText(g, label, Font, r, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            g.DrawRectangle(Pens.DarkGray, 0, 0, r.Width - 1, r.Height - 1);
        }
    }
}
