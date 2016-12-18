using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VK_Miner.Annotations;
using VK_Miner.VK.Model;

namespace VK_Miner.Model
{
    class UserModel : INotifyPropertyChanged
    {
        public User User { get; }

        public List<Pair> MainInfo { get; }

        private int _friendsLoadingProgress;
        public int FriendsLoadingProgress
        {
            get { return _friendsLoadingProgress; }
            set
            {
                _friendsLoadingProgress = value;
                OnPropertyChanged();
            }
        }

        private bool _friendsAreLoading;
        public bool FriendsAreLoading
        {
            get { return _friendsAreLoading; }
            set
            {
                _friendsAreLoading = value;
                OnPropertyChanged();
            }
        }

        public UserModel(User user)
        {
            User = user;
            MainInfo = new List<Pair>();
            if (!string.IsNullOrWhiteSpace(user.FormattedBirthdate))
                MainInfo.Add(new Pair("День рождения:", user.FormattedBirthdate));
            if (user.City != null)
                MainInfo.Add(new Pair("Город:", user.City.Title));
            if (user.Occupation != null)
                MainInfo.Add(new Pair(user.Occupation.Type == "work" ? "Работает в:" : "Учится в:", user.Occupation.Name));

            FriendsAreLoading = true;
        }

        public class Pair
        {
            public object Key { get; set; }
            public object Value { get; set; }

            public Pair(object key, object value)
            {
                Key = key;
                Value = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
