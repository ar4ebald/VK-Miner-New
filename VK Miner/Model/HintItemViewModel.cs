using Newtonsoft.Json.Linq;

namespace VK_Miner.Model
{
    class HintItemViewModel
    {
        public string Type { get; private set; }
        public string Description { get; private set; }
        public int Section { get; private set; }
        public long Id { get; private set; }
        public string Name { get; private set; }
        public string Domain { get; private set; }
        public string Photo50 { get; private set; }

        public HintItemViewModel() { }
        public HintItemViewModel(JToken json)
        {
            Type = json["type"].Value<string>();
            Description = json["description"].Value<string>();
            Id = json["id"].Value<long>();
            Name = json["name"].Value<string>();
            Domain = "https://vk.com/" + json["domain"].Value<string>();
            Photo50 = json["photo_50"].Value<string>();

            switch (json["section"].Value<string>())
            {
                case "friends":
                    Section = 0;
                    break;
                case "idols":
                    Section = 1;
                    break;
                case "publics":
                    Section = 2;
                    break;
                case "groups":
                    Section = 3;
                    break;
                case "events":
                    Section = 4;
                    break;
                case "correspondents":
                    Section = 5;
                    break;
                case "mutual_friends":
                    Section = 6;
                    break;
            }
        }

        public override string ToString() => Name;
    }
}
