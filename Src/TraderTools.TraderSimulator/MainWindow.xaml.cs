using System;
using System.Windows;
using System.Windows.Input;
using Abt.Controls.SciChart;
using Hallupa.Library.UI;

namespace TraderTools.TradingTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private InputWindow _dlg;

        public MainWindow()
        {
            InitializeComponent();

            Title = Title + $" {typeof(MainWindow).Assembly.GetName().Version}";

            Func<string> getInput = () =>
            {
                _dlg = new InputWindow
                {
                    WindowLabel = {Content = "Strategy:"}, WindowTextBox = {Text = ""}, Owner = this
                };
                _dlg.ShowDialog();
                Focus();
                var text = _dlg.WindowTextBox.Text;
                _dlg = null;

                return text;
            };
            DataContext = new MainWindowViewModel(
                getInput,
                txt => Dispatcher.Invoke(() =>
                {
                    var res = MessageBox.Show(txt, "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                    Focus();
                    return res;
                }),
                c => Cursor = c);

            PreviewKeyDown += UIElement_OnPreviewKeyDown;
        }

        private void UIElement_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_dlg == null)
            {
                var chartKey = false;
                if (DoubleChart.MainChartGroup.IsMouseOver || DoubleChart.SmallerChartGroup.IsMouseOver)
                {
                    var chartGroup = DoubleChart.MainChartGroup.IsMouseOver
                        ? DoubleChart.MainChartGroup
                        : DoubleChart.SmallerChartGroup;
                    var stockChart = VisualHelper.GetChildOfType<SciStockChart>(chartGroup);

                    if (stockChart != null)
                    {
                        var xCalc = stockChart.XAxis.GetCurrentCoordinateCalculator();
                        var yCalc = stockChart.YAxis.GetCurrentCoordinateCalculator();
                        var mousePoint = Mouse.GetPosition((UIElement) stockChart.ModifierSurface);
                        var candleIndex = (int) xCalc.GetDataValue(mousePoint.X);
                        var price = (decimal) yCalc.GetDataValue(mousePoint.Y);

                        ((MainWindowViewModel) DataContext).KeyDown(e.Key, candleIndex, price);
                        chartKey = true;
                    }
                }

                if (!chartKey)
                {
                    ((MainWindowViewModel) DataContext).KeyDown(e.Key, null, null);
                }
            }
        }
    }
}