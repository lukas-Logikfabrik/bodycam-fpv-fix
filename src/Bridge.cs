using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BodycamFpvFix
{
    /// <summary>What the virtual controller currently sends, for the live view.</summary>
    public sealed class OutputSnapshot
    {
        public double Throttle, Yaw, Pitch, Roll;   // -1..1, stick up/right positive
        public bool Armed, ThrottleLocked;
        public HashSet<string> Pressed = new HashSet<string>();
    }

    /// <summary>
    /// Reads the selected radio on a background thread and, while output is on, feeds a virtual Xbox controller.
    /// Bodycam drone layout (Acro mode): left stick = throttle (full travel) and yaw, right stick = pitch and roll,
    /// RB = arm, LB = toggle Acro mode. Pitch forward is negative on the right stick Y axis.
    /// </summary>
    public sealed class Bridge : IDisposable
    {
        const int PulseMs = 150;
        const int SettleMs = 2000;      // a fresh virtual pad drops reports for a moment after plugging in

        readonly HidDeviceInfo device;
        readonly Thread thread;
        volatile bool stop;
        volatile bool outputWanted;

        public DeviceProfile Profile;   // read every report; the UI edits it in place
        public volatile InputState Last;
        public volatile List<AxisInfo> Axes = new List<AxisInfo>();
        public volatile OutputSnapshot Output = new OutputSnapshot();
        public volatile string Status = "";
        public volatile bool DeviceConnected;
        public volatile bool OutputActive;
        public volatile string Warning = "";

        public Bridge(HidDeviceInfo device, DeviceProfile profile)
        {
            this.device = device;
            Profile = profile;
            thread = new Thread(Run) { IsBackground = true, Name = "Radio reader" };
            thread.Start();
        }

        public bool OutputWanted
        {
            get => outputWanted;
            set => outputWanted = value;
        }

        public void Dispose()
        {
            stop = true;
            thread.Join(2000);
        }

        void Run()
        {
            HidJoystick joy = null;
            VirtualPad pad = null;
            var pulses = new Pulses();
            DateTime padSince = DateTime.MinValue;
            bool nudged = false;
            var centerSamples = new Dictionary<string, List<int>>();
            var measured = new Dictionary<string, int>();

            try
            {
                while (!stop)
                {
                    if (joy == null)
                    {
                        DeviceConnected = false;
                        joy = OpenDevice();
                        if (joy == null)
                        {
                            Status = "Radio not found. Plug it in and switch it to USB joystick mode.";
                            SendNeutral(pad);
                            Thread.Sleep(1000);
                            continue;
                        }
                        Axes = joy.Axes;
                        DeviceConnected = true;
                    }

                    InputState s;
                    try { s = joy.Read(250); }
                    catch (IOException)
                    {
                        joy.Dispose();
                        joy = null;
                        Last = null;
                        SendNeutral(pad);
                        continue;
                    }
                    if (s == null) continue;
                    Last = s;

                    // Virtual pad on/off
                    if (outputWanted && pad == null)
                    {
                        try
                        {
                            pad = new VirtualPad();
                            padSince = DateTime.Now;
                            nudged = false;
                            centerSamples.Clear();
                            measured.Clear();
                            pulses = new Pulses();
                            Warning = "";
                        }
                        catch (Exception ex)
                        {
                            outputWanted = false;
                            Status = "Virtual controller failed: " + ex.Message;
                            continue;
                        }
                    }
                    else if (!outputWanted && pad != null)
                    {
                        pad.Dispose();
                        pad = null;
                    }
                    OutputActive = pad != null;

                    var p = Profile;
                    if (pad == null)
                    {
                        Output = Compute(s, p, measured, null, DateTime.Now);
                        Status = "Radio connected. Press Start to create the virtual Xbox controller.";
                        continue;
                    }

                    // Settle phase: measure stick centers of uncalibrated axes (sticks released), then nudge the pad once.
                    double sinceMs = (DateTime.Now - padSince).TotalMilliseconds;
                    if (sinceMs < SettleMs)
                    {
                        if (sinceMs > SettleMs - 500)
                            foreach (var m in new[] { p.Yaw, p.Pitch, p.Roll })
                                if (!string.IsNullOrEmpty(m.Axis) && s.Axes.TryGetValue(m.Axis, out int v))
                                {
                                    if (!centerSamples.TryGetValue(m.Axis, out var l)) centerSamples[m.Axis] = l = new List<int>();
                                    l.Add(v);
                                }
                        Status = "Starting virtual controller, keep the sticks centered ...";
                        continue;
                    }
                    if (!nudged)
                    {
                        measured = MeasureCenters(centerSamples, joy.Axes, p);
                        // ViGEm only forwards changed reports; without this the game sees a random start state until the first stick move.
                        pad.Send(1, 0, 0, 0, null);
                        Thread.Sleep(20);
                        nudged = true;
                    }

                    var o = Compute(s, p, measured, pulses, DateTime.Now);
                    Output = o;
                    pad.Send(ToShort(o.Yaw), ToShort(o.ThrottleLocked ? 0 : o.Throttle), ToShort(o.Roll), ToShort(-o.Pitch), o.Pressed);
                    Status = "Running: Bodycam sees an Xbox controller.";
                }
            }
            catch (Exception ex)
            {
                Status = "Error: " + ex.Message;
            }
            finally
            {
                pad?.Dispose();
                joy?.Dispose();
                OutputActive = false;
                DeviceConnected = false;
            }
        }

        HidJoystick OpenDevice()
        {
            var candidates = HidJoystick.FindJoysticks();
            var match = candidates.FirstOrDefault(d => d.Path == device.Path) ?? candidates.FirstOrDefault(d => d.Key == device.Key);
            return match == null ? null : HidJoystick.Open(match);
        }

        Dictionary<string, int> MeasureCenters(Dictionary<string, List<int>> samples, List<AxisInfo> axes, DeviceProfile p)
        {
            var centers = new Dictionary<string, int>();
            var warnings = new List<string>();
            foreach (var a in axes)
            {
                if (p.Calibration.ContainsKey(a.Name)) continue;   // calibrated axes use their stored center
                if (!samples.TryGetValue(a.Name, out var l) || l.Count == 0) continue;
                l.Sort();
                int median = l[l.Count / 2];
                if (Math.Abs(median - a.Mid) > (a.Max - a.Min) / 10)
                {
                    warnings.Add($"{a.Name} = {median}");
                    centers[a.Name] = a.Mid;   // far off: do not trust this center
                }
                else centers[a.Name] = median;
            }
            if (warnings.Count > 0)
                Warning = "Stick not centered or radio not calibrated (" + string.Join(", ", warnings) +
                          "). Use Calibrate, or calibrate the radio itself, then press Stop and Start.";
            return centers;
        }

        OutputSnapshot Compute(InputState s, DeviceProfile p, Dictionary<string, int> measured, Pulses pulses, DateTime now)
        {
            var o = new OutputSnapshot
            {
                Throttle = ThrottleValue(s, p.Throttle, p),
                Yaw = StickValue(s, p.Yaw, p, measured),
                Pitch = StickValue(s, p.Pitch, p, measured),
                Roll = StickValue(s, p.Roll, p, measured),
            };
            bool armSet = !string.IsNullOrWhiteSpace(p.ArmSource);
            o.Armed = !armSet || Source.IsActive(p.ArmSource, s);
            o.ThrottleLocked = armSet && p.LockThrottleWhenDisarmed && !o.Armed;
            if (pulses == null) return o;

            // Arm: one RB press when the arm switch goes on. Switching off does nothing, so switch and game stay in step after a crash.
            if (armSet && pulses.Pressed("arm", Source.IsActive(p.ArmSource, s), ButtonMap.TapWhenOn, now)) o.Pressed.Add("RB");
            // Acro: Bodycam toggles Acro mode with LB, so every flip of the switch is one press.
            if (!string.IsNullOrWhiteSpace(p.AcroSource) && pulses.Pressed("acro", Source.IsActive(p.AcroSource, s), ButtonMap.TapOnFlip, now)) o.Pressed.Add("LB");

            foreach (var b in p.Buttons)
                if (!string.IsNullOrWhiteSpace(b.Source) && !string.IsNullOrEmpty(b.Target) &&
                    pulses.Pressed("btn:" + b.Target, Source.IsActive(b.Source, s), b.Mode, now))
                    o.Pressed.Add(b.Target);
            return o;
        }

        /// <summary>Min, center and max of an axis: from the calibration if there is one, else the HID range and the measured center.</summary>
        void RangeOf(string axis, DeviceProfile p, Dictionary<string, int> measured, out int min, out int center, out int max)
        {
            if (p.Calibration.TryGetValue(axis, out var cal) && cal.Max > cal.Min)
            {
                min = cal.Min; center = cal.Center; max = cal.Max;
                return;
            }
            var a = Axes.FirstOrDefault(x => x.Name == axis);
            min = a?.Min ?? 0; max = a?.Max ?? 1;
            center = measured != null && measured.TryGetValue(axis, out int c) ? c : (a?.Mid ?? 0);
        }

        double ThrottleValue(InputState s, StickMap m, DeviceProfile p)
        {
            if (string.IsNullOrEmpty(m.Axis) || !s.Axes.TryGetValue(m.Axis, out int v)) return -1;
            RangeOf(m.Axis, p, null, out int min, out _, out int max);
            double t = (double)(v - min) / Math.Max(1, max - min);
            if (m.Invert) t = 1 - t;
            return Clamp(t * 2 - 1);
        }

        double StickValue(InputState s, StickMap m, DeviceProfile p, Dictionary<string, int> measured)
        {
            if (string.IsNullOrEmpty(m.Axis) || !s.Axes.TryGetValue(m.Axis, out int v)) return 0;
            RangeOf(m.Axis, p, measured, out int min, out int c, out int max);
            double n = v >= c ? (double)(v - c) / Math.Max(1, max - c) : (double)(v - c) / Math.Max(1, c - min);
            double dz = p.Deadzone;
            if (Math.Abs(n) < dz) n = 0;
            else n = Math.Sign(n) * (Math.Abs(n) - dz) / (1 - dz);
            return Clamp(m.Invert ? -n : n);
        }

        static double Clamp(double v) => Math.Max(-1, Math.Min(1, v));
        static short ToShort(double v) => (short)Math.Round(Clamp(v) * 32767);

        static void SendNeutral(VirtualPad pad)
        {
            try { pad?.Send(0, 0, 0, 0, null); } catch { }
        }

        /// <summary>Turns switch states into button presses: hold, tap on every flip, or tap when switched on.</summary>
        sealed class Pulses
        {
            readonly Dictionary<string, bool> last = new Dictionary<string, bool>();
            readonly Dictionary<string, DateTime> until = new Dictionary<string, DateTime>();

            public bool Pressed(string key, bool active, string mode, DateTime now)
            {
                if (mode == ButtonMap.Hold) return active;
                if (!last.TryGetValue(key, out bool before)) { last[key] = active; return false; }   // no press for the starting position
                if (active != before)
                {
                    last[key] = active;
                    if (mode == ButtonMap.TapOnFlip || active) until[key] = now.AddMilliseconds(PulseMs);
                }
                return until.TryGetValue(key, out var u) && now < u;
            }
        }
    }
}
