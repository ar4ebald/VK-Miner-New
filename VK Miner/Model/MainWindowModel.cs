using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using VK_Miner.Annotations;
using VK_Miner.VK;

namespace VK_Miner.Model
{
    class MainWindowModel : INotifyPropertyChanged
    {
        private string _userName;
        private string _photoUrl;
        private UserModel _selectedUser;
        private HintItemViewModel[] _hints;
        private VisualizationItemModel[] _visualizationItems;

        public string UserName
        {
            get
            {
                return _userName;
            }
            set
            {
                _userName = value;
                OnPropertyChanged();
            }
        }
        public string PhotoUrl
        {
            get
            {
                return _photoUrl;
            }
            set
            {
                _photoUrl = value;
                OnPropertyChanged();
            }
        }
        public UserModel SelectedUser
        {
            get { return _selectedUser; }
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedUserVisibility));
            }
        }
        public Visibility SelectedUserVisibility
        {
            get { return _selectedUser == null ? Visibility.Collapsed : Visibility.Visible; }
        }
        public HintItemViewModel[] Hints
        {
            get { return _hints; }
            set
            {
                _hints = value;
                OnPropertyChanged();
            }
        }
        public VisualizationItemModel[] VisualizationItems
        {
            get { return _visualizationItems; }
            set
            {
                _visualizationItems = value;
                OnPropertyChanged();
            }
        }


        private const string DefaultJson = "{\r\n  \"id\": 107920824,\r\n  \"first_name\": \"Кристина\",\r\n  \"last_name\": \"Константинова\",\r\n  \"domain\": \"kristinka_profile\",\r\n  \"bdate\": \"28.7\",\r\n  \"city\": {\r\n    \"id\": 1,\r\n    \"title\": \"Москва\"\r\n  },\r\n  \"photo_max\": \"https://pp.vk.me/c627628/v627628824/4fde4/VHheKNnEKyE.jpg\",\r\n  \"photo_max_orig\": \"https://pp.vk.me/c627628/v627628824/4fde3/Z1F-hI1vm1k.jpg\",\r\n  \"occupation\": {\r\n    \"type\": \"university\",\r\n    \"id\": 128,\r\n    \"name\": \"НИУ ВШЭ (ГУ-ВШЭ)\"\r\n  },\r\n  \"online\": 0\r\n}";
        private const string HintJson = "{ 'type': 'profile', 'description': 'УГАТУ (ИФ), Уфа', 'section': 'friends', 'id': 3236045, 'name': 'Артур Булатов', 'domain': 'id3236045', 'photo_50': 'https://pp.vk.me/c622523/v622523045/28aad/0iujhhYDisM.jpg' }";

        public static MainWindowModel DesignInstance
        {
            get
            {
                return new MainWindowModel()
                {
                    _userName = "Артур Булатов",
                    _photoUrl = "https://pp.vk.me/c624916/v624916229/365cb/9hLRQnlJRZI.jpg",
                    _selectedUser = new UserModel(new VK.Model.User(JObject.Parse(DefaultJson))),
                    _hints = new[] { new HintItemViewModel(JObject.Parse(HintJson)) },
                    _visualizationItems = new[] {new VisualizationItemModel("МГУ", Brushes.Red, 25), new VisualizationItemModel("НИУ ВШЭ", Brushes.Yellow, 12),  }
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class VisualizationItemModel
        {
            public string Name { get; set; }
            public SolidColorBrush Brush { get; set; }
            public int Count { get; set; }

            public VisualizationItemModel(string name, SolidColorBrush brush, int count)
            {
                Name = name;
                Brush = brush;
                Count = count;
            }
        }
    }
}
