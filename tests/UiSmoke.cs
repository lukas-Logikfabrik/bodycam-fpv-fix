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
                CheckTextFits(form);
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
                CheckTextFits(form);
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
            // A hand-edited settings file with nulls must load into a usable profile instead of freezing the UI.
            string broken = "{\"LastDeviceKey\":null,\"Profiles\":[{\"Key\":\"0001:0002\",\"Value\":{\"Throttle\":null,\"ArmSource\":null," +
                            "\"Calibration\":null,\"Buttons\":[null,{\"Target\":\"A\",\"Source\":null,\"Mode\":\"x\"}],\"Deadzone\":1}}]}";
            AppSettings loaded;
            using (var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(broken)))
                loaded = (AppSettings)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(AppSettings)).ReadObject(json);
            var repaired = loaded.ProfileFor(new HidDeviceInfo { VendorId = 1, ProductId = 2 });
            if (repaired.Throttle == null || repaired.Calibration == null || repaired.ArmSource == null || repaired.Deadzone > 0.5 ||
                repaired.Buttons.Count != PadButtons.All.Length || repaired.Buttons.Any(b => b.Source == null || b.Mode != ButtonMap.Hold))
                throw new Exception("Damaged settings were not repaired");
            Console.WriteLine("PASS: five pages render, two live previews, no-device start disabled, minimum-size layout, damaged settings repaired.");
        }
        // Font sizes in pixels follow the layout scale only, so a display scale above 100 % cannot make text outgrow its box.
        // Headings and navigation are single lines in fixed boxes and must fit at every window size.
        static void CheckTextFits(MainForm form)
        {
            var sizes = (Dictionary<Control, float>)Field(form, "textSizes").GetValue(form);
            int headings = 0;
            foreach (var c in sizes.Keys)
            {
                if (c is Panel || !c.Visible) continue;   // containers show no text; hidden pages are sized when opened
                if (c.Font.Unit != GraphicsUnit.Pixel) throw new Exception("Font not sized in pixels: " + c.GetType().Name + " \"" + c.Text + "\"");
                if (!(c is Label || c is Button) || c.AutoSize || c.Text.Length == 0 || c.Font.FontFamily.Name != "Bebas Neue") continue;
                headings++;
                int needed = TextRenderer.MeasureText(c.Text, c.Font).Width + c.Padding.Horizontal;
                if (needed > c.Width) throw new Exception($"Text cut off at {form.ClientSize.Width}x{form.ClientSize.Height}: \"{c.Text}\" needs {needed} px, box is {c.Width} px");
            }
            if (headings < 10) throw new Exception("Only " + headings + " headings checked; is the Bebas Neue font missing?");
        }
        static FieldInfo Field(MainForm form, string name) => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static void Tick(MainForm form) => typeof(MainForm).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
    }
}
