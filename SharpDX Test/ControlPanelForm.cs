using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SharpDX_Test
{
    public partial class ControlPanelForm : Form
    {
        public ControlPanelForm(object instance, params string[] propNames)
        {
            InitializeComponent();

            const int maxScrollValue = 1000000;

            var trackBars = new[]
            {
                trackBar1, trackBar2, trackBar3,
                trackBar4, trackBar5, trackBar6,
                trackBar7, trackBar8, trackBar9
            };

            var type = instance.GetType();
            var fields = propNames.Select(i =>
            {
                var Field = type.GetField(i);
                var range = Field.GetCustomAttribute<RangeAttribute>();
                return new { Field, range.Min, range.Max };
            }).ToArray();

            for (var i = 0; i < fields.Length; i++)
            {
                var trackBar = trackBars[i];

                var field = fields[i];
                trackBar.Value = (int)(maxScrollValue * ((float)field.Field.GetValue(instance) - field.Min) / (field.Max - field.Min));
                trackBar.Scroll += (s, e) =>
                {
                    field.Field.SetValue(instance, (field.Max - field.Min) * trackBar.Value / maxScrollValue);
                };
            }
        }
    }
}
