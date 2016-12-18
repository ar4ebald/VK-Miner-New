using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class PhotoSize : VkObject
    {
        public string Src { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Type { get; set; }

        private static readonly InitializerDelegate<PhotoSize> Initializer = CreateInitializer<PhotoSize>();
        public PhotoSize() { }
        public PhotoSize(JToken json) { Initializer(this, json); }
    }
}