using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class University : VkObject
    {
        public long Id { get; set; }
        public long Country { get; set; }
        public long City { get; set; }
        public string Name { get; set; }
        public long Faculty { get; set; }
        public string FacultyName { get; set; }
        public long Chair { get; set; }
        public string ChairName { get; set; }
        public long Graduation { set; get; }

        private static readonly InitializerDelegate<University> Initializer = CreateInitializer<University>();
        public University(JToken json) { Initializer(this, json); }
        public University() { }
    }
}