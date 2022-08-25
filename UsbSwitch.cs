using System;

namespace NvkCommon
{
    public abstract class UsbSwitch
    {
        public abstract String Filter { get; }

        public abstract void SwitchDeviceRemoteToLocal(string devicePath);

        public abstract void SwitchDeviceLocalToRemote(string devicePath);
    }

    /// <summary>
    /// https://iogear.com/products/gub231
    /// Commands were found by using Wireshark USB sniffer to monitor USB commands
    /// of their shitty Windows sharing app (available at the above website).
    /// </summary>
    public class IoGearGub231UsbSwitch : UsbSwitch
    {
        public const string FILTER = "vid_0557&pid_2405";

        public override string Filter => FILTER;

        public override void SwitchDeviceRemoteToLocal(string devicePath)
        {
            byte[] buffer = [0x02, 0x11];
            UsbManager.WriteDeviceData(devicePath, buffer);
        }

        public override void SwitchDeviceLocalToRemote(string devicePath)
        {
            byte[] buffer = [0x01, 0x11];
            UsbManager.WriteDeviceData(devicePath, buffer);
        }
    }

    /// <summary>
    /// Commands were found by using Wireshark USB sniffer to monitor USB commands.
    /// The ActionStar usbshare.exe app was very similar to the IoGear USB sharing app.
    /// </summary>
    class ActionStarUnknownUsbSwitch : UsbSwitch
    {
        public const string FILTER = "vid_2101&pid_0201";

        public override string Filter => FILTER;

        /// <summary>
        /// The ActionStar usbshare.exe app writes [0x00, 0x02, 0x00] to the switch
        /// </summary>
        /// <param name="devicePath"></param>
        public override void SwitchDeviceRemoteToLocal(string devicePath)
        {
            byte[] buffer = new byte[] { 0x00, 0x02, 0x00 };
            UsbManager.WriteDeviceData(devicePath, buffer);
        }

        public override void SwitchDeviceLocalToRemote(string devicePath)
        {
            throw new NotImplementedException();
        }
    }
}
