using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BodycamFpvFix
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // The ViGEm client library is merged into this exe at build time (ILRepack, see build.ps1).
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

        static readonly System.Collections.Generic.HashSet<string> shownErrors = new System.Collections.Generic.HashSet<string>();

        static void ShowError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.AppendAllText(Path.Combine(AppSettings.Folder, "error.log"), DateTime.Now + "  " + ex + Environment.NewLine);
            }
            catch { }
            // The UI timer keeps running behind a message box: show each error once, log every repeat.
            string message = ex?.Message ?? "Unknown error";
            if (!shownErrors.Add(message)) return;
            MessageBox.Show(message + Environment.NewLine + Environment.NewLine + "Details: " + Path.Combine(AppSettings.Folder, "error.log"),
                "Bodycam FPV Fix", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
