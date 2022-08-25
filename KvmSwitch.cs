using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NvkCommon
{
    public abstract class KvmSwitch
    {
        public string Port { get; set; }

        public abstract Task SetOutputs(params int[] outputs);
    }

    /// <summary>
    /// Rextron QSBM-3214 True 4K HDMI 2.0 KVM Matrix Switch with Seamless Switching
    /// https://www.rextron.com/product-True-4K-HDMI-2-0-KVM-Matrix-Switch-with-Seamless-Switching-QSBM-3214.html
    ///
    /// From the printed manual in the box:
    /// | "V=1-4" | Active-Video Select PC 1-4 |
    /// | "V=<"   | Active-Video Select The Previous Active PC |
    /// | "V=>"   | Active-Video Select The Next Active PC     |
    /// | "S=1-4" | Sub-Video Select PC 1-4 |
    /// | "U=1-4" | USB 3.2 Select PC 1-4 |
    /// | "U*"    | USB 3.2 Follows Video Output 1 |
    /// | "U=$"   | USB 3.2 Independent Switching (Non-Follow) |
    /// | "A=1-4" | Analog Audio Select PC 1-4 |
    /// | "A=*"   | Analog Audio Follows Video Output 1 |
    /// | "A=$"   | Analog Audio Independent Switching (Non-Follow) |
    /// | "H=R"   | Reset* |
    ///
    /// So, for NightVsKnight:
    ///  KNIGHT (port 1) PRIMARY, NIGHT (port 2) SECONDARY: "V=1", "S=2"
    ///  NIGHT (port 2) PRIMARY, KNIGHT (port 1) SECONDARD: "V=2", "S=1"
    ///
    /// Response:
    /// ">" == Success
    /// "<" or nothing == Failure
    ///
    /// After much debugging I discovered that the KVM is EXCEPTIONALLY finicky about receiving
    /// **more than one character at a time!** :/
    /// Seriously, sending it "S=2\r" does not work, but sending it "S", "=", "2", "\r" does, even if I send them fast!
    /// So, the below code is a bit of a mess to work around that, until I can figure out a better way to do it.
    ///
    /// I also have not found any way yet to get the RS232-over-IP to control the KVM. :/
    /// </summary>
    public class KvmSwitchRextronQsbm3214 : KvmSwitch
    {
        private static readonly string TAG = Log.TAG(typeof(KvmSwitchRextronQsbm3214));

        public override async Task SetOutputs(params int[] outputs)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"SetOutputs({string.Join(", ", outputs)})");

            var outputPrimary = outputs[0];
            var outputSecondary = outputs[1];

            // `V=#` causes the KVM to switch away from the current PC
            // which will cause the COM port to disappear/close, so we have to send it last.
            var command = $"S={outputSecondary}\rV={outputPrimary}\r";

            try
            {
                var ipEndPoint = IPEndPoint.Parse(Port);
                await Utils.TcpWrite(ipEndPoint, command, byteByByteWriteDelayMillis: 20, newLine: "\r");
                // TODO: Should or do we need to **read** the response to know if it was successful?
                return;
            }
            catch (Exception)//e)
            {
                //Log.PrintLine(TAG, Log.LogLevel.Error, $"SetOutputs: Exception: {e}");
            }

            await Utils.SerialWrite(Port, command, byteByByteWriteDelayMillis: 20, newLine: "\r");
            // TODO: Should or do we need to **read** the response to know if it was successful?
        }
    }

    //
    // TODO: For historical/reference reasons, add class for the old KVM Switch that we no longer use:
    // IOGEAR GHMS8422 2x2 HDMI® Video Matrix Switch with Cinema 4K and RS-232
    // https://www.iogear.com/product/GHMS8422/
    // output:
    //  "o01 i01" == output 1 is connected to input 1
    //  "o01 i02" == output 1 is connected to input 2
    //  "o02 i01" == output 2 is connected to input 1
    //  "o02 i02" == output 2 is connected to input 2
    // commands:
    // "EDID port1" - as port 1 mode
    // "EDID remix" - as remix mode
    // "EDID default" - as default mode
    //
    //...
}
