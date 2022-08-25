using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;

namespace NvkCommon
{
    public abstract class UsbLedKeyboard
    {
        public abstract string Filter { get; }

        public abstract bool SetColor(string portName, Color color);
    }

    /// <summary>
    /// https://github.com/Dygmalab/Bazecor/blob/development/FOCUS_API.md
    /// 
    /// Neat: https://dygma.com/blogs/product-development/an-explanation-of-the-pcbs-of-the-raise-2
    /// </summary>
    public class UsbLedKeyboardDygmaRaise : UsbLedKeyboard
    {
        private static readonly string TAG = Log.TAG(typeof(UsbLedKeyboardDygmaRaise));

        public const string FILTER = "vid_1209&pid_2201";

        public override string Filter => FILTER;

        public override bool SetColor(string portName, Color color)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"+SetColor(portName={Utils.Quote(portName)}, color={color})");
            try
            {
                if (!String.IsNullOrWhiteSpace(portName))
                {
                    try
                    {
                        using (var sp = new SerialPort(portName, 9600))
                        {
                            sp.Open();

                            // https://github.com/Dygmalab/Bazecor/blob/development/FOCUS_API.md#ledsetall
                            var text = String.Format("led.setAll {0} {1} {2}", color.R, color.G, color.B);
                            Log.PrintLine(TAG, Log.LogLevel.Information, $"SetColor: text={Utils.Quote(text)}");

                            sp.WriteLine(text);
                        }
                        return true;
                    }
                    catch (IOException e)
                    {
                        Log.PrintLine(TAG, Log.LogLevel.Error, $"SetColor: IOException: {Utils.Quote(e.Message)}");
                    }
                }
            }
            finally
            {
                Log.PrintLine(TAG, Log.LogLevel.Verbose, $"-SetColor(portName={Utils.Quote(portName)}, color={color})");
            }
            return false;
        }
    }
}
