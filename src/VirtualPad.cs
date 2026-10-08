using System;
using System.Collections.Generic;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BodycamFpvFix
{
    /// <summary>A virtual Xbox 360 controller through the ViGEmBus driver. Games see it like a real XInput pad.</summary>
    sealed class VirtualPad : IDisposable
    {
        static readonly Dictionary<string, Xbox360Button> ButtonByName = new Dictionary<string, Xbox360Button>
        {
            { "A", Xbox360Button.A }, { "B", Xbox360Button.B }, { "X", Xbox360Button.X }, { "Y", Xbox360Button.Y },
            { "LB", Xbox360Button.LeftShoulder }, { "RB", Xbox360Button.RightShoulder },
            { "Back", Xbox360Button.Back }, { "Start", Xbox360Button.Start },
            { "LS", Xbox360Button.LeftThumb }, { "RS", Xbox360Button.RightThumb },
            { "D-pad up", Xbox360Button.Up }, { "D-pad down", Xbox360Button.Down },
            { "D-pad left", Xbox360Button.Left }, { "D-pad right", Xbox360Button.Right },
        };

        readonly ViGEmClient client;
        readonly IXbox360Controller pad;

        public VirtualPad()
        {
            client = new ViGEmClient();
            pad = client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            pad.Connect();
        }

        /// <summary>Sends one full controller state. Pressed holds button names from PadButtons.All; LT and RT count as fully pressed triggers.</summary>
        public void Send(short leftX, short leftY, short rightX, short rightY, ICollection<string> pressed)
        {
            pad.SetAxisValue(Xbox360Axis.LeftThumbX, leftX);
            pad.SetAxisValue(Xbox360Axis.LeftThumbY, leftY);
            pad.SetAxisValue(Xbox360Axis.RightThumbX, rightX);
            pad.SetAxisValue(Xbox360Axis.RightThumbY, rightY);
            foreach (var kv in ButtonByName) pad.SetButtonState(kv.Value, pressed != null && pressed.Contains(kv.Key));
            pad.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(pressed != null && pressed.Contains("LT") ? 255 : 0));
            pad.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(pressed != null && pressed.Contains("RT") ? 255 : 0));
            pad.SubmitReport();
        }

        public void Dispose()
        {
            try { pad.Disconnect(); } catch { }
            try { client.Dispose(); } catch { }
        }

        /// <summary>True when the ViGEmBus driver is installed and reachable.</summary>
        public static bool DriverInstalled()
        {
            try
            {
                using (new ViGEmClient()) return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
