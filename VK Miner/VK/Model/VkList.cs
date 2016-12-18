using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class VkList<T> : VkObject where T : VkObject
    {
        public int Count { get; set; }
        public T[] Items { get; set; }

        private static readonly InitializerDelegate<VkList<T>> Initializer = CreateInitializer<VkList<T>>();
        public VkList(JToken json) { Initializer(this, json); }
        public VkList() { } 
    }
}
