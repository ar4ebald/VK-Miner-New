using System;

namespace VK_Miner.VK.Model
{
    [AttributeUsage(AttributeTargets.Property)]
    class VkConverterAttribute : Attribute
    {
        public string ConverterName { get; private set; }

        public VkConverterAttribute(string converterName)
        {
            ConverterName = converterName;
        }
    }
}
