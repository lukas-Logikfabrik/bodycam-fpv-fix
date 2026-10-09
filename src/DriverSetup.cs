using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace BodycamFpvFix
{
    /// <summary>
    /// Starts the official ViGEmBus installer that ships next to the exe (zip download, folder "driver"),
    /// after checking its SHA-256. The program itself never downloads anything.
    /// </summary>
    static class DriverSetup
    {
        // Official, signed release by Nefarius Software Solutions. The hash pins exactly this file.
        public const string InstallerName = "ViGEmBus_1.22.0_x64_x86_arm64.exe";
        public const string ReleasePage = "https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0";
        const string InstallerSha256 = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A";

        static string ProgramFolder => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>Path of the bundled installer (folder "driver" or right next to the exe), or null.</summary>
        public static string FindInstaller()
        {
            foreach (var path in new[] { Path.Combine(ProgramFolder, "driver", InstallerName), Path.Combine(ProgramFolder, InstallerName) })
                if (File.Exists(path)) return path;
            return null;
        }

        /// <summary>
        /// True when the exe was started straight from a zip: Explorer, WinRAR and 7-Zip then copy only the exe
        /// into a temporary folder (Temp1_*, Rar$EX*, 7zO*), so the driver installer next to it is missing.
        /// </summary>
        public static bool StartedFromZip()
        {
            string folder = Path.GetFullPath(ProgramFolder).TrimEnd('\\') + "\\";
            foreach (var mark in new[] { "\\Temp1_", "\\Rar$EX", "\\7zO", ".zip\\" })
                if (folder.IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Checks and starts the installer. Returns null on success, otherwise an error text for the user.</summary>
        public static string Install(string file)
        {
            string hash;
            try
            {
                using (var sha = SHA256.Create())
                using (var s = File.OpenRead(file))
                    hash = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
            }
            catch (Exception ex)
            {
                return "The driver installer could not be read: " + ex.Message;
            }
            if (!string.Equals(hash, InstallerSha256, StringComparison.OrdinalIgnoreCase))
                return "The file " + InstallerName + " next to the program is not the official ViGEmBus installer (checksum differs), "
                     + "so nothing was started. Download Bodycam FPV Fix again, or install ViGEmBus 1.22.0 from " + ReleasePage + ".";

            try
            {
                using (var p = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "runas" }))
                    p?.WaitForExit();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return "Admin confirmation was cancelled. Click \"Install driver\" to try again.";
            }
            catch (Exception ex)
            {
                return "The driver installer could not be started: " + ex.Message;
            }
            return null;
        }

        /// <summary>Opens the official ViGEmBus release page in the browser (the user downloads, not this program).</summary>
        public static void OpenReleasePage()
        {
            try { Process.Start(new ProcessStartInfo(ReleasePage) { UseShellExecute = true }); }
            catch { }
        }
    }
}
