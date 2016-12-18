using System;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

namespace VK_Miner.VK
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly string _url;

        public string AccessToken { get; private set; }
        public long UserId { get; private set; }

        public LoginWindow(int clientId, string version, string scope, string display = "page", bool revoke = false)
        {
            InitializeComponent();

            _url = $"https://oauth.vk.com/authorize" +
                      $"?client_id={clientId}" +
                      $"&display={display}" +
                      $"&redirect_uri=https%3A%2F%2Foauth.vk.com%2Fblank.html" +
                      $"&scope={WebUtility.UrlEncode(scope)}" +
                      $"&response_type=token" +
                      $"&v={version}";

            if (revoke)
                _url += "&revoke=1";
        }

        private void LoginWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            WebBrowser.Navigate(_url);
        }

        private void WebBrowser_OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (e.Uri.OriginalString.StartsWith("https://oauth.vk.com/blank.html"))
            {
                try
                {
                    var response = e.Uri.Fragment.Substring(1)
                        .Split('&')
                        .Select(i => i.Split('='))
                        .ToDictionary(i => i[0], i => WebUtility.UrlDecode(i[1]));

                    AccessToken = response["access_token"];
                    UserId = Convert.ToInt64(response["user_id"]);

                    DialogResult = true;
                }
                catch (Exception)
                {
                    DialogResult = false;
                }

                Close();
            }
        }
    }
}
