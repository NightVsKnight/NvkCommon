using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NvkCommon
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public static class SendInputInjector
    {
        private static readonly string TAG = Log.TAG(typeof(SendInputInjector));

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;       // Virtual Key Code
            public ushort wScan;     // Hardware Scan Code
            public uint dwFlags;     // Flags specifying the action (e.g., key up/down)
            public uint time;        // Timestamp for the event (0 = system-generated)
            public long dwExtraInfo; // Additional info
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            /// <summary>
            /// The absolute position of the mouse, or the amount of motion since the last mouse event was
            /// generated, depending on the value of the dwFlags member. Absolute data is specified as the
            /// x coordinate of the mouse; relative data is specified as the number of pixels moved.
            /// </summary>
            public int dx;
            /// <summary>
            /// The absolute position of the mouse, or the amount of motion since the last mouse event was
            /// generated, depending on the value of the dwFlags member. Absolute data is specified as the
            /// y coordinate of the mouse; relative data is specified as the number of pixels moved.
            /// </summary>
            public int dy;
            /// <summary>
            /// If dwFlags contains MOUSEEVENTF_WHEEL, then mouseData specifies the amount of wheel movement.
            /// A positive value indicates that the wheel was rotated forward, away from the user; a negative
            /// value indicates that the wheel was rotated backward, toward the user. One wheel click is defined
            /// as WHEEL_DELTA, which is 120.
            /// Windows Vista: If dwFlags contains MOUSEEVENTF_HWHEEL, then dwData specifies the amount of wheel
            /// movement.A positive value indicates that the wheel was rotated to the right; a negative value
            /// indicates that the wheel was rotated to the left.One wheel click is defined as WHEEL_DELTA,
            /// which is 120.
            /// If dwFlags does not contain MOUSEEVENTF_WHEEL, MOUSEEVENTF_XDOWN, or MOUSEEVENTF_XUP, then
            /// mouseData should be zero.
            /// If dwFlags contains MOUSEEVENTF_XDOWN or MOUSEEVENTF_XUP, then mouseData specifies which X
            /// buttons were pressed or released. This value may be any combination of the following flags:
            /// * XBUTTON1 0x0001 Set if the first X button is pressed or released.
            /// * XBUTTON2 0x0002 Set if the second X button is pressed or released.
            /// </summary>
            public uint mouseData;
            /// <summary>
            /// A set of bit flags that specify various aspects of mouse motion and button clicks. The bits in this
            /// member can be any reasonable combination of the following values.
            /// The bit flags that specify mouse button status are set to indicate changes in status, not ongoing
            /// conditions.For example, if the left mouse button is pressed and held down, MOUSEEVENTF_LEFTDOWN is
            /// set when the left button is first pressed, but not for subsequent motions.
            /// Similarly MOUSEEVENTF_LEFTUP is set only when the button is first released.
            /// You cannot specify both the MOUSEEVENTF_WHEEL flag and either MOUSEEVENTF_XDOWN or MOUSEEVENTF_XUP
            /// flags simultaneously in the dwFlags parameter, because they both require use of the mouseData field.
            /// </summary>
            public uint dwFlags;
            /// <summary>
            /// The time stamp for the event, in milliseconds. If this parameter is 0, the system will provide its own time stamp.
            /// </summary>
            public uint time;
            /// <summary>
            /// An additional value associated with the mouse event. An application calls GetMessageExtraInfo to obtain this extra information.
            /// </summary>
            public long dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            /// <summary>
            /// The message generated by the input hardware.
            /// </summary>
            private uint uMsg;
            /// <summary>
            /// The low-order word of the lParam parameter for uMsg.
            /// </summary>
            private ushort wParamL;
            /// <summary>
            /// The high-order word of the lParam parameter for uMsg.
            /// </summary>
            private ushort wParamH;
        }

        /// <summary>
        /// The event is a mouse event. Use the mi structure of the union.
        /// </summary>
        private const uint INPUT_MOUSE = 0;
        /// <summary>
        /// The event is a keyboard event. Use the ki structure of the union.
        /// </summary>
        private const uint INPUT_KEYBOARD = 1;
        /// <summary>
        /// The event is a hardware event. Use the hi structure of the union.
        /// </summary>
        private const uint INPUT_HARDWARE = 2;

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        /// </summary>
        [Flags]
        private enum MOUSEEVENTF : uint
        {
            /// <summary>
            /// Movement occurred.
            /// </summary>
            MOVE = 0x0001,
            /// <summary>
            /// The left button was pressed.
            /// </summary>
            LEFTDOWN = 0x0002,
            /// <summary>
            /// The left button was released.
            /// </summary>
            LEFTUP = 0x0004,
            /// <summary>
            /// The right button was pressed.
            /// </summary>
            RIGHTDOWN = 0x0008,
            /// <summary>
            /// The right button was released.
            /// </summary>
            RIGHTUP = 0x0010,
            /// <summary>
            /// The middle button was pressed.
            /// </summary>
            MIDDLEDOWN = 0x0020,
            /// <summary>
            /// The middle button was released.
            /// </summary>
            MIDDLEUP = 0x0040,
            /// <summary>
            /// An X button was pressed.
            /// </summary>
            XDOWN = 0x0080,
            /// <summary>
            /// An X button was released.
            /// </summary>
            XUP = 0x0100,
            /// <summary>
            /// The wheel was moved, if the mouse has a wheel. The amount of movement is specified in mouseData.
            /// </summary>
            WHEEL = 0x0800,
            /// <summary>
            /// The wheel was moved horizontally, if the mouse has a wheel. The amount of movement is specified in mouseData.
            /// </summary>
            HWHEEL = 0x01000,
            /// <summary>
            /// The WM_MOUSEMOVE messages will not be coalesced. The default behavior is to coalesce WM_MOUSEMOVE messages.
            /// </summary>
            MOVE_NOCOALESCE = 0x2000,
            /// <summary>
            /// Maps coordinates to the entire desktop. Must be used with MOUSEEVENTF_ABSOLUTE.
            /// </summary>
            VIRTUALDESK = 0x4000,
            /// <summary>
            /// The dx and dy parameters contain normalized absolute coordinates. If not set,
            /// those parameters contain relative data: the change in position since the last
            /// reported position. This flag can be set, or not set, regardless of what kind of
            /// mouse or mouse-like device, if any, is connected to the system. For further
            /// information about relative mouse motion, see the following Remarks section.
            /// </summary>
            ABSOLUTE = 0x8000,
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput
        /// </summary>
        [Flags]
        private enum KEYEVENTF : uint
        {
            /// <summary>
            /// If specified, the wScan scan code consists of a sequence of two bytes, where the
            /// first byte has a value of 0xE0. See Extended-Key Flag for more info.
            /// </summary>
            EXTENDEDKEY = 0x0001,
            /// <summary>
            /// If specified, the key is being released. If not specified, the key is being pressed.
            /// </summary>
            KEYUP = 0x0002,
            /// <summary>
            /// If specified, the system synthesizes a VK_PACKET keystroke. The wVk parameter
            /// must be zero. This flag can only be combined with the KEYEVENTF_KEYUP flag.
            /// </summary>
            UNICODE = 0x0004,
            /// <summary>
            /// If specified, wScan identifies the key and wVk is ignored.
            /// </summary>
            SCANCODE = 0x0008,
        }

        public static void InjectKeyDown(Keys vkCode)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki = new()
            {
                wVk = (ushort)vkCode,
                wScan = 0,
                dwFlags = 0, // Key down
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectKeyUp(Keys vkCode)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki = new()
            {
                wVk = (ushort)vkCode,
                wScan = 0,
                dwFlags = (uint)KEYEVENTF.KEYUP, // Key up
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseMoveAbsolute(int x, int y)
        {
            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"InjectMouseMoveAbsolute({x}, {y})");
            var bounds = Screen.FromPoint(new Point(x, y)).Bounds;
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dx = (x * 65535) / bounds.Width,  // Convert to absolute coordinates
                dy = (y * 65535) / bounds.Height, // Convert to absolute coordinates
                dwFlags = (uint)(MOUSEEVENTF.MOVE | MOUSEEVENTF.ABSOLUTE),
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseMoveRelative(int x, int y)
        {
            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"InjectMouseMoveRelative({x}, {y})");
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dx = x,
                dy = y,
                dwFlags = (uint)MOUSEEVENTF.MOVE,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseLeftDown()
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.LEFTDOWN,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseLeftUp()
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.LEFTUP,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseRightDown()
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.RIGHTDOWN,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseRightUp()
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.RIGHTUP,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseMiddleDown()
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.MIDDLEDOWN,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseMiddleUp() {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                dwFlags = (uint)MOUSEEVENTF.MIDDLEUP,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseWheelVertical(short scrollAmount)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new()
            {
                mouseData = (uint)scrollAmount,
                dwFlags = (uint)MOUSEEVENTF.WHEEL,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void InjectMouseWheelHorizontal(short scrollAmount)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi = new MOUSEINPUT
            {
                mouseData = (uint)scrollAmount,
                dwFlags = (uint)MOUSEEVENTF.HWHEEL,
            };

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
