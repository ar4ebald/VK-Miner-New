using System.Windows;
using System.Windows.Input;

namespace VK_Miner
{
    /// <summary>
    /// Interaction logic for CaptchaWindow.xaml
    /// </summary>
    public partial class CaptchaWindow : Window
    {
        private readonly string _url;

        public string CaptchaKey;

        public CaptchaWindow(string url)
        {
            this._url = url;
            InitializeComponent();
        }

        private void CaptchaWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            CaptchaImage.DataContext = _url;
            InputBox.Focus();
        }

        private void Confirm(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            CaptchaKey = InputBox.Text;
            Close();
        }

        private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm(sender, null);
            }
        }
    }
}
