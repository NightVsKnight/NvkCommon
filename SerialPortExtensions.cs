using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NvkCommon
{
    public static class SerialPortExtensions
    {
        private static readonly string TAG = Log.TAG(typeof(SerialPortExtensions));

        public static async Task<string> ReadLineAsync(this SerialPort serialPort)
        {
            return await ReadLineAsync(serialPort, CancellationToken.None);
        }

        public static async Task<string> ReadLineAsync(this SerialPort serialPort, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var buffer = new byte[1];
            string response;
            while (true)
            {
                // TO-DO: should use buffer instead of 1 by 1
                await serialPort.BaseStream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken)
                    .ConfigureAwait(false);
                var character = serialPort.Encoding.GetString(buffer);
                sb.Append(character);
                var completed = StringBuilderEndsWith(sb, serialPort.NewLine);
                if (completed)
                {
                    response = sb.ToString();
                    response = response[..^serialPort.NewLine.Length];
                    break;
                }
            }
            return response;
        }

        public static async Task WriteLineAsync(this SerialPort serialPort, string str)
        {
            await WriteAsync(serialPort, str + serialPort.NewLine)
                .ConfigureAwait(false);
        }

        public static async Task WriteAsync(this SerialPort serialPort, char ch)
        {
            await WriteAsync(serialPort, ch.ToString())
                .ConfigureAwait(false);
        }

        public static async Task WriteAsync(this SerialPort serialPort, string str)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteLineAsync: str={Utils.Quote(str)}");
            await WriteAsync(serialPort, serialPort.Encoding.GetBytes(str))
                .ConfigureAwait(false);
        }

        public static async Task WriteAsync(this SerialPort serialPort, byte[] data)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteLineAsync: data={Utils.ToHexString(data, true)}");
            await serialPort.BaseStream.WriteAsync(data)
                    .ConfigureAwait(false);
            await serialPort.BaseStream.FlushAsync()
                    .ConfigureAwait(false);
        }

        public static async Task WriteAsync(this SerialPort serialPort, byte data)
        {
            Log.PrintLine(TAG, Log.LogLevel.Verbose, $"WriteLineAsync: data={Utils.ToHexString(data, 1)}");
            await serialPort.BaseStream.WriteAsync((new byte[] { data }).AsMemory(0, 1))
                    .ConfigureAwait(false);
            await serialPort.BaseStream.FlushAsync()
                    .ConfigureAwait(false);
        }

        public static async Task<string> RequestResponseAsync(this SerialPort serialPort, string str)
        {
            await WriteLineAsync(serialPort, str)
                    .ConfigureAwait(false);
            return await ReadLineAsync(serialPort)
                    .ConfigureAwait(false);
        }

        public static async Task<string> RequestResponseAsync(this SerialPort serialPort, string str, CancellationToken cancellationToken)
        {
            await WriteLineAsync(serialPort, str)
                    .ConfigureAwait(false);
            return await ReadLineAsync(serialPort, cancellationToken)
                    .ConfigureAwait(false);
        }

        private static bool StringBuilderEndsWith(StringBuilder sb, string str)
        {
            if (sb.Length < str.Length) return false;
            var end = sb.ToString(sb.Length - str.Length, str.Length);
            return end.Equals(str);
        }
    }
}
