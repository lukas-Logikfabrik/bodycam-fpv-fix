using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace BodycamFpvFix
{
    /// <summary>Downloads the official ViGEmBus installer, checks it and runs it with one admin prompt.</summary>
    static class DriverSetup
    {
        // Official release by Nefarius Software Solutions (signed). The hash pins exactly this file.
        public const string InstallerUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";
        const string InstallerSha256 = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A";

        /// <summary>Returns null on success, otherwise an error text for the user.</summary>
        public static string Install()
        {
            string file = Path.Combine(Path.GetTempPath(), "ViGEmBus_1.22.0_x64_x86_arm64.exe");
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var web = new WebClient())
                    web.DownloadFile(InstallerUrl, file);
            }
            catch (Exception ex)
            {
                return "Download failed: " + ex.Message;
            }

            string hash;
            using (var sha = SHA256.Create())
            using (var s = File.OpenRead(file))
                hash = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
            if (!string.Equals(hash, InstallerSha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch { }
                return "The downloaded driver installer does not match the expected checksum. Nothing was installed.";
            }

            try
            {
                using (var p = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "runas" }))
                    p?.WaitForExit();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return "Admin confirmation was cancelled.";
            }
            catch (Exception ex)
            {
                return "Installer could not be started: " + ex.Message;
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
            return null;
        }
    }
}
