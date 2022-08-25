using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NvkCommon
{
    public static class Extensions
    {
        public static void Deconstruct<T>(this T[] list, out T first, out IList<T> rest)
        {
            first = list.Length > 0 ? list[0] : default;
            rest = list.Skip(1).ToList();
        }

        public static void Deconstruct<T>(this T[] list, out T first, out T second, out IList<T> rest)
        {
            first = list.Length > 0 ? list[0] : default;
            second = list.Length > 1 ? list[1] : default;
            rest = list.Skip(2).ToList();
        }
    }
}
