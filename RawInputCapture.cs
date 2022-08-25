using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static NvkCommon.Log;

#pragma warning disable IDE0130
namespace NvkCommon
#pragma warning restore IDE0130
{
    public class RawInputCapture(string Name)
    {
        private static readonly string TAG = NvkCommon.Log.TAG(typeof(RawInputCapture));

        public enum KeyDirection
        {
            Down,
            Up
        }

        public class KeyEventInfo(KeyDirection keyDirection, RAWKEYBOARD keyInfo)
        {
            public KeyDirection KeyDirection = keyDirection;
            public RAWKEYBOARD KeyInfo = keyInfo;

            public override string ToString()
            {
                return $"{{KeyDirection={KeyDirection}, KeyInfo={KeyInfo}}}";
            }

            public override bool Equals(object obj)
            {
                if (obj is KeyEventInfo keyEventInfo)
                {
                    return KeyDirection == keyEventInfo.KeyDirection &&
                           KeyInfo == keyEventInfo.KeyInfo;
                }
                return false;
            }

            public static bool operator ==(KeyEventInfo left, KeyEventInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(KeyEventInfo left, KeyEventInfo right)
            {
                return !(left == right);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(KeyDirection, KeyInfo);
            }
        }

        public class KeyboardEventArgs(KeyEventInfo keyEventInfo) : EventArgs
        {
            public static bool IsCapsLockOn { get { return (Utils.GetKeyState((int)Keys.Capital) & 0x0001) == 0x0001; } }

            public KeyEventInfo KeyEventInfo { get; private set; } = keyEventInfo;

            public override string ToString()
            {
                return $"{{KeyEventInfo={KeyEventInfo}}}";
            }
        }

        public delegate void KeyboardEventHandler(object sender, KeyboardEventArgs e);
        public event KeyboardEventHandler OnKeyboard;

        public class MouseButtonEventArgs : EventArgs
        {
            public RawMouseButtonFlags Flags { get; internal set; } = 0;
            public ushort Data { get; internal set; } = 0;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"{{Flags={Flags}");
                if (Flags.HasFlag(RawMouseButtonFlags.MouseWheelVertical))
                {
                    short wheelDelta = (short)Data;
                    short scrollDelta = (short)((float)wheelDelta / 120);
                    sb.Append($", WheelVertical=0x{Data:X4}({wheelDelta}[{scrollDelta}])");
                }
                else if (Flags.HasFlag(RawMouseButtonFlags.MouseWheelHorizontal))
                {
                    short wheelDelta = (short)Data;
                    short scrollDelta = (short)((float)wheelDelta / 120);
                    sb.Append($", WheelHorizontal=0x{Data:X4}({wheelDelta}[{scrollDelta}])");
                }
                else
                {
                    sb.Append($", Data=0x{Data:X4}");
                }
                sb.Append("}}");
                return sb.ToString();
            }
        }

        private readonly MouseButtonEventArgs mouseButtonEventArgs = new();
        public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);
        public event MouseButtonEventHandler OnMouseButton;

        public class MouseMoveEventArgs : EventArgs
        {
            public int DeltaX { get; internal set; } = 0;
            public int DeltaY { get; internal set; } = 0;
            public int X { get; internal set; } = 0;
            public int Y { get; internal set; } = 0;

            public override string ToString()
            {
                return $"{{DeltaX={DeltaX}, DeltaY={DeltaY}, X={X}, Y={Y}}}";
            }
        }

        public readonly MouseMoveEventArgs mouseMoveEventArgs = new();
        public delegate void MouseMoveEventHandler(object sender, MouseMoveEventArgs e);
        public event MouseMoveEventHandler OnMouseMove;

        public bool IsCapturingKeyboard { get; private set; }
        public bool IsCapturingMouse { get; private set; }

        private Thread mouseMoveThread;
        private int mouseAccumulatedDeltaX = 0;
        private int mouseAccumulatedDeltaY = 0;
        private readonly object inputLock = new();

        public string Name { get; private set; } = Name;

        private void Log(LogLevel level, string message)
        {
            NvkCommon.Log.PrintLine(TAG, level, $"{Utils.Quote(Name)} {message}");
        }

        public void StartKeyboard(IntPtr handle)
        {
            RegisterRawInputKeyboard(handle);
            IsCapturingKeyboard = true;
        }

        public void StopKeyboard(IntPtr handle)
        {
            IsCapturingKeyboard = false;
            UnregisterRawInputKeyboard(handle);
        }

        public void StartMouse(IntPtr handle)
        {
            RegisterRawInputMouse(handle);
            IsCapturingMouse = true;
            if (mouseMoveThread == null)
            {
                mouseMoveThread = new Thread(MouseInputLoop);
                mouseMoveThread.Start();
            }
        }

        public void StopMouse(IntPtr handle)
        {
            IsCapturingMouse = false;
            if (mouseMoveThread != null)
            {
                mouseMoveThread.Join();
                mouseMoveThread = null;
            }
            UnregisterRawInputMouse(handle);
        }

        private void MouseInputLoop()
        {
            int deltaX, deltaY;
            while (IsCapturingMouse)
            {
                lock (inputLock)
                {
                    deltaX = mouseAccumulatedDeltaX;
                    deltaY = mouseAccumulatedDeltaY;
                    mouseAccumulatedDeltaX = 0;
                    mouseAccumulatedDeltaY = 0;
                }
                if (deltaX != 0 || deltaY != 0)
                {
                    var cusorPositon = Cursor.Position;
                    mouseMoveEventArgs.X = cusorPositon.X;
                    mouseMoveEventArgs.Y = cusorPositon.Y;
                    mouseMoveEventArgs.DeltaX = deltaX;
                    mouseMoveEventArgs.DeltaY = deltaY;
                    OnMouseMove?.Invoke(this, mouseMoveEventArgs);
                }
                // Simulate frame rate (60 FPS)
                Thread.Sleep(1000 / 60);
            }
        }

        /// <summary>
        /// Hook this into your Form's WndProc method.
        /// ```
        /// private readonly RawInput rawInput;
        /// 
        /// private Form1()
        /// {
        ///     rawInput = new RawInput(Handle);
        /// }
        /// 
        /// protected override void WndProc(ref Message m)
        /// {
        ///     rawInput.WndProc(ref m);
        ///     base.WndProc(ref m);
        /// }
        /// ```
        /// </summary>
        /// <param name="m"></param>
        public void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;
            if ((IsCapturingKeyboard || IsCapturingMouse) && m.Msg == WM_INPUT)
            {
                HandleInput(m.LParam);
            }
        }

        private RAWINPUT instanceInput = new();
        private readonly uint rawInputHeaderSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));

        private void HandleInput(IntPtr hDevice)
        {
            //Log(LogLevel.Verbose, $"HandleInput(0x{hDevice:X8})");
            uint dwSize = 0;
            var result1 = GetRawInputData(hDevice, RawInputCommand.Input, IntPtr.Zero, ref dwSize, rawInputHeaderSize);
            if (result1 != 0)
            {
                Log(LogLevel.Error, $"GetRawInputData failed: result1={result1}, dwSize={dwSize}, rawInputHeaderSize={rawInputHeaderSize}");
                return;
            }
            var result2 = GetRawInputData(hDevice, RawInputCommand.Input, out instanceInput, ref dwSize, rawInputHeaderSize);
            if (result2 != dwSize)
            {
                /*
                 * Huh?!?!?!
                 * Maybe I can only have one RawInputCapture instance running at a time?
                 * 
                 * 25/06/29 02:03:09.183 V PA588 T0001 FormMain InactivityIdleCountdownTimer_Tick: remaining=24, threads=317, pu=True
                 * 25/06/29 02:03:09.189 V PA588 T0001 FormMain MouseJiggleWheel(scrollAmount=4)
                 * 25/06/29 02:03:09.195 D PA588 T0001 InputIdleDetector(Global) RawInputCapture_OnMouseButton: Mouse activity detected after idle; raising OnInputAfterIdle event
                 * 25/06/29 02:03:09.197 V PA588 T0001 FormMain InputIdleDetector_OnInputAfterIdle(...)
                 * 25/06/29 02:03:09.204 V PA588 T0001 FormMain ObsWebSocketSetScene(scene="KNIGHT night")
                 * 25/06/29 02:03:09.207 I PA588 T0001 FormMain ObsWebSocketSetScene: Changing OBS scene to "KNIGHT night"
                 * 25/06/29 02:03:09.245 E PA588 T0001 RawInputCapture "InputIdleDetector(StarCitizen)" Failed to get raw input data. outSize=0, instanceInputSize=24
                 * 25/06/29 02:03:10.183 V PA588 T0001 FormMain InactivityIdleCountdownTimer_Tick: remaining=23, threads=317, pu=True
                 */
                Log(LogLevel.Error, $"GetRawInputData failed: result1={result1}, result2={result2}, dwSize={dwSize}, instanceInputSize={rawInputHeaderSize}, instanceInput={instanceInput}");
                return;
            }
            HandleInput(instanceInput);
        }

        private void HandleInput(RAWINPUT input)
        {
            //Log(LogLevel.Verbose, $"HandleInput({input})");
            switch (input.Header.Type)
            {
                case RawInputDeviceType.MOUSE:
                    {
                        var mouse = input.Data.Mouse;
                        //Log(LogLevel.Verbose, $"Input: mouse={mouse}");
                        lock (inputLock)
                        {
                            mouseAccumulatedDeltaX += mouse.lLastX;
                            mouseAccumulatedDeltaY += mouse.lLastY;
                        }
                        if (mouse.ButtonFlags != 0)
                        {
                            mouseButtonEventArgs.Flags = mouse.ButtonFlags;
                            mouseButtonEventArgs.Data = mouse.usButtonData;
                            OnMouseButton?.Invoke(this, mouseButtonEventArgs);
                        }
                        break;
                    }
                case RawInputDeviceType.KEYBOARD:
                    {
                        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard
                        /// <summary>
                        /// KEYBOARD_OVERRUN_MAKE_CODE is a special MakeCode value sent when an invalid or unrecognizable combination
                        /// of keys is pressed or the number of keys pressed exceeds the limit for this keyboard.
                        /// </summary>
                        const int KEYBOARD_OVERRUN_MAKE_CODE = 0xFF;
                        /// <summary>
                        /// Key Down
                        /// </summary>
                        //const int RI_KEY_MAKE = 0x00;
                        /// <summary>
                        /// Key Up
                        /// </summary>
                        const int RI_KEY_BREAK = 0x01;
                        /// <summary>
                        /// Left version of the key
                        /// </summary>
                        #pragma warning disable CS0219 // Variable is assigned but its value is never used
                        const int RI_KEY_E0 = 0x02;
                        #pragma warning restore CS0219 // Variable is assigned but its value is never used
                        /// <summary>
                        /// Right version of the key. Only seems to be set for the Pause/Break key.
                        /// </summary>
                        //const int RI_KEY_E1 = 0x04;

                        var keyboard = input.Data.Keyboard;
                        //Log(LogLevel.Verbose, $"Input: keyboard={keyboard}");

                        var virtualKey = (Keys)keyboard.VKey;
                        if ((int)virtualKey == KEYBOARD_OVERRUN_MAKE_CODE) return; // false;?

                        //var makeCode = keyboard.MakeCode;
                        var flags = keyboard.Flags;
                        //var isE0BitSet = ((flags & RI_KEY_E0) != 0);
                        var isBreakBitSet = ((flags & RI_KEY_BREAK) != 0);

                        OnKeyboard?.Invoke(this, new KeyboardEventArgs(new KeyEventInfo(isBreakBitSet ? KeyDirection.Up : KeyDirection.Down, keyboard)));

                        break;
                    }
                case RawInputDeviceType.HID:
                    {
                        var hid = input.Data.HID;
                        Log(LogLevel.Verbose, $"Input: hid={hid}");
                        //...
                        break;
                    }
            }
        }

        private RAWINPUTDEVICE rawInputDeviceKeyboard = new()
        {
            UsagePage = HIDUsagePage.Generic,
            Usage = HIDUsage.Keyboard,
            Flags = RawInputDeviceFlags.InputSink,
        };

        private RAWINPUTDEVICE rawInputDeviceMouse = new()
        {
            UsagePage = HIDUsagePage.Generic,
            Usage = HIDUsage.Mouse,
            Flags = RawInputDeviceFlags.InputSink,
        };

        private void RegisterRawInputKeyboard(IntPtr handle)
        {
            RegisterRawInputDevices(handle, [rawInputDeviceKeyboard]);
        }

        private void RegisterRawInputMouse(IntPtr handle)
        {
            RegisterRawInputDevices(handle, [rawInputDeviceMouse]);
        }

        private static string ToString(RAWINPUTDEVICE[] rawInputDevices)
        {
            return rawInputDevices == null ? "null" : $"[{string.Join(", ", rawInputDevices.Select(r => r.ToString()))}]";
        }

        private void RegisterRawInputDevices(IntPtr handle, RAWINPUTDEVICE[] rawInputDevices)
        {
            Log(LogLevel.Verbose, $"RegisterRawInputDevices(0x{handle:X8}, {ToString(rawInputDevices)})");
            for (int i = 0; i < rawInputDevices.Length; i++)
            {
                rawInputDevices[i].WindowHandle = handle;
            }
            if (!RegisterRawInputDevices(rawInputDevices, rawInputDevices.Length, Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new ApplicationException("Failed to register raw input device(s).");
            }
            //Log(LogLevel.Verbose, $"RegisterRawInputDevices: Registered raw input device(s)");
            //Log(LogLevel.Verbose, $"RegisterRawInputDevices: OnKeyboard={OnKeyboard}");
            //Log(LogLevel.Verbose, $"RegisterRawInputDevices: OnMouseButton={OnMouseButton}");
            //Log(LogLevel.Verbose, $"RegisterRawInputDevices: OnMouseMove={OnMouseMove}");
        }

        private void UnregisterRawInputKeyboard(IntPtr handle)
        {
            UnregisterRawInputDevices(handle, [rawInputDeviceKeyboard]);
        }

        private void UnregisterRawInputMouse(IntPtr handle)
        {
            UnregisterRawInputDevices(handle, [rawInputDeviceMouse]);
        }

        private void UnregisterRawInputDevices(IntPtr handle, RAWINPUTDEVICE[] rawInputDevices)
        {
            Log(LogLevel.Verbose, $"UnregisterRawInputDevices(0x{handle:X8}, {ToString(rawInputDevices)})");
            for (int i = 0; i < rawInputDevices.Length; i++)
            {
                rawInputDevices[i].Flags = RawInputDeviceFlags.Remove;
                rawInputDevices[i].WindowHandle = handle;
            }
            RegisterRawInputDevices(rawInputDevices, rawInputDevices.Length, Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        #region Native

        private enum HIDUsagePage : ushort
        {
            /// <summary>Generic desktop controls.</summary>
            Generic = 0x01
        }

        private enum HIDUsage : ushort
        {
            Mouse = 0x02,
            Keyboard = 0x06,
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice#members
        /// </summary>
        [Flags]
        #pragma warning disable IDE0079
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:Enums values should not be duplicated", Justification = "<Pending>")]
        #pragma warning restore IDE0079
        private enum RawInputDeviceFlags
        {
            /// <summary>
            /// No flags.
            /// </summary>
            None = 0,
            /// <summary>
            /// If set, this removes the top level collection from the inclusion list. This tells the operating system to stop reading from a device which matches the top level collection.
            /// </summary>
            Remove = 0x00000001,
            /// <summary>
            /// If set, this specifies the top level collections to exclude when reading a complete usage page. This flag only affects a TLC whose usage page is already specified with PageOnly.
            /// </summary>
            Exclude = 0x00000010,
            /// <summary>
            /// If set, this specifies all devices whose top level collection is from the specified usUsagePage. Note that Usage must be zero. To exclude a particular top level collection, use Exclude.
            /// </summary>
            PageOnly = 0x00000020,
            /// <summary>
            /// If set, this prevents any devices specified by UsagePage or Usage from generating legacy messages. This is only for the mouse and keyboard.
            /// </summary>
            NoLegacy = 0x00000030,
            /// <summary>
            /// If set, this enables the caller to receive the input even when the caller is not in the foreground. Note that WindowHandle must be specified.
            /// </summary>
            InputSink = 0x00000100,
            /// <summary>
            /// If set, the mouse button click does not activate the other window. CaptureMouse can be specified only if NoLegacy is specified for a mouse device.
            /// </summary>
            CaptureMouse = 0x00000200,
            /// <summary>
            /// If set, the application-defined keyboard device hotkeys are not handled. However, the system hotkeys; for example, ALT+TAB and CTRL+ALT+DEL, are still handled. By default, all keyboard hotkeys are handled. NoHotKeys can be specified even if NoLegacy is not specified and WindowHandle is NULL.
            /// </summary>
            NoHotKeys = 0x00000200,
            /// <summary>
            /// If set, application keys are handled.  NoLegacy must be specified.  Keyboard only.
            /// </summary>
            AppKeys = 0x00000400,
            /// <summary>
            /// If set, this enables the caller to receive input in the background only if the foreground application does not process it. In other words, if the foreground application is not registered for raw input, then the background application that is registered will receive the input.
            /// </summary>
            ExInputSink = 0x00001000,
            /// <summary>
            /// If set, this enables the caller to receive WM_INPUT_DEVICE_CHANGE notifications for device arrival and device removal.
            /// </summary>
            DevNotify = 0x00002000
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice
        /// </summary>
        private struct RAWINPUTDEVICE
        {
            /// <summary>Top level collection Usage page for the raw input device.</summary>
            public HIDUsagePage UsagePage;
            /// <summary>Top level collection Usage for the raw input device. </summary>
            public HIDUsage Usage;
            /// <summary>Mode flag that specifies how to interpret the information provided by UsagePage and Usage.</summary>
            public RawInputDeviceFlags Flags;
            /// <summary>Handle to the target device. If NULL, it follows the keyboard focus.</summary>
            public IntPtr WindowHandle;

            public override readonly string ToString()
            {
                return $"{{UsagePage={UsagePage}, Usage={Usage}, Flags={Flags}, WindowHandle=0x{WindowHandle:X8}}}";
            }
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);

        private enum RawInputCommand : uint
        {
            /// <summary>
            /// Get input data.
            /// </summary>
            Input = 0x10000003,
            /// <summary>
            /// Get header data.
            /// </summary>
            Header = 0x10000005
        }

        private enum RawInputDeviceType : uint
        {
            MOUSE = 0,
            KEYBOARD = 1,
            HID = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public RawInputDeviceType Type;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [Flags]
        public enum RawMouseFlags : ushort
        {
            /// <summary>Mouse movement data is relative to the last mouse position.</summary>
            MoveRelative = 0,
            /// <summary>Mouse movement data is based on absolute position.</summary>
            MoveAbsolute = 1,
            /// <summary>Mouse coordinates are mapped to the virtual desktop (for a multiple monitor system).</summary>
            VirtualDesktop = 2,
            /// <summary>Mouse attributes changed; application needs to query the mouse attributes.</summary>
            AttributesChanged = 4,
            /// <summary>This mouse movement event was not coalesced. Mouse movement events can be coalesced by default.</summary>
            MoveNoCoalesce = 8,
        }

        [Flags]
        public enum RawMouseButtonFlags : ushort
        {
            /// <summary>Left button down.</summary>
            LeftButtonDown = 0x0001,
            /// <summary>Left button up.</summary>
            LeftButtonUp = 0x0002,
            /// <summary>Right button down.</summary>
            RightButtonDown = 0x0004,
            /// <summary>Right button up.</summary>
            RightButtonUp = 0x0008,
            /// <summary>Middle button down.</summary>
            MiddleButtonDown = 0x0010,
            /// <summary>Middle button up.</summary>
            MiddleButtonUp = 0x0020,
            /// <summary>Button 4 down.</summary>
            Button4Down = 0x0040,
            /// <summary>Button 4 up.</summary>
            Button4Up = 0x0080,
            /// <summary>Button 5 down.</summary>
            Button5Down = 0x0100,
            /// <summary>Button 5 up.</summary>
            Button5Up = 0x0200,
            /// <summary>Mouse wheel moved.</summary>
            MouseWheelVertical = 0x0400,
            /// <summary>Mouse horizontal wheel moved.</summary>
            MouseWheelHorizontal = 0x0800,
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse#members
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse#remarks
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)]
            public RawMouseFlags Flags;
            /// <summary>
            /// Reserved.
            /// </summary>
            [FieldOffset(4)]
            public uint ulButtons;
            /// <summary>
            /// The transition state of the mouse buttons.
            /// </summary>
            [FieldOffset(4)]
            public RawMouseButtonFlags ButtonFlags;
            /// <summary>
            /// If usButtonFlags has RI_MOUSE_WHEEL or RI_MOUSE_HWHEEL, this member specifies the distance the wheel is rotated.
            /// </summary>
            [FieldOffset(6)]
            public ushort usButtonData;
            /// <summary>
            /// The raw state of the mouse buttons. The Win32 subsystem does not use this member.
            /// </summary>
            [FieldOffset(8)]
            public uint ulRawButtons;
            /// <summary>
            /// The motion in the X direction. This is signed relative motion or absolute motion, depending on the value of usFlags.
            /// </summary>
            [FieldOffset(12)]
            public int lLastX;
            /// <summary>
            /// The motion in the Y direction. This is signed relative motion or absolute motion, depending on the value of usFlags.
            /// </summary>
            [FieldOffset(16)]
            public int lLastY;
            /// <summary>
            /// Additional device-specific information for the event. See https://learn.microsoft.com/en-us/windows/win32/tablet/system-events-and-mouse-messages#distinguishing-pen-input-from-mouse-and-touch for more info.
            /// </summary>
            [FieldOffset(20)]
            public uint ulExtraInformation;

            public override readonly string ToString()
            {
                return $"{{Flags={Flags}" +
                    $", ulButtons=0x{ulButtons:X}" +
                    $", ButtonFlags={ButtonFlags}" +
                    $", usButtonData=0x{usButtonData:X4}" +
                    $", ulRawButtons=0x{ulRawButtons:X}" +
                    $", lLastX={lLastX}, lLastY={lLastY}" +
                    $", ulExtraInformation=0x{ulExtraInformation:X}}}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public readonly ushort Reserved;
            public ushort VKey;
            public uint Message;
            public ulong ExtraInformation;

            public override readonly bool Equals([NotNullWhen(true)] object obj)
            {
                return obj is RAWKEYBOARD keyboard &&
                       MakeCode == keyboard.MakeCode &&
                       Flags == keyboard.Flags &&
                       Reserved == keyboard.Reserved &&
                       VKey == keyboard.VKey &&
                       Message == keyboard.Message &&
                       ExtraInformation == keyboard.ExtraInformation;
            }

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(MakeCode, Flags, Reserved, VKey, Message, ExtraInformation);
            }

            public override readonly string ToString()
            {
                return $"{{MakeCode=0x{MakeCode:X4}" +
                    $", Flags=0x{Flags:X4}" +
                    $", Reserved=0x{Reserved:X4}" +
                    $", VKey=0x{VKey:X4}" +
                    $", Message=0x{Message:X8}" +
                    $", ExtraInformation=0x{ExtraInformation:X16}}}";
            }

            public static bool operator ==(RAWKEYBOARD left, RAWKEYBOARD right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(RAWKEYBOARD left, RAWKEYBOARD right)
            {
                return !(left == right);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWHID
        {
            public uint dwSizHid;
            public uint dwCount;
            public byte bRawData;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RawData
        {
            /// <summary>
            /// Mouse raw input data.
            /// </summary>
            [FieldOffset(0)]
            public RAWMOUSE Mouse;
            /// <summary>
            /// Keyboard raw input data.
            /// </summary>
            [FieldOffset(0)]
            public RAWKEYBOARD Keyboard;
            /// <summary>
            /// HID raw input data.
            /// </summary>
            [FieldOffset(0)]
            public RAWHID HID;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            /// <summary>
            /// Header for the data.
            /// </summary>
            public RAWINPUTHEADER Header;
            public RawData Data;
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata
        /// </summary>
        /// <param name="hRawInput"></param>
        /// <param name="uiCommand"></param>
        /// <param name="pData"></param>
        /// <param name="pcbSize"></param>
        /// <param name="cbSizeHeader"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, RawInputCommand uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata
        /// </summary>
        /// <param name="hRawInput"></param>
        /// <param name="uiCommand"></param>
        /// <param name="pData"></param>
        /// <param name="pcbSize"></param>
        /// <param name="cbSizeHeader"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, RawInputCommand uiCommand, out RAWINPUT pData, ref uint pcbSize, uint cbSizeHeader);

        /// <summary>
        /// TODO: Find a way to get this to work. :/
        /// 
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputbuffer
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pcbSize"></param>
        /// <param name="cbSizeHeader"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern uint GetRawInputBuffer([Optional] IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        #endregion Native
    }
}
