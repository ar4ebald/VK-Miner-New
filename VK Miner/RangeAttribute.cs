using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VK_Miner
{
    class RangeAttribute : Attribute
    {
        public float Min { get; set; }
        public float Max { get; set; }
        public string Name { get; set; }

        public RangeAttribute(float min, float max, string name)
        {
            Min = min;
            Max = max;
            Name = name;
        }
    }
}
