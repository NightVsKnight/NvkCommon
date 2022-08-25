using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static NvkCommon.Utils;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace NvkCommon
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// CONSIDER USING `RawInputCapture` INSTEAD OF THIS CLASS!
    /// 
    /// After I wrote this I also found:
    /// https://github.com/rvknth043/Global-Low-Level-Key-Board-And-Mouse-Hook/tree/master/GlobalLowLevelHooks
    /// Maybe that has more ideas...
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    //[SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "<Pending>")]
    public class LowLevelKeyboardHook : IDisposable
    {
        private static readonly string TAG = Log.TAG(typeof(LowLevelKeyboardHook));

        public bool IsInstalled { get { return hookID != IntPtr.Zero; } }

        /// <summary>
        /// Internal callback processing function
        /// </summary>
        private HookProc keyboardProc;

        /// <summary>
        /// Low level HookProc's ID
        /// </summary>
        private IntPtr hookID = IntPtr.Zero;

        public delegate void KeyboardHookEventHandler(KeysChangedEventArgs e);
        public event KeyboardHookEventHandler KeysChanged;

        public void Install()
        {
            if (IsInstalled) return;
            keyboardProc = LowLevelKeyboardProc;
            hookID = SetHook(WH_KEYBOARD_LL, keyboardProc);
        }

        public void Uninstall()
        {
            if (hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookID);
                hookID = IntPtr.Zero;
            }
        }

        ~LowLevelKeyboardHook()
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

        public enum KeyDirection
        {
            Down,
            Up
        }

        private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: nCode={nCode}");
            if (nCode >= 0)
            {
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: lParam={lParam}");
                var keyboardStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: keyboardStruct={keyboardStruct}");
                //var key = (Keys)keyboardStruct.vkCode;
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: key={key}");
                switch ((int)wParam)
                {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                        {
                            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: direction={KeyDirection.Down}");
                            OnKeysChanged(new KeyEventInfo(KeyDirection.Down, keyboardStruct));
                            break;
                        }
                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        {
                            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"LowLevelKeyboardProc: direction={KeyDirection.Up}");

                            // TODO: Emit only the first up?
                            // Example: Shift-Scroll
                            // Releasing either key will leave Shift-Scroll
                            // Releasing all keys keeps it Shift-Scroll
                            // Pressing a new key will reset

                            OnKeysChanged(new KeyEventInfo(KeyDirection.Up, keyboardStruct));
                            break;
                        }
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);

        }

        public void OnKeysChanged(KeyEventInfo keyEventInfo)
        {
            KeysChanged?.Invoke(new KeysChangedEventArgs(keyEventInfo));
        }

        public class KeyEventInfo(LowLevelKeyboardHook.KeyDirection keyDirection, LowLevelKeyboardHook.KBDLLHOOKSTRUCT keyInfo)
        {
            public KeyDirection KeyDirection = keyDirection;
            public KBDLLHOOKSTRUCT KeyInfo = keyInfo;

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

        public class KeysChangedEventArgs(LowLevelKeyboardHook.KeyEventInfo keyEventInfo) : EventArgs
        {
            public static bool IsCapsLockOn { get { return (GetKeyState((int)Keys.Capital) & 0x0001) == 0x0001; } }

            public KeyEventInfo KeyEventInfo { get; private set; } = keyEventInfo;

            public override string ToString()
            {
                return $"{{KeyEventInfo={KeyEventInfo}}}";
            }
        }

        #region Native methods

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;

            public override readonly string ToString()
            {
                return $"{{vkCode=0x{vkCode:X} ({vkCode})" +
                    //$", scanCode={scanCode}" +
                    $", flags=0x{flags:X} ({flags})" +
                    //$", time={time}" +
                    $", dwExtraInfo=0x{dwExtraInfo:X} ({dwExtraInfo})}}";
            }

            public override readonly bool Equals(/*[NotNullWhen(true)]*/ object obj)
            {
                return obj is KBDLLHOOKSTRUCT s &&
                       vkCode == s.vkCode &&
                       //scanCode == s.scanCode &&
                       flags == s.flags &&
                       //time == s.time &&
                       dwExtraInfo == s.dwExtraInfo;
            }

            public static bool operator ==(KBDLLHOOKSTRUCT left, KBDLLHOOKSTRUCT right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(KBDLLHOOKSTRUCT left, KBDLLHOOKSTRUCT right)
            {
                return !(left == right);
            }

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(vkCode, /*scanCode,*/ flags, /*time,*/ dwExtraInfo);
            }
        }

        #endregion
    }
}
