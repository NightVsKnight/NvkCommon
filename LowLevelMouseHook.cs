using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static NvkCommon.Utils;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace NvkCommon
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// CONSIDER USING `RawInputCapture` INSTEAD OF THIS CLASS!
    /// 
    /// Original credit goes to:
    /// https://github.com/rvknth043/Global-Low-Level-Key-Board-And-Mouse-Hook/blob/master/GlobalLowLevelHooks/MouseHook.cs
    /// 
    /// Modified to:
    /// ...
    /// </summary>
    public class LowLevelMouseHook : IDisposable
    {
        public bool IsInstalled { get { return hookID != IntPtr.Zero; } }

        /// <summary>
        /// Internal callback processing function
        /// </summary>
        private HookProc mouseProc;

        /// <summary>
        /// Low level HookProc's ID
        /// </summary>
        private IntPtr hookID = IntPtr.Zero;

        /// <summary>
        /// Function to be called when defined even occurs
        /// </summary>
        /// <param name="mouseStruct">MSLLHOOKSTRUCT mouse structure</param>
        public delegate void MouseHookCallback(MSLLHOOKSTRUCT mouseStruct);

        #region Events
        public event MouseHookCallback MouseMove;
        public event MouseHookCallback LeftButtonDown;
        public event MouseHookCallback LeftButtonUp;
        public event MouseHookCallback LeftButtonDoubleClick;
        public event MouseHookCallback RightButtonDown;
        public event MouseHookCallback RightButtonUp;
        public event MouseHookCallback MiddleButtonDown;
        public event MouseHookCallback MiddleButtonUp;
        public event MouseHookCallback MouseWheel;
        #endregion

        /// <summary>
        /// Install low level mouse hook
        /// </summary>
        /// <param name="mouseHookCallbackFunc">Callback function</param>
        public void Install()
        {
            if (IsInstalled) return;
            mouseProc = LowLevelMouseProc;
            hookID = SetHook(WH_MOUSE_LL, mouseProc);
        }

        /// <summary>
        /// Remove low level mouse hook
        /// </summary>
        public void Uninstall()
        {
            if (hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookID);
                hookID = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Destructor. Unhook current hook
        /// </summary>
        ~LowLevelMouseHook()
        {
            Uninstall();
        }

        #region IDisposable Members

        /// <summary>
        /// Releases the keyboard hook.
        /// </summary>
        public void Dispose()
        {
            Uninstall();
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Callback function
        /// </summary>
        private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // parse system messages
            if (nCode >= 0)
            {
                var mouseStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                switch ((MouseMessages)wParam)
                {
                    case MouseMessages.WM_MOUSEMOVE:
                        MouseMove?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_LBUTTONDOWN:
                        LeftButtonDown?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_LBUTTONUP:
                        LeftButtonUp?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_LBUTTONDBLCLK:
                        LeftButtonDoubleClick?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_RBUTTONDOWN:
                        RightButtonDown?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_RBUTTONUP:
                        RightButtonUp?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_MBUTTONDOWN:
                        MiddleButtonDown?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_MBUTTONUP:
                        MiddleButtonUp?.Invoke(mouseStruct);
                        break;
                    case MouseMessages.WM_MOUSEWHEEL:
                        MouseWheel?.Invoke(mouseStruct);
                        break;
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        #region WinAPI

        private enum MouseMessages
        {
            WM_MOUSEMOVE = 0x0200,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_MBUTTONDBLCLK = 0x0209,
            WM_MOUSEWHEEL = 0x020A,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public override string ToString()
            {
                return $"{{x:{x}, y:{y}}}";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;

            public override string ToString()
            {
                return $"{{pt:{pt}, mouseData:{mouseData}, flags:0x{flags:X}, time:{time}, dwExtraInfo:0x{dwExtraInfo:X}}}";
            }
        }

        #endregion
    }
}
