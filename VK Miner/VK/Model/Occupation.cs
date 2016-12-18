using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class Occupation : VkObject
    {
        public string Type { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }

        private static readonly InitializerDelegate<Occupation> Initializer = CreateInitializer<Occupation>();
        public Occupation(JToken json) { Initializer(this, json); }
        public Occupation() { }
    }
}