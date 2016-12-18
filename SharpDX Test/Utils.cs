using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDX_Test
{
    static class Utils
    {
        public static int IndexOf<T>(this IEnumerable<T> source, T value)
        {
            var index = 0;
            foreach (var item in source)
            {
                if (value.Equals(item))
                    return index;
                index++;
            }
            return -1;
        }
    }
}
