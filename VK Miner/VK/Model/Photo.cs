using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class Photo : VkObject
    {
        public long Id { get; set; }
        public int AlbumId { get; set; }
        public int OwnerId { get; set; }
        public PhotoSize[] Sizes { get; set; }
        public string Text { get; set; }
        [VkConverter(nameof(DateTimeConverter))]
        public DateTime Date { get; set; }

        private static DateTime DateTimeConverter(JToken json)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(json.Value<long>());
        }

        private static readonly InitializerDelegate<Photo> Initializer = CreateInitializer<Photo>();
        public Photo() { }
        public Photo(JToken json) { Initializer(this, json); }
    }
}
