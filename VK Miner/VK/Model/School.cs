using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class School : VkObject
    {
        public long Id { get; set; }
        public long Country { get; set; }
        public long City { get; set; }
        public string Name { get; set; }
        public long YearFrom { get; set; }
        public long YearTo { get; set; }
        public long YearGraduated { get; set; }
        public string Class { get; set; }
        public string Speciality { get; set; }
        public long Type { get; set; }
        public string TypeStr { get; set; }

        private static readonly InitializerDelegate<School> Initializer = CreateInitializer<School>();
        public School(JToken json) { Initializer(this, json); }
        public School() { }
    }


}