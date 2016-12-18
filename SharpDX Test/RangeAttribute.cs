using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDX_Test
{
    class RangeAttribute : Attribute
    {
        public float Min { get; set; }
        public float Max { get; set; }

        public RangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}
