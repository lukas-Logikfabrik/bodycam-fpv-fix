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
        const string StickThrottle = "Throttle", StickYaw = "Yaw", StickPitch = "Pitch", StickRoll = "Roll";
        static readonly string[] Sticks = { StickThrottle, StickYaw, StickPitch, StickRoll };
        static readonly string[] ModeKeys = { ButtonMap.Hold, ButtonMap.TapOnFlip, ButtonMap.TapWhenOn };
        static readonly string[] ModeNames = { "Hold", "Tap on every flip", "Tap when switched on" };

        readonly AppSettings settings = AppSettings.Load();
        Bridge bridge;
        DeviceProfile profile;
        bool loading;
        bool driverInstallTried;
        bool driverReady;
        DateTime lastScan;

        // Top
        readonly ComboBox deviceBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly Button refreshButton = new Button { Text = "Refresh", AutoSize = true };
        readonly Label driverLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Button driverButton = new Button { Text = "Install driver (one admin prompt)", AutoSize = true, Visible = false };

        // Sticks tab
        readonly Dictionary<string, ComboBox> axisBoxes = new Dictionary<string, ComboBox>();
        readonly Dictionary<string, CheckBox> invertBoxes = new Dictionary<string, CheckBox>();
        readonly Dictionary<string, Bar> bars = new Dictionary<string, Bar>();
        readonly Button calibrateButton = new Button { Text = "Calibrate", AutoSize = true };
        readonly Button resetCalButton = new Button { Text = "Reset calibration", AutoSize = true };
        readonly Label calLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };

        // Switches tab
        readonly Label armSourceLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label acroSourceLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label armStateLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly CheckBox lockBox = new CheckBox { AutoSize = true, Text = "Lock throttle while the arm switch is off (your character does not walk backwards)" };

        // Buttons tab
        readonly Dictionary<string, Label> buttonSourceLabels = new Dictionary<string, Label>();
        readonly Dictionary<string, ComboBox> buttonModeBoxes = new Dictionary<string, ComboBox>();
        readonly Dictionary<string, Label> buttonNameLabels = new Dictionary<string, Label>();

        // Raw tab
        readonly Label rawLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

        // Bottom
        readonly Button startButton = new Button { Text = "Start", Height = 36, Dock = DockStyle.Fill };
        readonly CheckBox autoStartBox = new CheckBox { Text = "Start automatically", AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Label promptLabel = new Label { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        readonly Label statusLabel = new Label { AutoSize = true, MaximumSize = new Size(640, 0) };
        readonly Label warningLabel = new Label { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Color.Firebrick };
        readonly Timer timer = new Timer { Interval = 40 };

        // Learn: "stick:Throttle", "arm", "acro" or "btn:A"
        string learning;
        DateTime learnStarted;
        InputState learnBase;
        string learnCandidate;
        DateTime learnCandidateSince;

        // Calibration wizard: 0 = off, 1 = centers, 2 = ranges
        int calStep;
        Dictionary<string, int> calCenter, calMin, calMax;

        DateTime lastRawUpdate;
        Font boldFont;
        int shownAxisCount = -1;

        public MainForm()
        {
            Text = "Bodycam FPV Fix " + DisplayVersion();
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(700, 660);
            MinimumSize = new Size(620, 640);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            boldFont = new Font(Font, FontStyle.Bold);
            BuildLayout();

            deviceBox.SelectedIndexChanged += (s, e) => SelectDevice(deviceBox.SelectedItem as HidDeviceInfo);
            refreshButton.Click += (s, e) => RefreshDevices();
            driverButton.Click += async (s, e) => await InstallDriver();
            startButton.Click += (s, e) => { if (bridge != null) bridge.OutputWanted = !bridge.OutputWanted; };
            autoStartBox.CheckedChanged += (s, e) => { settings.AutoStart = autoStartBox.Checked; settings.Save(); };
            lockBox.CheckedChanged += (s, e) => { if (!loading && profile != null) { profile.LockThrottleWhenDisarmed = lockBox.Checked; settings.Save(); } };
            calibrateButton.Click += (s, e) => CalibrateClicked();
            resetCalButton.Click += (s, e) => ResetCalClicked();
            timer.Tick += (s, e) => Tick();

            autoStartBox.Checked = settings.AutoStart;
            Shown += async (s, e) =>
            {
                RefreshDevices();
                timer.Start();
                bool driverOk = await CheckDriver();
                if (!driverOk && !driverInstallTried)
                {
                    // First start without the driver: start the bundled installer right away; Windows asks for admin rights once.
                    driverInstallTried = true;
                    driverOk = await InstallDriver();
                }
                if (driverOk && settings.AutoStart && bridge != null) bridge.OutputWanted = true;
            };
            FormClosing += (s, e) => { timer.Stop(); settings.Save(); bridge?.Dispose(); };
        }

        // ---------- Layout ----------

        void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            // Radio and driver
            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.Controls.Add(new Label { Text = "Radio:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            top.Controls.Add(deviceBox, 1, 0);
            top.Controls.Add(refreshButton, 2, 0);
            var driverRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            driverRow.Controls.Add(driverLabel);
            driverRow.Controls.Add(driverButton);
            top.Controls.Add(driverRow, 0, 1);
            top.SetColumnSpan(driverRow, 3);
            root.Controls.Add(top, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(SticksPage());
            tabs.TabPages.Add(SwitchesPage());
            tabs.TabPages.Add(ButtonsPage());
            var raw = new TabPage("Raw input") { Padding = new Padding(10) };
            raw.Controls.Add(rawLabel);
            tabs.TabPages.Add(raw);
            root.Controls.Add(tabs, 0, 1);

            // Start, messages, hint
            var bottom = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            var startRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            startRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            startRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            startRow.Controls.Add(startButton, 0, 0);
            startRow.Controls.Add(autoStartBox, 1, 0);
            bottom.Controls.Add(startRow);
            bottom.Controls.Add(promptLabel);
            bottom.Controls.Add(statusLabel);
            bottom.Controls.Add(warningLabel);
            bottom.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(640, 0),
                ForeColor = SystemColors.GrayText,
                Text = "In Bodycam: take out the drone, flip the Acro switch (LB), throttle down, then switch Arm on (RB). " +
                       "Keep Arm on while flying, switch it off when you are back on foot. " +
                       "Tip: the default drone RC Rate in Bodycam is 2.0, which is very twitchy; around 1.0 feels like a normal quad."
            });
            root.Controls.Add(bottom, 0, 2);
        }

        TabPage SticksPage()
        {
            var page = new TabPage("Sticks") { Padding = new Padding(10) };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var intro = new Label { Text = "Mode 2: throttle and yaw on the left stick, pitch and roll on the right.", AutoSize = true, ForeColor = SystemColors.GrayText };
            t.Controls.Add(intro, 0, 0);
            t.SetColumnSpan(intro, 5);
            for (int i = 0; i < Sticks.Length; i++) AddStickRow(t, i + 1, Sticks[i]);

            var cal = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
            cal.Controls.Add(calibrateButton);
            cal.Controls.Add(resetCalButton);
            cal.Controls.Add(calLabel);
            t.Controls.Add(cal, 0, Sticks.Length + 1);
            t.SetColumnSpan(cal, 5);
            page.Controls.Add(t);
            return page;
        }

        void AddStickRow(TableLayoutPanel t, int row, string stick)
        {
            t.Controls.Add(new Label { Text = stick, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            box.SelectedIndexChanged += (s, e) => { if (!loading && profile != null) { StickOf(stick).Axis = box.SelectedItem as string ?? ""; settings.Save(); } };
            axisBoxes[stick] = box;
            t.Controls.Add(box, 1, row);
            var inv = new CheckBox { Text = "invert", AutoSize = true, Anchor = AnchorStyles.Left };
            inv.CheckedChanged += (s, e) => { if (!loading && profile != null) { StickOf(stick).Invert = inv.Checked; settings.Save(); } };
            invertBoxes[stick] = inv;
            t.Controls.Add(inv, 2, row);
            var bar = new Bar { Dock = DockStyle.Fill, Height = 18, Margin = new Padding(6), FromLeft = stick == StickThrottle };
            if (bar.FromLeft) bar.Value = -1;   // 0 % until a radio sends something
            bars[stick] = bar;
            t.Controls.Add(bar, 3, row);
            var learn = new Button { Text = "Learn", AutoSize = true };
            learn.Click += (s, e) => StartLearn("stick:" + stick);
            t.Controls.Add(learn, 4, row);
        }

        TabPage SwitchesPage()
        {
            var page = new TabPage("Arm & Acro") { Padding = new Padding(10) };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            AddSwitchRow(t, 0, "arm", "Arm (RB, press when switched on)", armSourceLabel);
            AddSwitchRow(t, 1, "acro", "Acro mode (LB, press on every flip)", acroSourceLabel);
            t.Controls.Add(lockBox, 0, 2);
            t.SetColumnSpan(lockBox, 4);
            t.Controls.Add(armStateLabel, 0, 3);
            t.SetColumnSpan(armStateLabel, 4);
            page.Controls.Add(t);
            return page;
        }

        void AddSwitchRow(TableLayoutPanel t, int row, string key, string title, Label sourceLabel)
        {
            t.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            t.Controls.Add(sourceLabel, 1, row);
            var learn = new Button { Text = "Learn", AutoSize = true };
            learn.Click += (s, e) => StartLearn(key);
            t.Controls.Add(learn, 2, row);
            var clear = new Button { Text = "Clear", AutoSize = true };
            clear.Click += (s, e) =>
            {
                if (profile == null) return;
                if (key == "arm") profile.ArmSource = ""; else profile.AcroSource = "";
                settings.Save();
                ShowProfile();
            };
            t.Controls.Add(clear, 3, row);
        }

        TabPage ButtonsPage()
        {
            var page = new TabPage("Buttons") { Padding = new Padding(10), AutoScroll = true };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var intro = new Label
            {
                Text = "Put any Xbox button on a switch or button of your radio. Bodycam already uses RB for arming and LB for Acro mode (tab Arm & Acro).",
                AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 6), UseMnemonic = false
            };
            t.Controls.Add(intro, 0, 0);
            t.SetColumnSpan(intro, 5);
            int row = 1;
            foreach (var target in PadButtons.All)
            {
                string tg = target;
                var name = new Label { Text = tg, AutoSize = true, Anchor = AnchorStyles.Left, Width = 80 };
                buttonNameLabels[tg] = name;
                t.Controls.Add(name, 0, row);
                var src = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
                buttonSourceLabels[tg] = src;
                t.Controls.Add(src, 1, row);
                var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                mode.Items.AddRange(ModeNames);
                mode.SelectedIndexChanged += (s, e) =>
                {
                    if (loading || profile == null || mode.SelectedIndex < 0) return;
                    ButtonOf(tg).Mode = ModeKeys[mode.SelectedIndex];
                    settings.Save();
                };
                buttonModeBoxes[tg] = mode;
                t.Controls.Add(mode, 2, row);
                var learn = new Button { Text = "Learn", AutoSize = true };
                learn.Click += (s, e) => StartLearn("btn:" + tg);
                t.Controls.Add(learn, 3, row);
                var clear = new Button { Text = "Clear", AutoSize = true };
                clear.Click += (s, e) => { if (profile != null) { ButtonOf(tg).Source = ""; settings.Save(); ShowProfile(); } };
                t.Controls.Add(clear, 4, row);
                row++;
            }
            page.Controls.Add(t);
            return page;
        }

        StickMap StickOf(string stick)
        {
            switch (stick)
            {
                case StickThrottle: return profile.Throttle;
                case StickYaw: return profile.Yaw;
                case StickPitch: return profile.Pitch;
                default: return profile.Roll;
            }
        }

        ButtonMap ButtonOf(string target) => profile.Buttons.First(b => b.Target == target);

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
                statusLabel.Text = "No radio found. Connect it by USB and choose USB joystick (HID) mode on the radio; it shows up here by itself.";
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
            if (bridge != null && device != null && bridge.Profile == settings.ProfileFor(device)) return;   // same radio after Refresh
            bool wasRunning = bridge?.OutputWanted ?? false;
            bridge?.Dispose();
            bridge = null;
            profile = null;
            calStep = 0;
            learning = null;
            if (device == null) return;
            profile = settings.ProfileFor(device);
            settings.LastDeviceKey = device.Key;
            settings.Save();
            bridge = new Bridge(device, profile) { OutputWanted = wasRunning || (settings.AutoStart && driverReady) };
            ShowProfile();
        }

        /// <summary>Version for the window title (from the build tag, e.g. "v1.1.0"), so screenshots show which version a user runs.</summary>
        static string DisplayVersion()
        {
            var info = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                typeof(MainForm).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            string v = info?.InformationalVersion ?? "";
            return v.Length == 0 || v.StartsWith("0.0.0") ? "(dev build)" : (v.StartsWith("v") ? v : "v" + v);
        }

        async Task<bool> CheckDriver()
        {
            bool ok = await Task.Run(() => VirtualPad.DriverInstalled());
            driverLabel.Text = ok ? "ViGEmBus driver: installed" : "ViGEmBus driver missing (needed for the virtual Xbox controller).";
            driverLabel.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
            driverButton.Visible = !ok;
            driverReady = ok;
            return ok;
        }

        async Task<bool> InstallDriver()
        {
            string installer = DriverSetup.FindInstaller();
            if (installer == null)
            {
                // No download here on purpose: the installer ships in the zip, or the user fetches it from the official page.
                string text = DriverSetup.StartedFromZip()
                    ? "It looks like Bodycam FPV Fix was started directly from inside the zip file.\n\n"
                      + "Please close it, right-click the zip, choose \"Extract All...\", and start BodycamFpvFix.exe from the extracted folder. "
                      + "The driver installer in the folder \"driver\" is then found automatically."
                    : "The ViGEmBus driver is needed for the virtual Xbox controller, and its installer was not found next to the program "
                      + "(folder \"driver\").\n\nIt is included in the zip download of Bodycam FPV Fix. You can also install "
                      + DriverSetup.InstallerName + " yourself from the official ViGEmBus release page and then restart this program.";
                text += "\n\nOpen the official ViGEmBus release page now?";
                if (MessageBox.Show(this, text, "Bodycam FPV Fix", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    DriverSetup.OpenReleasePage();
                return await CheckDriver();
            }
            driverButton.Enabled = false;
            driverLabel.Text = "Starting the ViGEmBus driver installer, Windows will ask for admin rights ...";
            driverLabel.ForeColor = SystemColors.ControlText;
            string error = await Task.Run(() => DriverSetup.Install(installer));
            driverButton.Enabled = true;
            bool ok = await CheckDriver();
            if (error != null) MessageBox.Show(this, error, "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (!ok)
                MessageBox.Show(this, "The driver is still not reachable. If the installer asked for it, restart Windows and open this program again.",
                                "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return ok;
        }

        // ---------- Profile display ----------

        void ShowProfile()
        {
            if (profile == null) return;
            loading = true;
            var names = bridge?.Axes.Select(a => a.Name).ToList() ?? new List<string>();
            foreach (var stick in Sticks)
            {
                var box = axisBoxes[stick];
                var m = StickOf(stick);
                box.BeginUpdate();
                box.Items.Clear();
                foreach (var n in names) box.Items.Add(n);
                if (!string.IsNullOrEmpty(m.Axis) && !names.Contains(m.Axis)) box.Items.Add(m.Axis);
                box.SelectedItem = m.Axis;
                box.EndUpdate();
                invertBoxes[stick].Checked = m.Invert;
            }
            armSourceLabel.Text = Source.Describe(profile.ArmSource);
            acroSourceLabel.Text = Source.Describe(profile.AcroSource);
            lockBox.Checked = profile.LockThrottleWhenDisarmed;
            foreach (var target in PadButtons.All)
            {
                var b = ButtonOf(target);
                buttonSourceLabels[target].Text = Source.Describe(b.Source);
                buttonModeBoxes[target].SelectedIndex = Math.Max(0, Array.IndexOf(ModeKeys, b.Mode));
            }
            calLabel.Text = profile.Calibration.Count == 0 ? "Not calibrated (uses the radio's own range)"
                                                           : "Calibrated: " + string.Join(", ", profile.Calibration.Keys);
            loading = false;
        }

        // ---------- Live update ----------

        void Tick()
        {
            if (bridge == null)
            {
                startButton.Text = "Start";
                startButton.Enabled = false;
                rawLabel.Text = "";
                // No radio yet: look again every two seconds, so plugging it in is enough.
                if ((DateTime.Now - lastScan).TotalSeconds > 2) { lastScan = DateTime.Now; RefreshDevices(); }
                return;
            }
            if (bridge.Axes.Count != shownAxisCount) { shownAxisCount = bridge.Axes.Count; ShowProfile(); }

            startButton.Enabled = !driverButton.Visible && bridge.DeviceConnected;
            startButton.Text = bridge.OutputWanted ? "Stop" : "Start";
            startButton.BackColor = bridge.OutputActive ? Color.FromArgb(200, 235, 205) : SystemColors.Control;
            statusLabel.Text = bridge.Status;

            var o = bridge.Output;
            warningLabel.Text = string.Join(Environment.NewLine, new[] { ThrottleWarning(o), bridge.Warning }.Where(w => w.Length > 0));
            bars[StickThrottle].Value = o.ThrottleLocked ? 0 : o.Throttle;
            bars[StickThrottle].Dimmed = o.ThrottleLocked;
            bars[StickYaw].Value = o.Yaw;
            bars[StickPitch].Value = o.Pitch;
            bars[StickRoll].Value = o.Roll;
            armStateLabel.Text = string.IsNullOrWhiteSpace(profile?.ArmSource) ? ""
                : o.Armed ? "Arm switch: ON, throttle free"
                          : "Arm switch: OFF" + (o.ThrottleLocked ? ", throttle locked at center" : "");
            foreach (var kv in buttonNameLabels)
            {
                var want = o.Pressed.Contains(kv.Key) ? boldFont : Font;
                if (kv.Value.Font != want) kv.Value.Font = want;
            }

            var s = bridge.Last;
            if (s != null && (DateTime.Now - lastRawUpdate).TotalMilliseconds > 120)
            {
                lastRawUpdate = DateTime.Now;
                string axes = string.Join(Environment.NewLine, bridge.Axes.Select(a =>
                    $"{a.Name,-8} {(s.Axes.TryGetValue(a.Name, out int v) ? v : 0),6}   range {a.Min}..{a.Max}"));
                string buttons = s.Buttons.Count == 0 ? "-" : string.Join(", ", s.Buttons.OrderBy(b => b));
                string pressed = o.Pressed.Count == 0 ? "-" : string.Join(", ", o.Pressed);
                rawLabel.Text = axes + Environment.NewLine + Environment.NewLine +
                                "Radio buttons pressed: " + buttons + Environment.NewLine +
                                "Xbox buttons sent:     " + pressed;
            }

            if (calStep == 2 && s != null)
                foreach (var kv in s.Axes)
                {
                    if (!calMin.TryGetValue(kv.Key, out int mn) || kv.Value < mn) calMin[kv.Key] = kv.Value;
                    if (!calMax.TryGetValue(kv.Key, out int mx) || kv.Value > mx) calMax[kv.Key] = kv.Value;
                }
            if (learning != null) LearnTick(s);
        }

        /// <summary>
        /// While the throttle reaches the game, Bodycam also reads it on foot: left stick down means walking backwards.
        /// Says so in red, because the character keeps walking as long as the arm switch stays on.
        /// </summary>
        string ThrottleWarning(OutputSnapshot o)
        {
            if (!bridge.OutputActive || !bridge.DeviceConnected || o.ThrottleLocked) return "";
            if (string.IsNullOrWhiteSpace(profile?.ArmSource))
                return "No Arm switch set: the throttle always goes to the game, so your character walks on foot. Set one in the tab Arm & Acro.";
            if (!o.Armed)
                return "Throttle lock is off: the throttle always goes to the game, so your character walks on foot. Turn the lock on in the tab Arm & Acro.";
            return "ARM IS ON: the throttle goes to the game. Switch Arm OFF before you are back on foot, otherwise your character walks backwards.";
        }

        // ---------- Calibration ----------

        void CalibrateClicked()
        {
            var s = bridge?.Last;
            if (s == null || profile == null) { promptLabel.Text = "Connect the radio first."; return; }
            learning = null;
            if (calStep == 0)
            {
                calStep = 1;
                calibrateButton.Text = "Next";
                resetCalButton.Text = "Cancel";
                promptLabel.Text = "Calibration 1/2: let go of both sticks (throttle all the way down), then click Next.";
            }
            else if (calStep == 1)
            {
                calCenter = new Dictionary<string, int>(s.Axes);
                calMin = new Dictionary<string, int>(s.Axes);
                calMax = new Dictionary<string, int>(s.Axes);
                calStep = 2;
                calibrateButton.Text = "Finish";
                promptLabel.Text = "Calibration 2/2: move both sticks to every corner and around the edge a few times, then click Finish.";
            }
            else
            {
                var result = new Dictionary<string, AxisCal>();
                foreach (var a in bridge.Axes)
                {
                    if (!calMin.TryGetValue(a.Name, out int mn) || !calMax.TryGetValue(a.Name, out int mx) || !calCenter.TryGetValue(a.Name, out int c)) continue;
                    if (mx - mn < (a.Max - a.Min) / 4) continue;   // not moved: probably a switch or an unused axis
                    result[a.Name] = new AxisCal { Min = mn, Center = Math.Max(mn, Math.Min(mx, c)), Max = mx };
                }
                EndCalibration();
                if (result.Count == 0) { promptLabel.Text = "No stick movement seen. Calibration not changed."; return; }
                profile.Calibration = result;   // replaced as a whole, see DeviceProfile.EnsureButtons
                settings.Save();
                ShowProfile();
                promptLabel.Text = "Calibrated: " + string.Join(", ", result.Keys) + ". Press Stop and Start if the controller is running.";
            }
        }

        void ResetCalClicked()
        {
            if (calStep != 0) { EndCalibration(); promptLabel.Text = "Calibration cancelled."; return; }
            if (profile == null) return;
            profile.Calibration = new Dictionary<string, AxisCal>();
            settings.Save();
            ShowProfile();
            promptLabel.Text = "Calibration removed. The program uses the radio's own range again.";
        }

        void EndCalibration()
        {
            calStep = 0;
            calibrateButton.Text = "Calibrate";
            resetCalButton.Text = "Reset calibration";
        }

        // ---------- Learn ----------

        void StartLearn(string what)
        {
            if (bridge?.Last == null || profile == null) { promptLabel.Text = "Connect the radio first."; return; }
            if (calStep != 0) EndCalibration();
            learning = what;
            learnStarted = DateTime.Now;
            learnBase = Copy(bridge.Last);
            learnCandidate = null;
            switch (what)
            {
                case "stick:" + StickThrottle: promptLabel.Text = "Push the THROTTLE stick fully UP and hold it."; break;
                case "stick:" + StickYaw: promptLabel.Text = "Move the LEFT stick fully RIGHT and hold it."; break;
                case "stick:" + StickPitch: promptLabel.Text = "Push the RIGHT stick fully UP (forward) and hold it."; break;
                case "stick:" + StickRoll: promptLabel.Text = "Move the RIGHT stick fully RIGHT and hold it."; break;
                case "arm": promptLabel.Text = "Flip your ARM switch to the ON position."; break;
                case "acro": promptLabel.Text = "Flip the switch you want for Acro mode."; break;
                default: promptLabel.Text = $"Flip the switch or press the button you want for Xbox {what.Substring(4)}."; break;
            }
        }

        void LearnTick(InputState s)
        {
            if ((DateTime.Now - learnStarted).TotalSeconds > 12)
            {
                learning = null;
                promptLabel.Text = "Nothing detected. Press Learn and try again.";
                return;
            }
            if (s == null) return;

            bool stick = learning.StartsWith("stick:");
            string found = stick ? DetectStick(s) : DetectSwitch(s);
            if (found == null) { learnCandidate = null; return; }
            if (found != learnCandidate) { learnCandidate = found; learnCandidateSince = DateTime.Now; return; }
            if ((DateTime.Now - learnCandidateSince).TotalMilliseconds < (stick ? 350 : 150)) return;

            string what = learning;
            learning = null;
            if (what == "arm") profile.ArmSource = found;
            else if (what == "acro") profile.AcroSource = found;
            else if (what.StartsWith("btn:")) ButtonOf(what.Substring(4)).Source = found;
            else
            {
                // found = "<axis>|+" or "<axis>|-": "+" means the raw value rises towards up/right.
                var parts = found.Split('|');
                var m = StickOf(what.Substring(6));
                m.Axis = parts[0];
                m.Invert = parts[1] == "-";
            }
            settings.Save();
            ShowProfile();
            promptLabel.Text = "Learned: " + found.Replace("|+", "").Replace("|-", " (inverted)");
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
