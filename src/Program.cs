using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BodycamFpvFix
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // The ViGEm client library is embedded, so the program is a single exe.
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                if (!e.Name.StartsWith("Nefarius.ViGEm.Client,", StringComparison.OrdinalIgnoreCase)) return null;
                using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nefarius.ViGEm.Client.dll"))
                {
                    if (res == null) return null;
                    var bytes = new byte[res.Length];
                    res.Read(bytes, 0, bytes.Length);
                    return Assembly.Load(bytes);
                }
            };

            using (var single = new Mutex(true, "BodycamFpvFix-single-instance", out bool first))
            {
                if (!first)
                {
                    MessageBox.Show("Bodycam FPV Fix is already running.", "Bodycam FPV Fix");
                    return;
                }
                Application.ThreadException += (s, e) => ShowError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        static void ShowError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.AppendAllText(Path.Combine(AppSettings.Folder, "error.log"), DateTime.Now + "  " + ex + Environment.NewLine);
            }
            catch { }
            MessageBox.Show(ex?.Message ?? "Unknown error", "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
