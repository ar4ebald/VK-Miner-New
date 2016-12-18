using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class City : VkObject
    {
        public long Id { get; set; }
        public string Title { get; set; }

        private static readonly InitializerDelegate<City> Initializer = CreateInitializer<City>();
        public City(JToken json) { Initializer(this, json); }
        public City() { }
    }
}