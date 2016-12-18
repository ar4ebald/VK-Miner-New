using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    public class User : VkObject
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Deactivated { get; set; }
        public bool Hidden { get; set; }

        public bool Online { get; set; }
        public Sex Sex { get; set; }

        public string Bdate
        {
            get { return _bdate; }
            set
            {
                _bdate = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var words = value.Split('.');

                    _birthdateDay = int.Parse(words[0]);
                    _birthdateMonth = int.Parse(words[1]);
                    var sb = new StringBuilder(_birthdateDay.ToString()).Append(' ').Append(MonthName[_birthdateMonth]);

                    if (words.Length == 3)
                    {
                        _birthdateYear = int.Parse(words[2]);
                        sb.Append(' ').Append(_birthdateYear);

                        var now = DateTime.Now;
                        Age = now.Year - _birthdateYear;
                        if (_birthdateMonth > now.Month || (_birthdateMonth == now.Month && _birthdateDay >= now.Day))
                            Age--;

                        if (Age > 0)
                        {
                            sb.Append(" (").Append(Age);
                            var mod = Age % 10;
                            if (mod == 1)
                                sb.Append(" год)");
                            else if (mod > 0 && mod < 5)
                                sb.Append(" года)");
                            else
                                sb.Append(" лет)");
                        }
                    }
                    else
                    {
                        _birthdateYear = 0;
                        Age = -1;
                    }

                    FormattedBirthdate = sb.ToString();
                }
                else
                {
                    _birthdateDay = _birthdateMonth = _birthdateYear = 0;
                    FormattedBirthdate = null;
                    Age = -1;
                }
            }
        }
        public City City { get; set; }
        public Counters Counters { get; set; }
        public string Domain { get; set; }
        public string Nickname { get; set; }

        public string UniversityName { get; set; }
        public University[] Universities { get; set; }
        public School[] Schools { get; set; }

        public string Photo100 { get; set; }
        public string PhotoMax { get; set; }
        public string PhotoMaxOrig { get; set; }

        public Occupation Occupation { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public int Age { get; private set; }

        public string FormattedBirthdate { get; private set; }

        private string _bdate;
        private int _birthdateDay = 0;
        private int _birthdateMonth = 0;
        private int _birthdateYear = 0;

        private static readonly Dictionary<int, string> MonthName = new Dictionary<int, string>
        {
            [1] = "января",
            [2] = "февраля",
            [3] = "марта",
            [4] = "апреля",
            [5] = "мая",
            [6] = "июня",
            [7] = "июля",
            [8] = "августа",
            [9] = "сентября",
            [10] = "октября",
            [11] = "ноября",
            [12] = "декабря"
        };

        private static readonly InitializerDelegate<User> Initializer = CreateInitializer<User>();
        public User(JToken json) { Initializer(this, json); }
        public User() { }
    }
}
