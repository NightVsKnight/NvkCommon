using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NvkCommon
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public partial class UsbManager : IDisposable
    {
        private static readonly string TAG = Log.TAG(typeof(UsbManager));

        public enum ChangeType
        {
            Add,
            Remove
        }

        public class UsbChangeEventArgs : EventArgs
        {
            public ChangeType ChangeType { get; private set; }
            public string DevicePath { get; private set; }

            internal UsbChangeEventArgs(ChangeType changeType, string devicePath)
            {
                ChangeType = changeType;
                DevicePath = devicePath;
            }
        }

        public event EventHandler<UsbChangeEventArgs> OnUsbChange;

        public ReadOnlyCollection<string> DevicePathsSubscribed => DevicePathsSubscribedInternal.ToList().AsReadOnly();

        private HashSet<string> DevicePathsSubscribedInternal = [];

        private IntPtr notificationHandle;

        #region IDisposable

        private bool isDisposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            UnregisterDeviceNotification();

            isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable

        /*
        public class HidDeviceId
        {
            public int VendorId { get; private set; }
            public int ProductId { get; private set; }
            public Version Version { get; private set; }

            public HidDeviceId(int vendorId, int productId, Version version)
            {
                VendorId = vendorId;
                ProductId = productId;
                Version = version;
            }
        }

        public static Version newVersion(int version)
        {
            var major = (int)((version & 0xFF000000) >> 24);
            var minor = (version & 0x00FF0000) >> 16;
            var build = (version & 0x0000FF00) >> 8;
            var revision = version & 0x000000FF;
            return new Version(major, minor, build, revision);
        }

        public static IEnumerable<HidDevice> Enumerate(HidDeviceId hidDeviceId, bool includeGreaterThan = false)
        {
            return HidDevices.Enumerate(hidDeviceId.VendorId, hidDeviceId.ProductId)
                .Where(x =>
                {
                    var xAttributesVersion = newVersion(x.Attributes.Version);
                    if (includeGreaterThan)
                    {
                        return xAttributesVersion >= hidDeviceId.Version;
                    }
                    else
                    {
                        return xAttributesVersion == hidDeviceId.Version;
                    }
                });
        }
        */

        public void Subscribe(string devicePath)
        {
            if (String.IsNullOrWhiteSpace(devicePath))
            {
                return;
            }
            devicePath = devicePath.ToLower();
            if (DevicePathsSubscribedInternal.Add(devicePath))
            {
                ChangeType changeType = FindDevice(devicePath) != null ? ChangeType.Add : ChangeType.Remove;
                OnUsbChange?.Invoke(this, new UsbChangeEventArgs(changeType, devicePath));
            }
        }

        public void Unsubscribe(string devicePath)
        {
            if (String.IsNullOrWhiteSpace(devicePath))
            {
                return;
            }
            devicePath = devicePath.ToLower();
            DevicePathsSubscribedInternal.Remove(devicePath);
        }

        public bool RegisterDeviceNotification(IntPtr windowHandle)
        {
            if (notificationHandle != IntPtr.Zero)
            {
                return false;
            }
            var dbi = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_devicetype = DeviceType.DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE.Guid,
            };
            dbi.dbcc_size = Marshal.SizeOf(dbi);
            var buffer = Marshal.AllocHGlobal(dbi.dbcc_size);
            Marshal.StructureToPtr(dbi, buffer, true);
            notificationHandle = RegisterDeviceNotification(windowHandle, buffer, 0);
            Marshal.FreeHGlobal(buffer);
            return true;
        }

        public void UnregisterDeviceNotification()
        {
            if (notificationHandle == IntPtr.Zero)
            {
                return;
            }
            UnregisterDeviceNotification(notificationHandle);
        }

        public void WndProc(ref Message m)
        {
            const bool log = false;

            //Log.PrintLine(TAG, LogLevel.Information, $"WndProc m={m}");
            switch ((DeviceManagementMessages)m.Msg)
            {
                case DeviceManagementMessages.WM_DEVICECHANGE:
                {
                    var deviceChangeEvent = (DeviceChangeEvent)m.WParam.ToInt32();
                    //Log.PrintLine(TAG, LogLevel.Information, $"WndProc WM_DEVICECHANGE wParam=0x{wParam:X8}");

                    /*
                    var serialPorts = SerialPort.GetPortNames().OrderBy(name => name);
                    foreach (var serialPort in serialPorts)
                    {
                        Log.PrintLine(TAG, LogLevel.Information, $"WndProc WM_DEVICECHANGE serialPort={serialPort}");
                    }
                    */

                    switch (deviceChangeEvent)
                    {
                        case DeviceChangeEvent.DBT_DEVNODES_CHANGED:
                            if (log)
                            {
                                #pragma warning disable CS0162 // Unreachable code detected
                                Log.PrintLine(TAG, Log.LogLevel.Information, String.Empty);
                                #pragma warning restore CS0162 // Unreachable code detected
                                Log.PrintLine(TAG, Log.LogLevel.Information, "WndProc.WM_DEVICECHANGE DBT_DEVNODES_CHANGED");
                            }
                            break;
                        case DeviceChangeEvent.DBT_DEVICEARRIVAL:
                            WndProcHandleDeviceChange(ChangeType.Add, m.LParam, log);
                            break;
                        case DeviceChangeEvent.DBT_DEVICEREMOVECOMPLETE:
                            WndProcHandleDeviceChange(ChangeType.Remove, m.LParam, log);
                            break;
                    }
                    break;
                }
            }
        }

        private void WndProcHandleDeviceChange(ChangeType changeType, IntPtr lParam, bool log)
        {
            const bool logDevices = false;

            var pHdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_HDR));
            var deviceType = pHdr.dbch_devicetype;
            if (log)
            {
                Log.PrintLine(TAG, Log.LogLevel.Information, String.Empty);
                Log.PrintLine(TAG, Log.LogLevel.Information, $"WndProcHandleDeviceChange {changeType} deviceType={deviceType}");
            }
            switch (deviceType)
            {
                case DeviceType.DBT_DEVTYP_DEVICEINTERFACE:
                    {
                        //Log.PrintLine(TAG, LogLevel.Information, $"WndProcHandleDeviceChange {changeType} devType=DBT_DEVTYP_DEVICEINTERFACE");
                        var pDevice = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                        var devicePath = pDevice.dbcc_name;
                        //Log.PrintLine(TAG, LogLevel.Information, $"WndProcHandleDeviceChange {changeType} RAW pDevice.dbcc_name={Utils.Quote(devicePath)}");
                        devicePath = CleanUpDeviceName(devicePath);
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"WndProcHandleDeviceChange {changeType} pDevice.dbcc_name={Utils.Quote(devicePath)}");
                        }

                        if (logDevices)
                        {
                            #pragma warning disable CS0162 // Unreachable code detected
                            GetUsbDevices(log: true, caller: "WndProc.WM_DEVICECHANGE logDevices");
                            #pragma warning restore CS0162 // Unreachable code detected
                        }

                        var devicePathsSubscribed = DevicePathsSubscribed;
                        if (devicePathsSubscribed.Count == 0 || devicePathsSubscribed.Contains(devicePath))
                        {
                            InvokeOnUsbChange(changeType, devicePath);
                        }
                        break;
                    }
                case DeviceType.DBT_DEVTYP_PORT:
                    {
                        //Log.PrintLine(TAG, LogLevel.Information, $"WndProcHandleDeviceChange {changeType} devType=DBT_DEVTYP_PORT");
                        var pPort = Marshal.PtrToStructure<DEV_BROADCAST_PORT>(lParam);
                        var portName = pPort.dbcp_name;
                        //Log.PrintLine(TAG, LogLevel.Information, $"WndProcHandleDeviceChange {changeType} RAW pPort.dbcc_name={Utils.Quote(portName)}");
                        portName = CleanUpDeviceName(portName);
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"WndProcHandleDeviceChange {changeType} pPort.dbcp_name={Utils.Quote(portName)}");
                        }
                        /*
                        var serialPortsSubscribed = SerialPortsSubscribed;
                        if (serialPortsSubscribed.Count == 0 || serialPortsSubscribed.Contains(portName))
                        {
                            OnSerialPortChange?.Invoke(this, new SerialPortChangeEventArgs(changeType, portName));
                        }
                        */
                        break;
                    }
            }
        }

        /// <summary>
        /// There seems to be a race-condition where a device reported as DBT_DEVICEARRIVAL doesn't show up when enumerating all devices.
        /// A quick temporary fix is to add a small delay before reporting the change.
        /// I will investigate this and hopefully remove the delay hack.
        /// </summary>
        /// <param name="changeType"></param>
        /// <param name="devicePath"></param>
        private async void InvokeOnUsbChange(ChangeType changeType, string devicePath)
        {
            // TODO:(pv) Try to find way to invoke this on the next message pump
            await Task.Delay(200);
            // TODO:(pv) Better way to debounce?
            OnUsbChange?.Invoke(this, new UsbChangeEventArgs(changeType, devicePath));
        }

        private static string CleanUpDeviceName(string deviceName)
        {
            //Log.PrintLine(TAG, LogLevel.Information, $"CleanUpDeviceName BEFORE devicePath={Utils.Quote(deviceName)}");

            // Remove null-terminated data from the string
            var pos = deviceName.IndexOf((char)0);
            if (pos != -1)
            {
                deviceName = deviceName[..pos];
            }

            deviceName = deviceName.ToLower();

            //Log.PrintLine(TAG, LogLevel.Information, $"CleanUpDeviceName AFTER devicePath={Utils.Quote(deviceName)}");

            return deviceName;
        }

        public static DeviceInfo FindDevice(string devicePath)
        {
            devicePath = devicePath.ToLower();

            if (!devicePath.StartsWith('\"'))
            {
                devicePath = $"\"{devicePath}";
            }
            if (!devicePath.EndsWith('\"'))
            {
                devicePath = $"{devicePath}\"";
            }

            foreach (var deviceInfo in GetUsbDevices())
            {
                //Log.PrintLine(TAG, LogLevel.Information, $"FindDevice deviceInfo={(deviceInfo != null ? deviceInfo.ToString(true) : "null")}");
                var thisDevicePath = deviceInfo.Path;
                //Log.PrintLine(TAG, LogLevel.Information, $"FindDevice thisDevicePath={Utils.Quote(thisDevicePath)}");
                if (thisDevicePath == devicePath)
                {
                    return deviceInfo;
                }
            }

            return null;
        }

        /// <summary>
        /// SerialPort.GetPortNames() IS BUGGY!
        /// It is even worse than these indicate:
        /// https://stackoverflow.com/questions/32040209/serialport-getportnames-returns-incorrect-port-names
        /// https://stackoverflow.com/questions/8218153/serialport-getportnames-is-wrong?noredirect=1&lq=1
        /// https://social.msdn.microsoft.com/Forums/vstudio/en-US/a78b4668-ebb6-46aa-9985-ec41667abdde/ioportsserialportgetportnames-registrykeygetvalue-corruption-with-usbsersys-driver-on-windows?forum=netfxbcl
        /// Basically, SerialPort.GetPortNames() can return [for example] an old COM5 when it is currently COM3.
        /// This method is a hack to work around this system bug.
        /// </summary>
        public static string GetUsbSerialDeviceComPort(string devicePath)
        {
            //Log.PrintLine(TAG, LogLevel.Verbose, $"GetUsbSerialDeviceComPort(devicePath={devicePath})");
            string portName = null;
            if (devicePath != null)
            {
                var match = Regex.Match(devicePath, @"usb#vid_(?<VID>\d{4})&pid_(?<PID>\d{4})#(?<IID>.*?)#{(?<CLS>.*?)}", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var deviceVid = match.Groups["VID"].Value;
                    var devicePid = match.Groups["PID"].Value;
                    var deviceInstanceId = match.Groups["IID"].Value;
                    var deviceEnumPath = $@"SYSTEM\CurrentControlSet\Enum\USB\VID_{deviceVid}&PID_{devicePid}\{deviceInstanceId}";
                    var k1 = Registry.LocalMachine.OpenSubKey(deviceEnumPath);
                    if (k1 != null)
                    {
                        var deviceParentIdPrefix = k1.GetValue("ParentIdPrefix") as string;
                        var prefix = $@"USB\VID_{match.Groups["VID"]}&PID_{match.Groups["PID"]}";
                        var k2 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\usbser\Enum");
                        if (k2 != null)
                        {
                            var count = (int) k2.GetValue("Count");
                            for (int i = 0; i < count; i++)
                            {
                                var deviceID = k2.GetValue(i.ToString("D", CultureInfo.InvariantCulture)) as string;
                                if (deviceID.StartsWith(prefix) && deviceID.IndexOf(deviceParentIdPrefix) > prefix.Length)
                                {
                                    deviceEnumPath = $@"SYSTEM\CurrentControlSet\Enum\{deviceID}\Device Parameters";
                                    var k3 = Registry.LocalMachine.OpenSubKey(deviceEnumPath);
                                    if (k3 != null)
                                    {
                                        portName = k3.GetValue("PortName") as string;
                                        if (portName != null)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return portName;
        }
    }
}
