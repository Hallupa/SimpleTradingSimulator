using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TraderTools.Core.UI
{
    /// <summary>
    /// Interaction logic for DoubleChartView.xaml
    /// </summary>
    public partial class DoubleChartView : UserControl
    {
        public DoubleChartView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ChartCursorProperty = DependencyProperty.Register(
            "ChartCursor", typeof(Cursor), typeof(DoubleChartView), new PropertyMetadata(Cursors.Arrow));

        public Cursor ChartCursor
        {
            get { return (Cursor)GetValue(ChartCursorProperty); }
            set { SetValue(ChartCursorProperty, value); }
        }
    }
}