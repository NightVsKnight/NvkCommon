using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NvkCommon
{
    public static class MemoryStreamExtensions
    {
        public static void WriteUshort(this MemoryStream memoryStream, ushort value)
        {
            memoryStream.WriteByte((byte)(value & 0xFF));
            memoryStream.WriteByte((byte)(value >> 8));
        }
    }
}
