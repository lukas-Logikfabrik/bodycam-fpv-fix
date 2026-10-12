using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace BodycamFpvFix
{
    static class UiSmoke
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string destination = args[0];
            Directory.CreateDirectory(destination);
            using (var form = new MainForm(true))
            {
                form.Show();
                Application.DoEvents();
                form.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                form.WindowState = FormWindowState.Normal;
                Application.DoEvents();
                foreach (string page in new[] { "CONTROLLER", "CALIBRATION", "SWITCHES", "BUTTONS", "ADVANCED" })
                {
                    var nav = form.Controls.Find("nav-" + page, true).Single() as Button;
                    nav.PerformClick();
                    Application.DoEvents();
                    using (var shot = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(shot, new Rectangle(Point.Empty, form.ClientSize));
                        shot.Save(Path.Combine(destination, page.ToLowerInvariant() + ".png"));
                    }
                }
                var start = form.Controls.Find("bridge-start", true).Single();
                if (start.Enabled) throw new Exception("Start enabled without a radio");
                if (form.Controls.Find("axis-preview", true).Length != 2) throw new Exception("Missing stick previews");
                ((Button)form.Controls.Find("nav-CONTROLLER", true).Single()).PerformClick();
                form.ClientSize = new Size(1000, 680);
                Application.DoEvents();
                using (var shot = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    form.DrawToBitmap(shot, new Rectangle(Point.Empty, form.ClientSize));
                    shot.Save(Path.Combine(destination, "minimum.png"));
                }
                if (form.ClientSize.Height > 728) throw new Exception("Minimum size exceeds a 768p working area");
                var profile = new DeviceProfile { ArmSource = "Button 1", AcroSource = "Button 2" };
                profile.EnsureButtons();
                var fake = (Bridge)FormatterServices.GetUninitializedObject(typeof(Bridge));
                fake.Profile = profile;
                fake.Last = new InputState();
                fake.Axes = new List<AxisInfo>();
                fake.Output = new OutputSnapshot { Armed = true, Throttle = 1, Yaw = 1 };
                fake.Status = "Running: synthetic UI test, no hardware output.";
                fake.Warning = "Stick not centered or radio not calibrated (X = 1234, Y = 2345, Z = 3456). Use Calibrate, or calibrate the radio itself, then press Stop and Start.";
                fake.DeviceConnected = fake.OutputActive = true;
                Field(form, "profile").SetValue(form, profile);
                Field(form, "bridge").SetValue(form, fake);
                Tick(form);
                form.ClientSize = new Size(1280, 850);
                Tick(form);
                Application.DoEvents();
                var warning = (Label)Field(form, "warningLabel").GetValue(form);
                var measured = TextRenderer.MeasureText(warning.Text, warning.Font, new Size(warning.Width, 1000), TextFormatFlags.WordBreak);
                if (measured.Height > warning.Height) throw new Exception("Combined safety warning clipped");
                using (var shot = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(shot, form.ClientRectangle);
                    shot.Save(Path.Combine(destination, "synthetic-connected.png"));
                }
                fake.DeviceConnected = false;
                fake.Last = null;
                Tick(form);
                var bars = (Dictionary<string, Bar>)Field(form, "bars").GetValue(form);
                if (bars["Yaw"].Value != 0) throw new Exception("Disconnected radio shows stale input");
                var fonts = (List<Font>)Field(form, "ownedFonts").GetValue(form);
                form.ClientSize = new Size(1080, 740);
                int before = fonts.Count;
                for (int i = 0; i < 20; i++) form.ClientSize = new Size(1100 + i * 3, 760 + i * 2);
                if (fonts.Count > before + 5) throw new Exception("Resizing retains superseded fonts");
                form.Close();
            }
            Console.WriteLine("PASS: five pages render, two live previews, no-device start disabled, minimum-size layout.");
        }
        static FieldInfo Field(MainForm form, string name) => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static void Tick(MainForm form) => typeof(MainForm).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    }
}
