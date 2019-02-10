using System.Reflection;
using System.Windows;
using Abt.Controls.SciChart.Visuals;
using Hallupa.Library;
using log4net;
using TraderTools.Core.UI.Services;

namespace TraderTools.TradingSimulator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Info("Starting application");

            DependencyContainer.AddAssembly(typeof(App).Assembly);
            DependencyContainer.AddAssembly(typeof(ChartingService).Assembly);
        }
    }
}
