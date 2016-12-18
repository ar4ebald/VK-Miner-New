using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using SharpDX;
using VK_Miner.Model;
using VK_Miner.VK;
using Color = System.Windows.Media.Color;

namespace VK_Miner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Api _api;
        private GraphService _graphService;
        private MainWindowModel _model;
        private SettingsWindow _settingsWindow;
        private User[] _users;

        private int _searchBlockChangeId;

        public MainWindow()
        {
            InitializeComponent();
            MouseTouchDevice.RegisterEvents(Surface);
            Mouse.AddMouseDownHandler(this, OnMouseDown);
        }

        public void ShowUserGraph(long userId)
        {
            _users = _api.Run(
                "friends.get",
                "user_id", userId,
                "order", "random",
                "fields", "bdate,photo_max,photo_max_orig,occupation,city,counters,domain,universities,schools")
                ["response"]["items"]
                .Cast<JObject>()
                .Select(i => new VK.Model.User(i))
                .Select((i, index) => new User()
                {
                    ArrayIndex = index,
                    Model = i
                })
                .ToArray();

            _graphService.InitializeNodes(_api, _users);

            var selectedIndex = VisualizationComboBox.SelectedIndex;
            VisualizationComboBox.SelectedIndex = -1;
            VisualizationComboBox.SelectedIndex = selectedIndex;

            DummyTextBox.Focus();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _graphService = new GraphService(Surface);
            _graphService.Run(Surface);

            _graphService.UserSelected += GraphServiceOnUserSelected;
            _graphService.UserNavigated += GraphServiceOnUserNavigated;

            var properties = typeof(GraphService).GetProperties()
                .Where(i => i.GetCustomAttribute<RangeAttribute>() != null)
                .ToArray();
            _settingsWindow = new SettingsWindow(properties, _graphService);

            Auth(false);
        }

        private void GraphServiceOnUserNavigated(VK.Model.User user)
        {
            ShowUserGraph(user.Id);
        }

        private void GraphServiceOnUserSelected(VK.Model.User user)
        {
            _model.SelectedUser = new UserModel(user);
        }

        private void Auth(bool relogin)
        {
            _api = Api.CacheOrCreate("VK Miner", 4989758, "friends,groups", revoke: relogin);
            if (_api == null)
            {
                Close();
                return;
            }

            var user = _api.Run("users.get", "fields", "photo_100")["response"][0];

            this.DataContext = _model = new MainWindowModel()
            {
                UserName = $"{user["first_name"]} {user["last_name"]}",
                PhotoUrl = user["photo_100"].ToString()
            };

            ShowUserGraph(_api.UserId);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Auth(true);
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            _settingsWindow.Show();
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            _settingsWindow?.Close();
            Application.Current.Shutdown();
        }

        private async void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var id = Interlocked.Increment(ref _searchBlockChangeId);

            var delay = _api.DelayUntilNextCall;
            if (delay > 0) await Task.Delay(delay);

            if (_searchBlockChangeId != id) return;

            var response = _api.Run("execute.getHints", "q", SearchBox.Text)["response"] as JArray;
            if (response == null) return;

            _model.Hints = response
                .Select(i => new HintItemViewModel(i))
                .OrderBy(i => i.Section)
                .ToArray();
        }

        private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            var count = SearchHintsListBox.Items.Count;
            if (count == 0) return;
            switch (e.Key)
            {
                case Key.Down:
                    SearchHintsListBox.SelectedIndex = (SearchHintsListBox.SelectedIndex + 1) % count;
                    break;
                case Key.Up:
                    if (SearchHintsListBox.SelectedIndex == -1)
                        SearchHintsListBox.SelectedIndex = count - 1;
                    else
                        SearchHintsListBox.SelectedIndex = (SearchHintsListBox.SelectedIndex - 1 + count) % count;
                    break;
                case Key.Enter:
                    var hint = SearchHintsListBox.SelectedItems.Cast<HintItemViewModel>().FirstOrDefault();
                    if (hint != null) ShowUserGraph(hint.Id);
                    break;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs)
        {
            if (!SearchBox.IsFocused || ReferenceEquals(Mouse.DirectlyOver, SearchBox))
                return;

            DummyTextBox.Focus();
        }

        private void HintItem_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var id = ((HintItemViewModel)((FrameworkElement)sender).DataContext).Id;
            ShowUserGraph(id);
        }

        private void Visualizer_OnSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_graphService == null || e.AddedItems.Count == 0)
                return;

            Func<VK.Model.User, IEnumerable<Tuple<long, string>>> infoExtractor = null;

            switch ((string)((ComboBoxItem)e.AddedItems[0]).Content)
            {
                case "По городу":
                    infoExtractor = user => user.City == null ? new Tuple<long, string>[0] : new[] { Tuple.Create(user.City.Id, user.City.Title) };
                    break;
                case "По университету":
                    infoExtractor = user => user.Universities.Select(i => Tuple.Create(i.Id, i.Name));
                    break;
                case "По школе":
                    infoExtractor = user => user.Schools.Select(i => Tuple.Create(i.Id, i.Name));
                    break;
                default:
                    _model.VisualizationItems = null;
                    _graphService.UpdateColors(false);
                    return;
            }

            var brushes = new SolidColorBrush[]
            {
                Brushes.Red,
                Brushes.Yellow,
                Brushes.Orange,
                Brushes.Green,
                Brushes.Aqua,
                Brushes.Fuchsia,
                Brushes.LightPink,
                Brushes.Purple,
                Brushes.Lime,
                Brushes.Brown,
            };

            var distribution = _users.SelectMany(i => infoExtractor(i.Model))
                .GroupBy(i => i.Item1)
                .OrderByDescending(i => i.Count())
                .Take(10)
                .Select((i, v) => new
                {
                    Id = i.Key,
                    Model = new MainWindowModel.VisualizationItemModel(i.First().Item2, brushes[v], i.Count())
                })
                .ToDictionary(i => i.Id, i => i.Model);

            _model.VisualizationItems = distribution.Values.OrderByDescending(i => i.Count).ToArray();

            for (var i = 0; i < _users.Length; i++)
            {
                int maxCount = int.MinValue;
                Color maxColor = default(Color);
                foreach (var item in infoExtractor(_users[i].Model))
                {
                    MainWindowModel.VisualizationItemModel model;
                    if (distribution.TryGetValue(item.Item1, out model) && model.Count > maxCount)
                    {
                        maxCount = model.Count;
                        maxColor = model.Brush.Color;
                    }
                }

                _users[i].Color = maxCount == int.MinValue
                    ? Color3.Black
                    : new Color3(maxColor.ScR, maxColor.ScG, maxColor.ScB);
            }

            _graphService.UpdateColors(true);
        }

        private void DrawLinesToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_graphService != null)
                _graphService.DrawEdges = true;
        }

        private void DrawLinesToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_graphService != null)
                _graphService.DrawEdges = false;
        }
    }
}
