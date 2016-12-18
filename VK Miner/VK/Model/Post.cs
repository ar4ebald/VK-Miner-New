using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class Post : VkObject
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int FromId { get; set; }
        public int Date { get; set; }
        public string Text { get; set; }

        public LikesClass Likes { get; set; }
        public RepostsClass Reposts { get; set; }

        private static readonly InitializerDelegate<Post> Initializer = CreateInitializer<Post>();
        public Post(JToken json) { Initializer(this, json); }
        public Post() { }

        public class LikesClass : VkObject
        {
            public int Count { get; set; }
            public bool UserLikes { get; set; }
            public bool CanLike { get; set; }
            public bool CanPublish { get; set; }

            private static InitializerDelegate<LikesClass> Initializer = CreateInitializer<LikesClass>();
            public LikesClass(JToken json) { Initializer(this, json); }
        }

        public class RepostsClass : VkObject
        {
            public int Count { get; set; }
            public bool UserReposted { get; set; }

            private static InitializerDelegate<RepostsClass> Initializer = CreateInitializer<RepostsClass>(); 
            public RepostsClass(JToken json) { Initializer(this, json); }
        }
 
    }
}
