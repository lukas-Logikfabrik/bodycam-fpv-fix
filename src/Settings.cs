using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace BodycamFpvFix
{
    /// <summary>Which device axis drives one stick function. Invert is set so that stick up/right gives +1.</summary>
    [DataContract]
    public sealed class StickMap
    {
        [DataMember] public string Axis = "";
        [DataMember] public bool Invert;

        public StickMap() { }
        public StickMap(string axis, bool invert) { Axis = axis; Invert = invert; }
    }

    /// <summary>
    /// Mapping of one radio. Switch sources are written as "Button 5", "!Button 5", "Slider>1023" or "Ry<1023".
    /// </summary>
    [DataContract]
    public sealed class DeviceProfile
    {
        [DataMember] public StickMap Throttle = new StickMap("Z", false);
        [DataMember] public StickMap Yaw = new StickMap("Rx", false);
        [DataMember] public StickMap Pitch = new StickMap("Y", true);
        [DataMember] public StickMap Roll = new StickMap("X", false);
        [DataMember] public string ArmSource = "";
        [DataMember] public string AcroSource = "";
        [DataMember] public bool LockThrottleWhenDisarmed = true;
        [DataMember] public double Deadzone = 0.01;

        // The serializer skips constructors and field initializers; keep defaults for fields missing in older files.
        [OnDeserializing]
        void BeforeLoad(StreamingContext c)
        {
            Throttle = new StickMap("Z", false); Yaw = new StickMap("Rx", false);
            Pitch = new StickMap("Y", true); Roll = new StickMap("X", false);
            ArmSource = ""; AcroSource = ""; LockThrottleWhenDisarmed = true; Deadzone = 0.01;
        }

        /// <summary>Defaults for a radio. Most radios send AETR on X, Y, Z, Rx.</summary>
        public static DeviceProfile DefaultFor(HidDeviceInfo device)
        {
            var p = new DeviceProfile();
            // BETAFPV LiteRadio (STM32 joystick 0483:572B): measured and flown in Bodycam on 2026-10-08.
            if (device != null && device.Key == "0483:572B")
            {
                p.ArmSource = "Button 5";       // SA
                p.AcroSource = "Slider<1023";   // rightmost switch
            }
            return p;
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string LastDeviceKey = "";
        [DataMember] public bool AutoStart;
        [DataMember] public Dictionary<string, DeviceProfile> Profiles = new Dictionary<string, DeviceProfile>();

        [OnDeserializing]
        void BeforeLoad(StreamingContext c) { LastDeviceKey = ""; Profiles = new Dictionary<string, DeviceProfile>(); }

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BodycamFpvFix");
        static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    using (var s = File.OpenRead(FilePath))
                    {
                        var loaded = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(s);
                        if (loaded.Profiles == null) loaded.Profiles = new Dictionary<string, DeviceProfile>();
                        return loaded;
                    }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                using (var s = File.Create(tmp))
                    new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(s, this);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }

        public DeviceProfile ProfileFor(HidDeviceInfo device)
        {
            if (!Profiles.TryGetValue(device.Key, out var p) || p == null)
            {
                p = DeviceProfile.DefaultFor(device);
                Profiles[device.Key] = p;
            }
            return p;
        }
    }

    /// <summary>Evaluates switch sources such as "Button 5", "!Button 5" or "Slider>1023".</summary>
    public static class Source
    {
        public static bool IsActive(string source, InputState s)
        {
            if (string.IsNullOrWhiteSpace(source) || s == null) return false;
            source = source.Trim();
            bool negate = source.StartsWith("!");
            if (negate) source = source.Substring(1).Trim();
            bool result;
            if (source.StartsWith("Button ", StringComparison.OrdinalIgnoreCase))
            {
                result = int.TryParse(source.Substring(7), out int b) && s.Buttons.Contains(b);
            }
            else
            {
                int gt = source.IndexOf('>'), lt = source.IndexOf('<');
                int pos = gt > 0 ? gt : lt;
                if (pos <= 0) return false;
                string axis = source.Substring(0, pos).Trim();
                if (!int.TryParse(source.Substring(pos + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit)) return false;
                if (!s.Axes.TryGetValue(axis, out int v)) return false;
                result = gt > 0 ? v > limit : v < limit;
            }
            return negate ? !result : result;
        }

        public static string Describe(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "not set";
            return source;
        }
    }
}
