using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VK_Miner
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private GraphService _graph;
        private PropertyInfo[] _properties;
        private float[] _defaultValues;

        public SettingsWindow(PropertyInfo[] properties, GraphService graph)
        {
            InitializeComponent();

            _graph = graph;
            _properties = properties;
            _defaultValues = new float[properties.Length];

            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var range = property.GetCustomAttribute<RangeAttribute>();

                _defaultValues[i] = (float)property.GetValue(graph);

                RootPanel.Children.Add(new TextBlock()
                {
                    Text = range.Name
                });
                var slider = new Slider()
                {
                    Minimum = range.Min,
                    Maximum = range.Max,
                    Value = _defaultValues[i],
                    TickFrequency = (range.Max - range.Min) / 100,
                };
                slider.ValueChanged += (s, e) =>
                {
                    property.SetValue(_graph, (float)e.NewValue);
                };
                RootPanel.Children.Add(slider);
            }
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            for (var i = 0; i < _defaultValues.Length; i++)
                _properties[i].SetValue(_graph, _defaultValues[i]);
        }

        private void SortButton_OnClick(object sender, RoutedEventArgs e)
        {
            _graph.SortNodes();
        }

        private void SettingsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
