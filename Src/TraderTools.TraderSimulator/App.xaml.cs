﻿using System.ComponentModel.Composition;
using System.Reflection;
using System.Windows;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Core.Services;
using TraderTools.Core.UI.Services;

namespace TraderTools.TradingSimulator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        [Import] private IDataDirectoryService _dataDirectoryService;

        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Info("Starting application");

            DependencyContainer.AddAssembly(typeof(App).Assembly);
            DependencyContainer.AddAssembly(typeof(ChartingService).Assembly);
            DependencyContainer.AddAssembly(typeof(MarketDetailsService).Assembly);

            DependencyContainer.ComposeParts(this);
            _dataDirectoryService.SetApplicationName("TradeSimulator");
        }
    }
}
