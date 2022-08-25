using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

#pragma warning disable IDE0130
namespace NvkCommon
#pragma warning restore IDE0130
{
    public static class Utils
    {
        private static readonly string TAG = Log.TAG(typeof(Utils));

#if DEBUG
        public const bool IS_DEBUG = true;
#else
        public const bool IS_DEBUG = false;
#endif

        /// <summary>
        /// This is mostly used with FAKE_MACHINE_NAME to test/see how themes look side-by-side.
        /// </summary>
        public const bool ALLOW_MULTIPLE_INSTANCES = false;

        /// <summary>
        /// This is mostly used with ALLOW_MULTIPLE_INSTANCES to test/see how themes look side-by-side.
        /// </summary>
        public const bool FAKE_MACHINE_NAME = false;

        public const string MACHINE_NAME_SWWWOOBY = "SWWWOOBY";
        public const string MACHINE_NAME_NIGHT55 = "NIGHT55";
        public const string MACHINE_IP_NIGHT55 = "10.0.0.51"; // I could implement lookup...but why bother? :)
        public const string MACHINE_NAME_KNIGHT55 = "KNIGHT55";
        public const string MACHINE_IP_KNIGHT55 = "10.0.0.52"; // I could implement lookup...but why bother? :)

        public static string MachineName
        {
            get
            {
#if DEBUG
                if (FAKE_MACHINE_NAME)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    // For debugging purposes only:
                    //return MACHINE_NAME_SWWWOOBY;
                    //return MACHINE_NAME_NIGHT55;
                    return MACHINE_NAME_KNIGHT55;
                    //return "UNKNOWN";
#pragma warning restore CS0162 // Unreachable code detected
                }
                else
                {
                    return Environment.MachineName;
                }
#else
                return Environment.MachineName;
#endif
            }
        }

        public static string ServerIpAddress
        {
            get
            {
                return MachineName switch
                {
                    MACHINE_NAME_NIGHT55 => MACHINE_IP_KNIGHT55,
                    MACHINE_NAME_KNIGHT55 => MACHINE_IP_NIGHT55,
                    _ => String.Empty,
                };
            }
        }

        public static bool IsSwwwooby
        {
            get
            {
                return MachineName == MACHINE_NAME_SWWWOOBY;
            }
        }

        public const string ManualSwitchPrimerDefault = @"\\?\usb#vid_046d&pid_085c#7f1799df#{a5dcbf10-6530-11d2-901f-00c04fb951ed}";

        public static string PrimaryAutoSwitchDefault
        {
            get
            {
                // NOTE: I think these can change after OS re-installs
                return MachineName switch
                {
                    MACHINE_NAME_NIGHT55 => @"\\?\hid#vid_0557&pid_2405&mi_01#7&3b2da764&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                    MACHINE_NAME_KNIGHT55 => @"\\?\hid#vid_0557&pid_2405&mi_01#7&2a6abc53&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                    _ => null,
                };
            }
        }

        public static string SecondaryAutoSwitchDefault
        {
            get
            {
                // NOTE: I think these can change after OS re-installs
                return MachineName switch
                {
                    MACHINE_NAME_NIGHT55 => @"\\?\hid#vid_0557&pid_2405&mi_01#7&60d021c&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                    MACHINE_NAME_KNIGHT55 => @"\\?\hid#vid_0557&pid_2405&mi_01#7&4c24ac6&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                    _ => null,
                };
            }
        }

        // NOTE: I think these can change after OS re-installs
        public const string PrimaryLedKeyboardDefault = @"\\?\usb#vid_046d&pid_085c#7f1799df#{a5dcbf10-6530-11d2-901f-00c04fb951ed}";
        // NOTE: I think these can change after OS re-installs
        public const string SecondaryLedKeyboardDefault = @"\\?\usb#vid_046d&pid_085c#7f1799df#{a5dcbf10-6530-11d2-901f-00c04fb951ed}";

        public static Color LedKeyboardColor
        {
            get
            {
                return MachineName switch
                {
                    MACHINE_NAME_NIGHT55 => Color.Red,
                    MACHINE_NAME_KNIGHT55 => Color.White,
                    _ => Color.Magenta,
                };
            }
        }

        public const bool FAKE_NETWORK_DEPLOYED = false;

        public static bool IsNetworkDeployed
        {
            get
            {
                var isNetworkDeployed = FAKE_NETWORK_DEPLOYED;
                isNetworkDeployed |= ApplicationDeployment.IsNetworkDeployed;
                return isNetworkDeployed;
            }
        }
        public static bool IsDevelopmentDeployed
        {
            get
            {
                var isDevelopmentDeployed = !IsNetworkDeployed;
                return isDevelopmentDeployed;
            }
        }

        public static string CurrentVersion
        {
            get
            {
                string applicationVersion;
                if (IsNetworkDeployed)
                {
                    applicationVersion = ApplicationDeployment.CurrentVersion?.ToString();
                    if (applicationVersion == null)
                    {
                        applicationVersion = "FAKE_NETWORK_DEPLOYED_VERSION";
                    }
                    else
                    {
                        applicationVersion += " (Official)";
                    }
                }
                else
                {
                    var assembly = Assembly.GetEntryAssembly();
                    var assemblyName = assembly.GetName();
                    applicationVersion = assemblyName.Version.ToString();
                    applicationVersion += " (DEVELOPMENT)";
                }
                return $"{applicationVersion}";
            }
        }

        private static readonly string REG_PATH_APPLICATION_STARTUP = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        public static readonly string REG_PATH_APPLICATION_STARTUP_ROOT = $@"{Registry.CurrentUser.Name}\{REG_PATH_APPLICATION_STARTUP}";
        public static string APPLICATION_STARTUP_NAME
        {
            get
            {
                var applicationExecutableName = ApplicationExecutableName;
                if (!IsNetworkDeployed)
                {
                    // Avoid conflict with any ClickOnce network deployed version
                    applicationExecutableName += "-Debug";
                }
                return applicationExecutableName;
            }
        }
        public static string REG_PATH_APPLICATION_STARTUP_FULL
        {
            get
            {
                return $@"{REG_PATH_APPLICATION_STARTUP_ROOT}\{APPLICATION_STARTUP_NAME}";
            }
        }

        public static string ApplicationStartupCommand
        {
            get
            {
                string command;
                if (IsNetworkDeployed)
                {
                    var assembly = Assembly.GetEntryAssembly();
                    var assemblyName = assembly.GetName().Name;

                    // Example: C:\Users\pv\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\NvK-Agent\NvK-Agent.appref-ms
                    command = $@"{Environment.GetFolderPath(Environment.SpecialFolder.Programs)}\{assemblyName}\{assemblyName}.appref-ms";
                }
                else
                {
                    command = ApplicationExecutablePath;
                }
                return command;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="command">Appropriately quoted string of command and parameters. Set to null to delete.</param>
        public static void RunAfterLogin(string name, string command = null)
        {
            var regkey = name;
            var rk = Registry.CurrentUser.OpenSubKey(REG_PATH_APPLICATION_STARTUP, true);
            if (command != null)
            {
                rk?.SetValue(regkey, command);
            }
            else
            {
                rk?.DeleteValue(regkey, false);
            }
        }

        public static void RunApplicationExecutableAfterLogin(bool value, string parameters = null)
        {
            //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RunApplicationExecutableAfterLogin: value={value}, parameters={Quote(parameters)}");
            var name = APPLICATION_STARTUP_NAME;
            string command = null;
            if (value)
            {
                command = Quote(ApplicationStartupCommand);
                Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RunApplicationExecutableAfterLogin: command={command}");
                if (!String.IsNullOrWhiteSpace(parameters))
                {
                    command += " " + parameters;
                }
            }
            RunAfterLogin(name, command);
        }

        public static void RegJump(string keyFull)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RegJump: keyFull={keyFull}");
            var keyPath = keyFull;
            string keyName = null;
            if (!keyFull.EndsWith('\\'))
            {
                var indexValue = keyFull.LastIndexOf('\\');
                keyPath = keyFull[..indexValue];
                keyName = keyFull[(indexValue + 1)..];
            }
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RegJump: keyPath={keyPath}, keyName={keyName}");

            var rk = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Applets\\Regedit", true);
            rk?.SetValue("LastKey", keyPath);

            var regeditProcess = ProcessStart("regedit", elevated: true);
            try
            {
                if (regeditProcess?.WaitForInputIdle(10000) == true)
                {
                    // NOTE: This should be effectively a no-op if the running app is not elevated.
                    RegJumpHelper.RegJumpUsingUIAutomation(keyFull);
                }
            }
            catch
            {
                // ignore
            }
        }

        #region VoiceMeeter

        public static string VOICEMEETER_EXECUTABLE_NAME
        {
            get
            {
                return MachineName switch
                {
                    MACHINE_NAME_KNIGHT55 or MACHINE_NAME_NIGHT55 or MACHINE_NAME_SWWWOOBY => "voicemeeter8x64",
                    _ => "voicemeeterpro",
                };
            }
        }

        public static string VOICEMEETER_EXECUTABLE_PATH
        {
            get
            {
                return $@"{GetProgramFilesX86}\VB\Voicemeeter\{VOICEMEETER_EXECUTABLE_NAME}.exe";
            }
        }

        public static void VoiceMeeterRestartAudioEngine()
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, "Restarting VoiceMeeter Audio Engine");
            ProcessStart(VOICEMEETER_EXECUTABLE_PATH, "-R");
        }

        #endregion VoiceMeeter

        #region StarCitizen

        public const int STAR_CITIZEN_INACTIVITY_TIMEOUT_SECONDS = 15 * 60; // 15 minutes
        public const int STAR_CITIZEN_TRADE_UPDATE_INTERVAL_SECONDS = 7 * 60; // 7 minutes

        public const string STAR_CITIZEN_PROCESS_NAME = "StarCitizen";
        /// <summary>
        /// This can actually vary; I have seen as low as 260.
        /// </summary>
        public const int STAR_CITIZEN_PERSISTENT_UNIVERSE_INACTIVE_NOMINAL_PROCESS_THREAD_COUNT_THRESHOLD = 290;
        /// <summary>
        /// This can actually vary; I have seen as low as 280.
        /// </summary>
        public const int STAR_CITIZEN_PERSISTENT_UNIVERSE_ACTIVE_NOMINAL_PROCESS_THREAD_COUNT_THRESHOLD = 300;

        public static void StarCitizenProcessStop()
        {
            ProcessStop(STAR_CITIZEN_PROCESS_NAME);
        }

        public const string StarCitizenLauncherProcessName = "RSI Launcher";

        public static readonly string StarCitizenLauncherExecutablePath = $@"{GetProgramFilesX64}\Roberts Space Industries\{StarCitizenLauncherProcessName}\{StarCitizenLauncherProcessName}.exe";

        public static Process StarCitizenLauncherStart()
        {
            var processName = StarCitizenLauncherProcessName;
            if (IsProcessRunning(processName))
            {
                return null;
            }
            Log.PrintLine(TAG, Log.LogLevel.Information, "Starting Star Citizen Launcher");
            return ProcessStart(StarCitizenLauncherExecutablePath);
        }

        public static void StarCitizenLauncherStop()
        {
            ProcessStop(StarCitizenLauncherProcessName);
        }

        public static bool IsForegroundProcessStarCitizen
        {
            get => IsForegroundProcess(Utils.STAR_CITIZEN_PROCESS_NAME);
        }

        public static int StarCitizenProcessThreadCount
        {
            get => GetProcessThreadCount(STAR_CITIZEN_PROCESS_NAME);
        }

        /// <summary>
        /// See <see cref="STAR_CITIZEN_PERSISTENT_UNIVERSE_ACTIVE_NOMINAL_PROCESS_THREAD_COUNT_THRESHOLD"/>.
        /// </summary>
        /// <param name="threadCount"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool IsStarCitizenPersistentUniverseActive(int threadCount, int threshold)
        {
            return threadCount > threshold;
        }

        /// <summary>
        /// See <see cref="STAR_CITIZEN_PERSISTENT_UNIVERSE_ACTIVE_NOMINAL_PROCESS_THREAD_COUNT_THRESHOLD"/>.
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static bool IsStarCitizenPersistentUniverseActive(int threshold)
        {
            return IsStarCitizenPersistentUniverseActive(StarCitizenProcessThreadCount, threshold);
        }

        #endregion StarCitizen

        #region OBS

        // TODO:(pv) Put OBS_HOST_URL and OBS_HOST_PASSWORD in Settings...
        public static readonly string OBS_HOST_URL = $"ws://{MACHINE_NAME_SWWWOOBY}:4444";
        public const string OBS_HOST_PASSWORD = "qwigybo";

        private const string OBS_PROCESS_NAME = "obs64";
        private static readonly string OBS_EXECUTABLE_PATH = $@"{GetProgramFilesX64}\obs-studio\bin\64bit\{OBS_PROCESS_NAME}.exe";

        //
        // https://obsproject.com/wiki/Launch-Parameters
        //

        public static Process StartOBS(bool ignoreIfAlreadyRunning = false)
        {
            if (ignoreIfAlreadyRunning)
            {
                var processes = GetProcessesByName(OBS_PROCESS_NAME);
                var process = (processes?.Length > 0) ? processes[0] : null;
                if (process != null)
                {
                    return process;
                }
            }
            return ProcessStart(OBS_EXECUTABLE_PATH, stopProcessName: OBS_PROCESS_NAME);
        }

        public static string StopOBS()
        {
            return ProcessStop(OBS_PROCESS_NAME);
        }

        #endregion OBS

        #region NGINX

        private const string NGINX_PROCESS_NAME = "nginx";
        private static readonly string NGINX_EXECUTABLE_PATH = $@"C:\Streaming\nginx 1.7.11.3 Gryphon\{NGINX_PROCESS_NAME}.exe";

        //
        // http://nginx.org/en/docs/switches.html
        //

        public static Process StartNGINX()
        {
            return ProcessStart(NGINX_EXECUTABLE_PATH, stopProcessName: NGINX_PROCESS_NAME);
        }

        #endregion NGINX

        #region STUNNEL

        private const string STUNNEL_PROCESS_NAME = "stunnel";
        private static readonly string STUNNEL_EXECUTABLE_PATH = $@"C:\Users\pv\AppData\Local\Programs\stunnel\bin\{STUNNEL_PROCESS_NAME}.exe";
        // "C:\Program Files (x86)\stunnel\bin\stunnel.exe"
        // "C:\Program Files (x86)\stunnel\bin\stunnel.exe" -start
        // "C:\Program Files (x86)\stunnel\bin\stunnel.exe" -install

        public static Process StartSTUNNEL()
        {
            return ProcessStart(STUNNEL_EXECUTABLE_PATH, stopProcessName: STUNNEL_PROCESS_NAME);
        }

        #endregion STUNNEL

        #region Chrome

        public static readonly string CHROME_EXECUTABLE_PATH = $@"{GetProgramFilesX86}\Google\Chrome\Application\chrome.exe";

        public static Process StartChrome(string url, Rectangle rect, string profileName)
        {
            url = HttpUtility.UrlEncode(url);
            var arguments = "--app=\"data:text/html,<html><body><script>";
            if (rect != Rectangle.Empty)
            {
                arguments += $"window.moveTo({rect.X}, {rect.Y});window.resizeTo({rect.Width}, {rect.Height});";
            }
            arguments += $"window.location='{url}';</script></body></html >\"";
            if (!String.IsNullOrEmpty(profileName))
            {
                arguments += $" --profile-directory=\"{profileName}\"";
            }
            return ProcessStart(CHROME_EXECUTABLE_PATH, arguments);
        }

        #endregion Chrome

        #region General

        public static string ToUTF8String(this byte[] buffer)
        {
            var value = Encoding.UTF8.GetString(buffer);
            #pragma warning disable IDE0057
            return value.Remove(value.IndexOf((char)0));
            #pragma warning restore IDE0057
        }

        public static string ToUTF16String(this byte[] buffer)
        {
            var value = Encoding.Unicode.GetString(buffer);
            #pragma warning disable IDE0057
            return value.Remove(value.IndexOf((char)0));
            #pragma warning restore IDE0057
        }

        public static string Quote(object value)
        {
            if (value == null) return "null";
            if (value is string) return $"\"{value}\"";
            if (value is StringBuilder) return $"\"{value}\"";
            return value.ToString();
        }

        public static string ToString(IEnumerable<Object> values)
        {
            var sb = new StringBuilder();
            if (values == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append('[');
                var it = values.GetEnumerator();
                if (it.MoveNext())
                {
                    while (true)
                    {
                        var value = it.Current;
                        if (value is string v)
                        {
                            sb.Append(Quote(v));
                        }
                        else
                        {
                            sb.Append(value);
                        }
                        if (!it.MoveNext())
                        {
                            break;
                        }
                        sb.Append(',');
                    }
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        public static readonly string[] LINEFEED_SEPARATOR = ["\r\n"];

        public static string LinesToCSV(string text)
        {
            var lines = text?.Replace("\r\r", "\r").Split(LINEFEED_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            return ToString(lines);
        }

        public static byte[] GetBytes(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static string ToHexString(byte[] bytes, bool asByteArray = true)
        {
            return ToHexString(bytes, 0, bytes.Length, asByteArray);
        }

        public static string ToHexString(byte[] bytes, int offset, int count, //
            bool asByteArray = true)
        {
            // TODO:(pv) Use BitConverter.ToString(bytes, offset, count);

            if (bytes == null)
            {
                return "null";
            }

            char[] hexChars =
            [
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
            ];

            var sb = new StringBuilder();
            if (asByteArray)
            {
                sb.Append('[');
                var end = offset + count;
                for (var i = offset; i < end; i++)
                {
                    if (i != offset)
                    {
                        sb.Append('-');
                    }
                    sb.Append(hexChars[(bytes[i] & 0x000000f0) >> 4]);
                    sb.Append(hexChars[(bytes[i] & 0x0000000f)]);
                }
                sb.Append(']');
            }
            else
            {
                for (var i = count - 1; i >= 0; i--)
                {
                    sb.Append(hexChars[(bytes[i] & 0x000000f0) >> 4]);
                    sb.Append(hexChars[(bytes[i] & 0x0000000f)]);
                }
            }
            return sb.ToString();
        }

        public static string ToHexString(byte value, int maxLength, bool asByteArray = false)
        {
            return ToHexString([value], 0, maxLength, asByteArray);
        }

        public static string ToHexString(char value, int maxLength, bool asByteArray = false)
        {
            return ToHexString(BitConverter.GetBytes(value), 0, maxLength, asByteArray);
        }

        public static string ToHexString(short value, int maxLength, bool asByteArray = false)
        {
            return ToHexString(BitConverter.GetBytes(value), 0, maxLength, asByteArray);
        }

        public static string ToHexString(int value, int maxLength, bool asByteArray = false)
        {
            return ToHexString(BitConverter.GetBytes(value), 0, maxLength, asByteArray);
        }

        public static string ToHexString(long value, int maxLength, bool asByteArray = false)
        {
            return ToHexString(BitConverter.GetBytes(value), 0, maxLength, asByteArray);
        }

        public static string ToHexString(String value, bool asByteArray = false)
        {
            return ToHexString(GetBytes(value), asByteArray);
        }

        public static string ToBitString(byte[] bytes, int maxBits, int spaceEvery)
        {
            var bits = new BitArray(bytes);
            maxBits = Math.Max(0, Math.Min(maxBits, bits.Length));
            var sb = new StringBuilder();
            for (var i = maxBits - 1; i >= 0; i--)
            {
                sb.Append((bits[i]) ? '1' : '0');
                if ((spaceEvery != 0) && (i > 0) && (i % spaceEvery == 0))
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }

        public static string ToBitString(byte value, int maxBits)
        {
            return ToBitString([value], maxBits, 0);
        }

        public static string ToBitString(short value, int maxBits, int spaceEvery = 8)
        {
            return ToBitString(BitConverter.GetBytes(value), maxBits, spaceEvery);
        }

        public static string ToBitString(int value, int maxBits, int spaceEvery = 8)
        {
            return ToBitString(BitConverter.GetBytes(value), maxBits, spaceEvery);
        }

        public static string ToBitString(long value, int maxBits, int spaceEvery = 8)
        {
            return ToBitString(BitConverter.GetBytes(value), maxBits, spaceEvery);
        }

        public static void MemSet<T>(T[] array, T value)
        {
            if (array == null) return;

            int block = 32, index = 0;
            var length = Math.Min(block, array.Length);

            // Fill the initial array
            while (index < length)
            {
                array[index++] = value;
            }

            length = array.Length;
            while (index < length)
            {
                Buffer.BlockCopy(array, 0, array, index, Math.Min(block, length - index));
                index += block;
                block *= 2;
            }
        }

        #endregion General

        #region Windows

        static RegistryKey GetBaseKeyFromKeyName(string registryPath)
        {
            string[] pathParts = registryPath.Split(['\\'], 2);
            var hiveName = pathParts[0];
            return hiveName.ToUpper() switch
            {
                "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKEY_USERS" => Registry.Users,
                "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
                _ => throw new ArgumentException("Invalid registry hive."),
            };
        }

        public static void SetWindowPosition(Form form, bool isMaximized, Point windowPosition, Size windowSize)
        {
            if (isMaximized)
            {
                form.WindowState = FormWindowState.Maximized;
            }
            else if (Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(windowPosition)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.DesktopLocation = windowPosition;
                form.Size = windowSize;
                form.WindowState = FormWindowState.Normal;
            }
        }

        public static void SetWindowPosition(Form form, bool isMaximized, Rectangle windowPosition)
        {
            if (isMaximized)
            {
                form.WindowState = FormWindowState.Maximized;
            }
            else if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowPosition)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.DesktopBounds = windowPosition;
                form.WindowState = FormWindowState.Normal;
            }
        }

        public static bool IsMouseRightClicked(MouseEventArgs e)
        {
            return e.Button == MouseButtons.Right;
        }

        /// <summary>
        /// Return value
        /// Type: SHORT
        /// The return value specifies the status of the specified virtual key, as follows:
        /// * If the high-order bit is 1, the key is down; otherwise, it is up.
        /// * If the low-order bit is 1, the key is toggled. A key, such as the CAPS LOCK key, is toggled if it is turned on. The key is off and untoggled if the low-order bit is 0. A toggle key's indicator light (if any) on the keyboard will be on when the key is toggled, and off when the key is untoggled.
        /// </summary>
        /// <param name="keyCode"></param>
        /// <returns></returns>
        [DllImport("user32.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern short GetKeyState(int keyCode);

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        #pragma warning disable IDE0079
        #pragma warning disable CA2101
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        #pragma warning restore CA2101
        #pragma warning restore IDE0079
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        /// <summary>
        /// Sets hook and assigns its ID for tracking
        /// </summary>
        /// <param name="idHook">WH_* Hook ID</param>
        /// <param name="proc">Internal callback function</param>
        /// <returns>Hook ID</returns>
        internal static IntPtr SetHook(int idHook, HookProc proc)
        {
            return SetWindowsHookEx(idHook, proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        }

        #endregion Windows

        #region Processes

        public static string ApplicationExecutablePath
        {
            get
            {
                return Application.ExecutablePath;
            }
        }

        public static string ApplicationExecutableName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(ApplicationExecutablePath);
            }
        }

        public static bool IsAnyCpu(Assembly assembly = null)
        {
            if (assembly == null)
            {
                assembly = Assembly.GetEntryAssembly();
            }
            assembly.ManifestModule.GetPEKind(out PortableExecutableKinds portableExecutableKinds, out _);
            return portableExecutableKinds == PortableExecutableKinds.ILOnly;
        }

        public static bool Is64BitProcess
        {
            get
            {
                return IntPtr.Size == 8;
            }
        }

        public static bool Is32BitProcess
        {
            get
            {
                return IntPtr.Size == 4;
            }
        }

        public static string GetProgramFilesX64
        {
            get
            {
                string path;
                if (Is32BitProcess)
                {
                    path = GetProgramFilesX86.Replace(" (x86)", String.Empty);
                }
                else
                {
                    var specialFolder = Environment.SpecialFolder.ProgramFiles;
                    path = Environment.GetFolderPath(specialFolder);

                }
                return path;
            }
        }

        public static string GetProgramFilesX86
        {
            get
            {
                var specialFolder = Environment.SpecialFolder.ProgramFilesX86;
                return Environment.GetFolderPath(specialFolder);
            }
        }

        public static Process[] GetProcessesByName(string processName)
        {
            var extension = Path.GetExtension(processName);
            if (!String.IsNullOrEmpty(extension))
            {
                processName = processName.Replace(extension, String.Empty);
            }
            return Process.GetProcessesByName(processName);
        }

        public static bool IsProcessRunning(string processName)
        {
            return GetProcessesByName(processName).Length > 0;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        public static bool IsForegroundProcess(string processName)
        {
            processName = processName.ToLower();
            try
            {
                var hwnd = GetForegroundWindow();
                //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"IsForegroundProcess(processName={Utils.Quote(processName)}): hwnd={hwnd}");
                if (hwnd != nint.Zero)
                {
                    var dwCreationThread = GetWindowThreadProcessId(hwnd, out uint pid);
                    //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"IsForegroundProcess: hwnd={hwnd}, dwCreationThread={dwCreationThread}, pid={pid}");
                    if (dwCreationThread != nint.Zero)
                    {
                        var foregroundProcessName = Process.GetProcessById((int)pid).ProcessName.ToLower();
                        //Log.PrintLine(TAG, Log.LogLevel.Verbose, $"IsForegroundProcess: pid={pid}, processName={Utils.Quote(foregroundProcessName)}");
                        return string.Equals(processName, foregroundProcessName);
                    }
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        public static void ForegroundProcessWindow(string processName, bool force = false)
        {
            try
            {
                var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process != null && process.MainWindowHandle != nint.Zero)
                {
                    if (force || GetForegroundWindow() != process.MainWindowHandle)
                    {
                        SetForegroundWindow(process.MainWindowHandle);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        public static int GetProcessThreadCount(string processName)
        {
            var count = 0;
            using var p = Process.GetProcessesByName(processName).FirstOrDefault();
            if (p != null)
            {
                count = p.Threads.Count;
            }
            return count;
        }


        public static Process ProcessStart(string processPath, string arguments = null, string stopProcessName = null, bool elevated = false)
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, $"ProcessStart(processPath={Quote(processPath)}, arguments={Quote(arguments)}, stopProcessName={Quote(stopProcessName)}, elevated={elevated})");

            // TODO:(pv) Start as a new top process, not as a child process (test if this even makes sense by closing app and seeing if started processes keep running and how they show up in TaskManager)
            if (!string.IsNullOrEmpty(stopProcessName))
            {
                ProcessStop(stopProcessName);
            }

            processPath = Environment.ExpandEnvironmentVariables(processPath);

            //var workingDirectory = new FileInfo(processPath).Directory.FullName;
            //var workingDirectory = new FileInfo(processPath).DirectoryName;
            var workingDirectory = Path.GetDirectoryName(processPath);

            var processStartInfo = new ProcessStartInfo(processPath, arguments)
            {
                WorkingDirectory = workingDirectory
            };

            if (elevated)
            {
                processStartInfo.Verb = "runas";
                processStartInfo.UseShellExecute = true;
            }

            try
            {
                return Process.Start(processStartInfo);
            }
            catch (Win32Exception)
            {
                return null;
            }
        }

        public static string ProcessStop(string processName)
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, $"ProcessStop({Quote(processName)})");
            if (string.IsNullOrEmpty(processName))
            {
                return null;
            }

            string processFilePath = null;
            var processes = GetProcessesByName(processName);
            foreach (var process in processes)
            {
                processFilePath = ProcessStop(process);
            }
            return processFilePath;
        }

        public static string ProcessStop(Process process)
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, $"ProcessStop({process})");
            if (process == null)
            {
                return null;
            }

            string processFilePath = null;

            try
            {
                processFilePath = process.MainModule.FileName;

                var millisecondsTimeout = 128;
                while (!process.HasExited && millisecondsTimeout < 4096)
                {
                    if (process.MainWindowHandle == IntPtr.Zero || !process.CloseMainWindow())
                    {
                        process.Kill();
                    }
                    Thread.Sleep(millisecondsTimeout);
                    millisecondsTimeout *= 2;
                }

                Log.PrintLine(TAG, Log.LogLevel.Information, $"ProcessStop: Stopped all {Quote(processFilePath)}");
            }
            catch (Exception e)
            {
                Log.PrintLine(TAG, Log.LogLevel.Information, $"ProcessStop Exception", e);
            }

            return processFilePath;
        }

        #endregion Processes

        #region Json

        public class Response
        {
            public string result;

            public Response()
            {
            }

            public Response(string result)
            {
                this.result = result;
            }

            public override string ToString()
            {
                return "{ result:" + Quote(result) + " }";
            }
        }

        // TODO:(pv) Consider using NewtonSoft Json instead of System.Text.Json

        public static T TryJsonDeserialize<T>(string data)
        {
            return TryJsonDeserialize<T>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public static T TryJsonDeserialize<T>(string data, JsonSerializerOptions options)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(data, options);
            }
            catch// (Exception ex)
            {
                //Log.PrintLine(TAG, Log.LogLevel.Warning, $"TryJsonDeserialize: Exception {ex}");
                return default;
            }
        }

        public static string TryJsonSerialize<T>(T data)
        {
            return TryJsonSerialize<T>(data, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public static string TryJsonSerialize<T>(T data, JsonSerializerOptions options)
        {
            try
            {
                return JsonSerializer.Serialize<T>(data, options);
            }
            catch (Exception ex)
            {
                //Log.PrintLine(TAG, Log.LogLevel.Warning, $"TryJsonSerialize: Exception {ex}");
                return $"{{\"result\":\"{ex}\"}}";
            }
        }

        #endregion Json

        #region Serial-Over-IP

        /// <summary>
        /// Ethernet RS232 control HDMI switch
        /// https://www.usriot.com/products/rs232-to-ethernet-converter.html
        /// </summary>
        /// <param name="ipEndpoint"></param>
        /// <param name="outputs"></param>
        public static async Task TcpWrite(IPEndPoint ipEndpoint,
            string data,
            int byteByByteWriteDelayMillis = 0,
            #pragma warning disable IDE0060
            string newLine = "\n",
            #pragma warning restore IDE0060
            Encoding encoding = null,
            int writeTimeout = 1000)
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpWrite({ipEndpoint}, {Quote(data)}, ...)");
            using (var tcpClient = new TcpClient())
            {
                Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpWrite ConnectAsync {ipEndpoint}");
                try
                {
                    await tcpClient.ConnectAsync(ipEndpoint.Address, ipEndpoint.Port);
                }
                catch (SocketException e)
                {
                    Log.PrintLine(TAG, Log.LogLevel.Warning, $"TcpWrite ConnectAsync SocketException {e}");
                    return;
                }
                if (tcpClient.Connected)
                {
                    using var stream = tcpClient.GetStream();
                    stream.WriteTimeout = writeTimeout;

                    var bytes = (encoding ?? Encoding.ASCII).GetBytes(data);

                    try
                    {
                        if (byteByByteWriteDelayMillis > 0)
                        {
                            foreach (var b in bytes)
                            {
                                await stream.WriteAsync((new byte[] { b }).AsMemory(0, 1));
                                await Task.Delay(byteByByteWriteDelayMillis);
                            }
                        }
                        else
                        {
                            await stream.WriteAsync(bytes);
                        }
                    }
                    catch (IOException e)
                    {
                        // ignore...
                        Log.PrintLine(TAG, Log.LogLevel.Warning, $"TcpWrite IOException {e}");
                    }
                }
            }
            Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpWrite TcpClient closed");
        }

        public static async Task TcpRead(NetworkStream networkStream)
        {
            try
            {
                do
                {
                    var buffer = new byte[1024];
                    Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpRead +ReadAsync(...)");
                    var byteCount = await networkStream.ReadAsync(buffer);
                    Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpRead -ReadAsync(...); byteCount={byteCount}");
                    var response = Encoding.ASCII.GetString(buffer, 0, byteCount);
                    if (true)
                    {
                        // tiny cleanup to make debug output a tad bit prettier (albeit not 100% true)
                        response = Utils.LinesToCSV(response);
                    }
                    Log.PrintLine(TAG, Log.LogLevel.Information, $"TcpRead ReadAsync response={Quote(response)}");
                } while (networkStream.DataAvailable);
            }
            catch (IOException e)
            {
                // ignore...
                Log.PrintLine(TAG, Log.LogLevel.Warning, $"TcpRead IOException {e}");
            }
        }

        #endregion Serial-Over-IP

        #region Serial

        public static async Task SerialWrite(string comPort,
            string data,
            int byteByByteWriteDelayMillis = 0,
            string newLine = "\r",
            Encoding encoding = null,
            int baudRate = 19200, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One, Handshake handshake = Handshake.None,
            int readTimeout = 1000,
            int writeTimeout = 1000)
        {
            Log.PrintLine(TAG, Log.LogLevel.Information, $"SerialWrite({Quote(comPort)}, {Quote(data)})");

            using var serialPort = new SerialPort(comPort, baudRate, parity, dataBits, stopBits);
            serialPort.Handshake = handshake;
            serialPort.NewLine = newLine;
            serialPort.Encoding = encoding ?? Encoding.ASCII;
            serialPort.ReadTimeout = readTimeout;
            serialPort.WriteTimeout = writeTimeout;

            try
            {
                serialPort.Open();

                var bytes = serialPort.Encoding.GetBytes(data);
                Log.PrintLine(TAG, Log.LogLevel.Information, $"SerialWrite: serialPort.Write({Utils.ToHexString(bytes)})");
                if (byteByByteWriteDelayMillis > 0)
                {
                    foreach (var b in bytes)
                    {
                        await serialPort.WriteAsync(b);
                        await Task.Delay(byteByByteWriteDelayMillis);
                    }
                }
                else
                {
                    await serialPort.WriteAsync(bytes);
                }

                serialPort.Close();
            }
            catch (Exception e)
            {
                Log.PrintLine(TAG, Log.LogLevel.Warning, $"SerialWrite Exception {e}");
            }
        }

        #endregion Serial
    }
}
