using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Abt.Controls.SciChart;
using Hallupa.Library;
using Hallupa.Library.UI;
using Octokit;

namespace TraderTools.TradingSimulator
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

            SciStockChart mainChart = null;
            SciStockChart smallerChart = null;
            
            Func<string> getInput = () =>
            {
                _dlg = new InputWindow
                {
                    WindowLabel = { Content = "Comments:" },
                    WindowTextBox = { Text = "" },
                    Owner = this
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
                c => DoubleChart.ChartCursor = c,
                (trade, completeCallback, vm) =>
                {
                    var editTradeWindow = new EditTradeWindow { Owner = this };
                    editTradeWindow.Closed += (sender, args) => { completeCallback?.Invoke(); };
                    vm.CloseEditViewAction = () => editTradeWindow.Close();
                    editTradeWindow.DataContext = vm;
                    editTradeWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    editTradeWindow.Show();
                },
                () =>
                {
                    var mainChartLocal = mainChart ?? (mainChart = VisualHelper.GetChildOfType<SciStockChart>(DoubleChart.MainChartGroup));
                    var smallerChartLocal = smallerChart ?? (smallerChart = VisualHelper.GetChildOfType<SciStockChart>(DoubleChart.SmallerChartGroup));

                    var d1 = mainChartLocal.SuspendUpdates();
                    var d2 = smallerChartLocal.SuspendUpdates();
                    return new DisposableAction(() =>
                    {
                        d1.Dispose();
                        d2.Dispose();
                    });
                },
                UpdateLayout);

            PreviewKeyDown += UIElement_OnPreviewKeyDown;

            Task.Run(() =>
            {
                var github = new GitHubClient(new ProductHeaderValue("Hallupa"));
                var releases = github.Repository.Release.GetAll("Hallupa", "SimpleTradingSimulator").Result;
                var latestReleaseVersion = releases[0].TagName.Replace("v", "");
                var assemblyVersion = typeof(MainWindow).Assembly.GetName().Version;
                var currentVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

                if (latestReleaseVersion != currentVersion)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            "Newer version is available - please download from https://github.com/Hallupa/SimpleTradingSimulator",
                            "Newer version available",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
            });
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
                        var mousePoint = Mouse.GetPosition((UIElement)stockChart.ModifierSurface);
                        var candleIndex = (int)xCalc.GetDataValue(mousePoint.X);
                        var price = (decimal)yCalc.GetDataValue(mousePoint.Y);

                        ((MainWindowViewModel)DataContext).KeyDown(e.Key, candleIndex, price);
                        chartKey = true;
                    }
                }

                if (!chartKey)
                {
                    ((MainWindowViewModel)DataContext).KeyDown(e.Key, null, null);
                }
            }
        }
    }
}