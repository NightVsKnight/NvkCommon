using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static NvkCommon.Utils;

/// <summary>
/// Many ideas pulled from:
/// * https://github.com/Microsoft/Windows-driver-samples/blob/master/usb/usbview/
/// * https://github.com/mikeobrien/HidLibrary/tree/master/src/HidLibrary
/// * https://github.com/GeorgeHahn/LibUsbDotNet/blob/master/InfWizard/SetupApi.cs
/// * https://sourceforge.net/p/usbviewerincsha/code/HEAD/tree/trunk/UsbLib/UsbDevice.cs
/// * http://www.emmet-gray.com/AdminTools.html "USBView" C# version of Windows-driver-sample
/// </summary>
namespace NvkCommon
{
    public partial class UsbManager
    {
        #region Native

        //
        // REFERENCE;
        //  https://docs.microsoft.com/en-us/dotnet/framework/interop/marshaling-data-with-platform-invoke
        //

        public static bool IsHandleInvalid(IntPtr handle)
        {
            return handle == IntPtr.Zero || handle.ToInt64() == INVALID_HANDLE_VALUE;
        }

        public static bool IsHandleValid(IntPtr handle)
        {
            return !IsHandleInvalid(handle);
        }

        [DebuggerDisplay("{ToString(true)}")]
        public class GuidInfo
        {
            public string Name { get; private set; }
            public Guid Guid { get; private set; }

            internal GuidInfo(string name, string guid) : this(name, new Guid(guid)) { }

            internal GuidInfo(string name, Guid guid)
            {
                Name = name;
                Guid = guid;
            }

            public override string ToString()
            {
                return ToString(false);
            }

            public string ToString(bool debug)
            {
                var s = $"Name={Quote(Name)}, Guid={Guid.ToString("B")}";
                return debug ? s : $"{{{s}}}";
            }
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-usb-device
        /// </summary>
        private static readonly GuidInfo GUID_DEVINTERFACE_USB_DEVICE = new GuidInfo("GUID_DEVINTERFACE_USB_DEVICE", "a5dcbf10-6530-11d2-901f-00c04fb951ed");
        private static readonly GuidInfo GUID_DEVCLASS_USB = new GuidInfo("GUID_DEVCLASS_USB", "36fc9e60-c465-11cf-8056-444553540000");

        private static GuidInfo _hidClassGuid = null;

        /// <summary>
        /// Usually {4d1e55b2-f16f-11cf-88cb-001111000030}
        /// </summary>
        private static GuidInfo HidClassGuid
        {
            get
            {
                if (_hidClassGuid == null)
                {
                    Guid guid = Guid.Empty;
                    HidD_GetHidGuid(ref guid);
                    _hidClassGuid = new GuidInfo("HidClassGuid", guid);
                }
                return _hidClassGuid;
            }
        }

        private static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY { fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), pid = 4 };

        private const int INVALID_HANDLE_VALUE = -1;
        private const int ERROR_INSUFFICIENT_BUFFER = 0x0000007a;

        // https://github.com/tpn/winsdk-10/blob/master/Include/10.0.16299.0/um/cfgmgr32.h
        // typedef DWORD RETURN_TYPE;
        // typedef RETURN_TYPE CONFIGRET;
        // typedef DWORD       DEVNODE, DEVINST;
        // typedef DEVNODE    *PDEVNODE, *PDEVINST;
        private const UInt32 CR_SUCCESS = 0x00000000; // CONFIGRET

        private const int DIGCF_DEFAULT = 0x00000001;  // only valid with DIGCF_DEVICEINTERFACE
        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_ALLCLASSES = 0x00000004;
        private const int DIGCF_PROFILE = 0x00000008;
        private const int DIGCF_DEVICEINTERFACE = 0x00000010;

        private const int SPDRP_ADDRESS = 0x1c;
        private const int SPDRP_BUSNUMBER = 0x15;
        private const int SPDRP_BUSTYPEGUID = 0x13;
        private const int SPDRP_CAPABILITIES = 0xf;
        private const int SPDRP_CHARACTERISTICS = 0x1b;
        private const int SPDRP_CLASS = 7;
        private const int SPDRP_CLASSGUID = 8;
        private const int SPDRP_COMPATIBLEIDS = 2;
        private const int SPDRP_CONFIGFLAGS = 0xa;
        private const int SPDRP_DEVICE_POWER_DATA = 0x1e;
        private const int SPDRP_DEVICEDESC = 0;
        private const int SPDRP_DEVTYPE = 0x19;
        private const int SPDRP_DRIVER = 9;
        private const int SPDRP_ENUMERATOR_NAME = 0x16;
        private const int SPDRP_EXCLUSIVE = 0x1a;
        private const int SPDRP_FRIENDLYNAME = 0xc;
        private const int SPDRP_HARDWAREID = 1;
        private const int SPDRP_LEGACYBUSTYPE = 0x14;
        private const int SPDRP_LOCATION_INFORMATION = 0xd;
        private const int SPDRP_LOWERFILTERS = 0x12;
        private const int SPDRP_MFG = 0xb;
        private const int SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0xe;
        private const int SPDRP_REMOVAL_POLICY = 0x1f;
        private const int SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x20;
        private const int SPDRP_REMOVAL_POLICY_OVERRIDE = 0x21;
        private const int SPDRP_SECURITY = 0x17;
        private const int SPDRP_SECURITY_SDS = 0x18;
        private const int SPDRP_SERVICE = 4;
        private const int SPDRP_UI_NUMBER = 0x10;
        private const int SPDRP_UI_NUMBER_DESC_FORMAT = 0x1d;
        private const int SPDRP_UPPERFILTERS = 0x11;

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_devinfo_data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize; // DWORD
            public Guid ClassGuid; // GUID
            public UInt32 DevInst; // DWORD
            public UIntPtr Reserved; // ULONG_PTR
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_device_interface_data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public UInt32 cbSize; // DWORD
            public Guid InterfaceClassGuid; // GUID
            public UInt32 Flags; // DWORD
            public UIntPtr Reserved;
        }

        private const int DEVICE_PATH_MAX_LENGTH = 1024;
        private const int ANYSIZE_ARRAY = 1;

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-sp_device_interface_detail_data_a
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-sp_device_interface_detail_data_w
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize; // DWORD
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ANYSIZE_ARRAY)]
            public string DevicePath; // TCHAR[ANYSIZE_ARRAY]
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows-hardware/drivers/install/devpropkey
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid; // DEVPROPGUID; typedef GUID  DEVPROPGUID, *PDEVPROPGUID;
            public UInt32 pid; // DEVPROPID; typedef ULONG DEVPROPID, *PDEVPROPID;
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_gethidguid
        /// </summary>
        /// <param name="hidGuid"></param>
        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(ref Guid hidGuid);

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetclassdevsw
        /// </summary>
        /// <param name="classGuid"></param>
        /// <param name="enumerator"></param>
        /// <param name="hwndParent"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs( // HDEVINFO
            ref Guid classGuid, // GUID
            [MarshalAs(UnmanagedType.LPTStr)] string enumerator, // PCWSTR
            IntPtr hwndParent, // HWND
            UInt32 flags); // DWORD

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdienumdeviceinfo
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <param name="memberIndex"></param>
        /// <param name="deviceInfoData"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet, // HDEVINFO
            UInt32 memberIndex, // DWORD
            ref SP_DEVINFO_DATA deviceInfoData); // PSP_DEVINFO_DATA

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdienumdeviceinterfaces
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <param name="deviceInfoData"></param>
        /// <param name="interfaceClassGuid"></param>
        /// <param name="memberIndex"></param>
        /// <param name="deviceInterfaceData"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet, // HDEVINFO
            ref SP_DEVINFO_DATA deviceInfoData, // PSP_DEVINFO_DATA
            ref Guid interfaceClassGuid, // GUID
            UInt32 memberIndex, // DWORD
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData); // PSP_DEVICE_INTERFACE_DATA

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetdeviceinterfacedetaila
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetdeviceinterfacedetailw
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <param name="deviceInterfaceData"></param>
        /// <param name="deviceInterfaceDetailData"></param>
        /// <param name="deviceInterfaceDetailDataSize"></param>
        /// <param name="requiredSize"></param>
        /// <param name="deviceInfoData"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet, // HDEVINFO
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, // PSP_DEVICE_INTERFACE_DATA
            IntPtr deviceInterfaceDetailData, // PSP_DEVICE_INTERFACE_DETAIL_DATA
            UInt32 deviceInterfaceDetailDataSize, // DWORD
            ref UInt32 requiredSize, // PDWORD
            ref SP_DEVINFO_DATA deviceInfoData); // PSP_DEVINFO_DATA

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceInfo"></param>
        /// <param name="deviceInfoData"></param>
        /// <param name="propkey"></param>
        /// <param name="propertyDataType"></param>
        /// <param name="propertyBuffer"></param>
        /// <param name="propertyBufferSize"></param>
        /// <param name="requiredSize"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", SetLastError = true)]
        private static extern bool SetupDiGetDeviceProperty(
            IntPtr deviceInfo,
            ref SP_DEVINFO_DATA deviceInfoData,
            ref DEVPROPKEY propkey,
            ref ulong propertyDataType,
            byte[] propertyBuffer,
            int propertyBufferSize,
            ref int requiredSize,
            uint flags);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <param name="deviceInfoData"></param>
        /// <param name="propertyVal"></param>
        /// <param name="propertyRegDataType"></param>
        /// <param name="propertyBuffer"></param>
        /// <param name="propertyBufferSize"></param>
        /// <param name="requiredSize"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryProperty", SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            int propertyVal,
            ref int propertyRegDataType,
            byte[] propertyBuffer,
            int propertyBufferSize,
            ref int requiredSize);

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdidestroydeviceinfolist
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(
            IntPtr deviceInfoSet); // HDEVINFO

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_parent
        /// </summary>
        /// <param name="pdnDevInst"></param>
        /// <param name="dnDevInst"></param>
        /// <param name="ulFlags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern UInt32 CM_Get_Parent( // CONFIGRET
            out UInt32 pdnDevInst, // PDEVINST
            UInt32 dnDevInst, // DEVINST
            UInt32 ulFlags); // ULONG

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_child
        /// </summary>
        /// <param name="pdnDevInst"></param>
        /// <param name="dnDevInst"></param>
        /// <param name="ulFlags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern UInt32 CM_Get_Child( // CONFIGRET
            out UInt32 pdnDevInst, // PDEVINST
            UInt32 dnDevInst, // DEVINST
            UInt32 ulFlags); // ULONG

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_sibling
        /// </summary>
        /// <param name="pdnDevInst"></param>
        /// <param name="dnDevInst"></param>
        /// <param name="ulFlags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern UInt32 CM_Get_Sibling( // CONFIGRET
            out UInt32 pdnDevInst, // PDEVINST
            UInt32 dnDevInst, // DEVINST
            UInt32 ulFlags); // ULONG

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_device_id_size
        /// </summary>
        /// <param name="pdnDevInst"></param>
        /// <param name="dnDevInst"></param>
        /// <param name="ulFlags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern UInt32 CM_Get_Device_ID_Size( // CONFIGRET
            out UInt32 pulLen, // PULONG
            UInt32 dnDevInst, // DEVINST
            UInt32 ulFlags); // ULONG

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_device_ida
        /// https://docs.microsoft.com/en-us/windows/desktop/api/cfgmgr32/nf-cfgmgr32-cm_get_device_idw
        /// </summary>
        /// <param name="dnDevInst"></param>
        /// <param name="Buffer"></param>
        /// <param name="BufferLen"></param>
        /// <param name="ulFlags"></param>
        /// <returns></returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern UInt32 CM_Get_Device_ID( // CONFIGRET
            UInt32 dnDevInst, // DEVINST
            StringBuilder Buffer, // PWSTR
            UInt32 BufferLen, // ULONG
            UInt32 ulFlags); // ULONG

        //
        //
        //

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/devio/device-management-messages
        /// </summary>
        private enum DeviceManagementMessages : ushort
        {
            /// <summary>
            /// https://learn.microsoft.com/en-us/windows/win32/devio/wm-devicechange
            /// Notifies an application of a change to the hardware configuration of a device or the computer.
            /// </summary>
            WM_DEVICECHANGE = 0x0219, // device change event
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/devio/device-management-events
        /// </summary>
        private enum DeviceChangeEvent: ushort
        {
            /// <summary>
            /// https://learn.microsoft.com/en-us/windows/win32/devio/dbt-devnodes-changed
            /// "A device has been added to or removed from the system."
            /// </summary>
            DBT_DEVNODES_CHANGED = 0x0007,
            /// <summary>
            /// https://learn.microsoft.com/en-us/windows/win32/devio/dbt-devicearrival
            /// system detected a new device
            /// </summary>
            DBT_DEVICEARRIVAL = 0x8000,
            /// <summary>
            /// https://learn.microsoft.com/en-us/windows/win32/devio/dbt-deviceremovecomplete
            /// "A device or piece of media has been removed."
            /// </summary>
            DBT_DEVICEREMOVECOMPLETE = 0x8004,
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/dbt/ns-dbt-dev_broadcast_hdr
        /// </summary>
        private enum DeviceType : int
        {
            /// <summary>
            /// OEM- or IHV-defined device type. This structure is a DEV_BROADCAST_OEM structure.
            /// </summary>
            DBT_DEVTYP_OEM = 0x00000000,
            /// <summary>
            /// ...
            /// </summary>
            DBT_DEVTYP_DEVNODE = 0x00000001,
            /// <summary>
            /// Logical volume. This structure is a DEV_BROADCAST_VOLUME structure.
            /// </summary>
            DBT_DEVTYP_VOLUME = 0x00000002,
            /// <summary>
            /// Port device (serial or parallel). This structure is a DEV_BROADCAST_PORT structure.
            /// </summary>
            DBT_DEVTYP_PORT = 0x00000003,
            /// <summary>
            /// ...
            /// </summary>
            DBT_DEVTYP_NET = 0x00000004,
            /// <summary>
            /// Class of devices. This structure is a DEV_BROADCAST_DEVICEINTERFACE structure.
            /// </summary>
            DBT_DEVTYP_DEVICEINTERFACE = 0x00000005,
            /// <summary>
            /// File system handle. This structure is a DEV_BROADCAST_HANDLE structure.
            /// </summary>
            DBT_DEVTYP_HANDLE = 0x00000006,
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/dbt/ns-dbt-dev_broadcast_hdr
        /// http://www.pinvoke.net/default.aspx/Structures.DEV_BROADCAST_HDR
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public DeviceType dbch_devicetype;
            public int dbch_reserved;
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/dbt/ns-dbt-dev_broadcast_port_w
        /// http://www.pinvoke.net/default.aspx/Structures.DEV_BROADCAST_PORT
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEV_BROADCAST_PORT
        {
            public int dbcp_size;
            public DeviceType dbcp_devicetype;
            public int dbcp_reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string dbcp_name;

            //public static readonly int Size = Marshal.SizeOf(typeof(DEV_BROADCAST_PORT));
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/dbt/ns-dbt-dev_broadcast_deviceinterface_w
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public DeviceType dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string dbcc_name;

            //public static readonly int Size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE));
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        //
        //
        //

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CancelIo(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const short FILE_SHARE_READ = 0x1;
        private const short FILE_SHARE_WRITE = 0x2;
        private const short OPEN_EXISTING = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/nf-fileapi-createfilea
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <param name="dwDesiredAccess"></param>
        /// <param name="dwShareMode"></param>
        /// <param name="lpSecurityAttributes"></param>
        /// <param name="dwCreationDisposition"></param>
        /// <param name="dwFlagsAndAttributes"></param>
        /// <param name="hTemplateFile"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, int dwShareMode, ref SECURITY_ATTRIBUTES lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, int hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        #endregion Native

        //
        //
        //

        [DebuggerDisplay("{ToString(true)}")]
        public class DeviceInfo
        {
            public string Path { get; internal set; }
            public string Description { get; internal set; }
            public string Parent { get; internal set; }
            public List<string> Children { get; internal set; }

            public override string ToString()
            {
                return ToString(false);
            }

            public string ToString(bool debug)
            {
                var parentChildren = $"Parent={Quote(Parent)}, Children({Children?.Count ?? 0})={Utils.ToString(Children)}";
                if (debug)
                {
                    return $"Description={Quote(Description)}, Path={Quote(Path)}, {parentChildren}";
                }
                else
                {
                    return $"{Quote(Description)} ({Quote(Path)}); {parentChildren}";
                }
            }
        }

        public static List<DeviceInfo> GetUsbHidDevices(string filter = null, bool log = false, string parent = null, string caller = null)
        {
            caller += ".GetUsbHidDevices";
            return GetUsbDevices(filter: filter, classGuid: HidClassGuid, log: log, parent: parent, caller: caller);
        }

        /// <summary>
        /// References:
        /// http://visualprog.cz/Net/DetectDeviceClasses.htm
        /// https://nakov.com/blog/2009/05/10/enumerate-all-com-ports-and-find-their-name-and-deviceDescription-in-c/
        /// http://qaru.site/questions/364958/setupdigetdeviceproperty-usage-example
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="classGuid"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static List<DeviceInfo> GetUsbDevices(string filter = null, GuidInfo classGuid = null, bool log = false, string parent = null, string caller = null)
        {
            caller += ".GetUsbDevices";

            if (filter != null)
            {
                filter = filter.ToLower();
            }

            if (classGuid == null)
            {
                classGuid = GUID_DEVINTERFACE_USB_DEVICE;
            }

            if (log)
            {
                Log.PrintLine(TAG, Log.LogLevel.Information, String.Empty);
                Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller}(filter={Quote(filter)}, classGuid={classGuid}, log={log}, parent={Quote(parent)})");
            }

            var devices = new List<DeviceInfo>();

            var _classGuid = classGuid.Guid;
            var deviceInfoSet = SetupDiGetClassDevs(ref _classGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (IsHandleValid(deviceInfoSet))
            {
                var deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);

                var deviceIndex = 0U;
                while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex, ref deviceInfoData))
                {
                    deviceIndex += 1;

                    var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                    deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(deviceInterfaceData);

                    var deviceInterfaceIndex = 0U;
                    while (SetupDiEnumDeviceInterfaces(deviceInfoSet, ref deviceInfoData, ref _classGuid, deviceInterfaceIndex, ref deviceInterfaceData))
                    {
                        deviceInterfaceIndex++;

                        var devicePath = GetDevicePath(deviceInfoSet, deviceInfoData, deviceInterfaceData);
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} devicePath={Quote(devicePath)}");
                        }
                        if (devicePath == null)
                        {
                            if (log)
                            {
                                Log.PrintLine(TAG, Log.LogLevel.Warning, $"{caller} UNEXPECTED devicePath=null; ignoring");
                            }
                            continue;
                        }
                        if (!String.IsNullOrEmpty(filter) && !devicePath.Contains(filter))
                        {
                            if (log)
                            {
                                Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} devicePath does not match filter; ignoring");
                            }
                            continue;
                        }
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, String.Empty);
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} MATCH devicePath={Quote(devicePath)}");
                        }

                        UInt32 devInstDevice = deviceInfoData.DevInst;

                        string parentDeviceId = null;
                        if (CM_Get_Parent(out UInt32 devInstParent, devInstDevice, 0) == CR_SUCCESS)
                        {
                            parentDeviceId = CM_Get_Device_ID(devInstParent);
                        }
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} parentDeviceId={Quote(parentDeviceId)}");
                        }
                        if (!string.IsNullOrEmpty(parent) && !parent.Contains(parentDeviceId))
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} parent does not match; ignoring");
                            continue;
                        }

                        List<string> children = GetChildren(caller, devInstDevice, log: log);
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} children={Utils.ToString(children)}");
                        }

                        var deviceDescription = GetBusReportedDeviceDescription(deviceInfoSet, ref deviceInfoData) ??
                                                GetDeviceDescription(deviceInfoSet, ref deviceInfoData);
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} deviceDescription={Quote(deviceDescription)}, devicePath={Quote(devicePath)}");
                        }

                        var deviceInfo = new DeviceInfo
                        {
                            Path = devicePath,
                            Description = deviceDescription,
                            Parent = parentDeviceId,
                            Children = children
                        };
                        if (log)
                        {
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"{caller} ADDING deviceInfo={deviceInfo}");
                        }

                        devices.Add(deviceInfo);
                    }
                }
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            if (log)
            {
                Log.PrintLine(TAG, Log.LogLevel.Information, "");
            }

            return devices;
        }

        public static string CM_Get_Device_ID(UInt32 devInst)
        {
            string devInstDeviceId = null;
            if (CM_Get_Device_ID_Size(out uint devInstDeviceIdSize, devInst, 0) == CR_SUCCESS)
            {
                devInstDeviceIdSize += (uint)Marshal.SystemDefaultCharSize;
                var buffer = new StringBuilder((int)devInstDeviceIdSize);
                if (CM_Get_Device_ID(devInst, buffer, devInstDeviceIdSize, 0) == CR_SUCCESS)
                {
                    devInstDeviceId = buffer.ToString();
                    devInstDeviceId = devInstDeviceId.ToLower();
                    devInstDeviceId = devInstDeviceId.Replace("\\", "#");
                }
            }
            return devInstDeviceId;
        }

        private static List<string> GetChildren(string caller, UInt32 devInstDevice, bool log = false)
        {
            caller += ".GetChildren";
            List<string> children = new List<string>();
            //Log.PrintLine(TAG, LogLevel.Information, $"CM_Get_Child(..., {devInstDevice})");
            if (CM_Get_Child(out uint devInstChild, devInstDevice, 0) == CR_SUCCESS)
            {
                string devInstChildDeviceId = CM_Get_Device_ID(devInstChild);
                if (log)
                {
                    //Log.PrintLine(TAG, LogLevel.Information, $"{caller} CM_Get_Child CM_Get_Device_ID devInstChildDeviceId={Quote(devInstChildDeviceId)}");
                }
                if (devInstChildDeviceId != null)
                {
                    children.Add(devInstChildDeviceId);
                }
                while (CM_Get_Sibling(out uint devInstSibling, devInstChild, 0) == CR_SUCCESS)
                {
                    string devInstSiblingDeviceId = CM_Get_Device_ID(devInstSibling);
                    if (log)
                    {
                        //Log.PrintLine(TAG, LogLevel.Information, $"{caller} CM_Get_Sibling CM_Get_Device_ID devInstSiblingDeviceId={Quote(devInstSiblingDeviceId)}");
                    }
                    if (devInstSiblingDeviceId != null)
                    {
                        children.Add(devInstSiblingDeviceId);
                    }
                    devInstChild = devInstSibling;
                }
            }
            return children;
        }

        /// <summary>
        /// From:
        /// https://stackoverflow.com/a/37713668/252308
        /// See Also:
        /// https://stackoverflow.com/a/30981402/252308
        /// https://stackoverflow.com/a/33086877
        /// </summary>
        /// <param name="deviceInfoSet"></param>
        /// <param name="deviceInfoData"></param>
        /// <param name="deviceInterfaceData"></param>
        /// <returns></returns>
        private static string GetDevicePath(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
        {
            var deviceInterfaceDetailData = IntPtr.Zero;

            var requiredSize = 0U;
            if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, deviceInterfaceDetailData, requiredSize, ref requiredSize, ref deviceInfoData))
            {
                var lastWin32Error = Marshal.GetLastWin32Error();
                if (lastWin32Error != ERROR_INSUFFICIENT_BUFFER)
                {
                    var lastWin32Exception = new Win32Exception(lastWin32Error);
                    return null;
                }
            }

            var structSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA));

            if (requiredSize <= structSize)
            {
                return null;
            }

            try
            {
                deviceInterfaceDetailData = Marshal.AllocHGlobal((int)requiredSize);
                Marshal.WriteInt32(deviceInterfaceDetailData, IntPtr.Size == 8 ? 8 : 6);

                if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, deviceInterfaceDetailData, requiredSize, ref requiredSize, ref deviceInfoData))
                {
                    var lastWin32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                    return null;
                }

                var offset = structSize - (2 * Marshal.SystemDefaultCharSize);
                var devicePath = Marshal.PtrToStringUni(new IntPtr(deviceInterfaceDetailData.ToInt64() + offset));

                return devicePath.ToLower();
            }
            finally
            {
                Marshal.FreeHGlobal(deviceInterfaceDetailData);
            }
        }

        private static string GetBusReportedDeviceDescription(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devinfoData)
        {
            var descriptionBuffer = new byte[1024];
            if (Environment.OSVersion.Version.Major > 5)
            {
                ulong propertyType = 0;
                var requiredSize = 0;
                var _continue = SetupDiGetDeviceProperty(
                    deviceInfoSet,
                    ref devinfoData,
                    ref DEVPKEY_Device_BusReportedDeviceDesc,
                    ref propertyType,
                    descriptionBuffer,
                    descriptionBuffer.Length,
                    ref requiredSize,
                    0);
                if (_continue)
                {
                    return descriptionBuffer.ToUTF16String();
                }
            }
            return null;
        }

        private static string GetDeviceDescription(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devinfoData)
        {
            var descriptionBuffer = new byte[1024];
            var requiredSize = 0;
            var type = 0;
            SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref devinfoData,
                SPDRP_DEVICEDESC,
                ref type,
                descriptionBuffer,
                descriptionBuffer.Length,
                ref requiredSize);
            return descriptionBuffer.ToUTF8String();
        }

        //
        //
        //

        private static IntPtr OpenDeviceIO(string devicePath)
        {
            var deviceAccess = GENERIC_READ | GENERIC_WRITE;
            var shareMode = FILE_SHARE_READ | FILE_SHARE_WRITE;
            var security = new SECURITY_ATTRIBUTES();
            var disposition = OPEN_EXISTING;
            var flags = 0;

            security.lpSecurityDescriptor = IntPtr.Zero;
            security.bInheritHandle = true;
            security.nLength = Marshal.SizeOf(security);

            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"OpenDeviceIO: CreateFile({Quote(devicePath)}, 0x{deviceAccess:X8}, 0x{shareMode:X8}, ref security, OPEN_EXISTING, 0x{flags:X8}, 0)");
            var hidHandle = CreateFile(devicePath, deviceAccess, shareMode, ref security, disposition, flags, 0);
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"OpenDeviceIO: CreateFile: hidHandle=0x{hidHandle.ToString("X8")}");
            if (hidHandle.ToInt64() == INVALID_HANDLE_VALUE)
            {
                var lastWin32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                Log.PrintLine(TAG, Log.LogLevel.Error, $"OpenDeviceIO: CreateFile lastWin32Exception={lastWin32Exception}");
            }
            return hidHandle;
        }

        private static bool WriteDeviceData(IntPtr handle, byte[] buffer)
        {
            if (IsHandleInvalid(handle))
            {
                return false;
            }
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteDeviceData: WriteFile(0x{handle.ToString("X8")}, {ToHexString(buffer)}, {buffer.Length}, out bytesWritten, IntPtr.Zero)");
            var success = WriteFile(handle, buffer, (uint)buffer.Length, out uint bytesWritten, IntPtr.Zero);
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteDeviceData: WriteFile: success={success}, bytesWritten={bytesWritten}");
            if (!success)
            {
                var lastWin32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                Log.PrintLine(TAG, Log.LogLevel.Error, $"WriteDeviceData: WriteFile lastWin32Exception={lastWin32Exception}");
            }
            return success;
        }

        private static void CloseDeviceIO(IntPtr handle)
        {
            if (IsHandleInvalid(handle))
            {
                return;
            }
            if (Environment.OSVersion.Version.Major > 5)
            {
                CancelIoEx(handle, IntPtr.Zero);
            }
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"CloseDeviceIO: CloseHandle(0x{handle.ToString("X8")})");
            var success = CloseHandle(handle);
            if (!success)
            {
                var lastWin32Exception = new Win32Exception(Marshal.GetLastWin32Error());
                Log.PrintLine(TAG, Log.LogLevel.Error, $"CloseDeviceIO: CloseHandle lastWin32Exception={lastWin32Exception}");
            }
        }

        public static bool WriteDeviceData(string devicePath, byte[] buffer)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteDeviceData(devicePath={Quote(devicePath)}, buffer={ToHexString(buffer)})");
            var handle = OpenDeviceIO(devicePath);
            if (IsHandleInvalid(handle))
            {
                return false;
            }
            try
            {
                return WriteDeviceData(handle, buffer);
            }
            finally
            {
                CloseDeviceIO(handle);
            }
        }
    }
}
