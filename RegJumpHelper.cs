using System.Windows.Forms;
using System.Windows.Automation;
using System.Diagnostics.CodeAnalysis;

namespace NvkCommon
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public class RegJumpHelper
    {
        private static readonly string TAG = Log.TAG(typeof(RegJumpHelper));

        /// <summary>
        /// </summary>
        /// <param name="keyFull">if ends with `\` then used directly, else uses path and name split at the last `\`</param>
        /// <returns></returns>
        public static bool RegJumpUsingUIAutomation(string keyFull)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RegJumpUsingUIAutomation: keyFull={keyFull}");
            string keyPath = keyFull;
            string keyName = null;
            if (!keyFull.EndsWith("\\"))
            {
                var indexValue = keyFull.LastIndexOf('\\');
                keyPath = keyFull.Substring(0, indexValue);
                keyName = keyFull.Substring(indexValue + 1);
            }
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"RegJumpUsingUIAutomation: keyPath={keyPath}, keyName={keyName}");

            // Find the RegEdit main window
            var regeditWindow = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "Registry Editor")
            );
            if (regeditWindow == null)
            {
                // Launch RegEdit if not found
                var regeditProcess = NvkCommon.Utils.ProcessStart("regedit.exe", elevated: true);
                if (regeditProcess?.WaitForInputIdle(2000) != true)
                {
                    return false;
                }

                regeditWindow = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.NameProperty, "Registry Editor")
                );
                if (regeditWindow == null)
                {
                    return false;
                }
            }

            // Find the address bar (Edit control)
            var addressBar = regeditWindow.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit"),
                    new PropertyCondition(AutomationElement.IsEnabledProperty, true),
                    new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true)
                )
            );
            if (addressBar == null)
            {
                return false;
            }

            // Set focus to the address bar
            addressBar.SetFocus();

            // Set the text
            var valuePattern = addressBar.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            valuePattern.SetValue(keyPath);

            // Press Enter
            SendKeys.SendWait("{ENTER}");

            var listView = regeditWindow.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "SysListView32"),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "2") // Id found using Windows SDK `inspect.exe`
                )
            );
            if (listView == null)
            {
                return false;
            }

            listView.SetFocus();

            var element = listView.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, keyName)
            );
            if (element == null)
            {
                return false;
            }

            element.SetFocus();
            return true;
        }
    }
}