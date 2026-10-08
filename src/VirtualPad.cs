using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BodycamFpvFix
{
    /// <summary>A virtual Xbox 360 controller through the ViGEmBus driver. Games see it like a real XInput pad.</summary>
    sealed class VirtualPad : IDisposable
    {
        readonly ViGEmClient client;
        readonly IXbox360Controller pad;

        public VirtualPad()
        {
            client = new ViGEmClient();
            pad = client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            pad.Connect();
        }

        public void Send(short leftX, short leftY, short rightX, short rightY, bool leftShoulder, bool rightShoulder)
        {
            pad.SetAxisValue(Xbox360Axis.LeftThumbX, leftX);
            pad.SetAxisValue(Xbox360Axis.LeftThumbY, leftY);
            pad.SetAxisValue(Xbox360Axis.RightThumbX, rightX);
            pad.SetAxisValue(Xbox360Axis.RightThumbY, rightY);
            pad.SetButtonState(Xbox360Button.LeftShoulder, leftShoulder);
            pad.SetButtonState(Xbox360Button.RightShoulder, rightShoulder);
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
