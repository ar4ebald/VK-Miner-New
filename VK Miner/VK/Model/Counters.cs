﻿using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class Counters : VkObject
    {
        public int Albums { get; set; }
        public int Videos { get; set; }
        public int Audios { get; set; }
        public int Notes { get; set; }
        public int Photos { get; set; }
        public int Groups { get; set; }
        public int Gifts { get; set; }
        public int Friends { get; set; }
        public int OnlineFriends { get; set; }
        public int UserPhotos { get; set; }
        public int UserVideos { get; set; }
        public int Followers { get; set; }
        public int Subscriptions { get; set; }
        public int Pages { get; set; }

        private static readonly InitializerDelegate<Counters> Initializer = CreateInitializer<Counters>();
        public Counters(JToken json) { Initializer(this, json); }
        public Counters() { }
    }
}