using System;
using System.Collections.Generic;
using log4net.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using log4net;

namespace Hallupa.Library.UI
{
    /// <summary>
    /// Interaction logic for LogControl.xaml
    /// </summary>
    public partial class LogControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        private bool _autoScroll;
        private bool _showExpanded;
        private int _upto = -1;
        private bool _showDebug = false;
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;
        private CollectionView _loggingView;
        private bool _lvLoaded;
        private DispatcherTimer _timer;
        private const int MaxItems = 10000;

        public LogControl()
        {
            AutoScroll = true;
            InitializeComponent();

            DataContext = this;

            LogListView.Loaded += (sender, args) =>
            {
                if (!_lvLoaded)
                {
                    _loggingView = (CollectionView)CollectionViewSource.GetDefaultView(LogListView.ItemsSource);
                    _loggingView.Filter = Filter;
                    _lvLoaded = true;
                }
            };

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, TimerCallback, Dispatcher.CurrentDispatcher);

            ClearCommand = new DelegateCommand(Clear);
        }

        private void Clear(object obj)
        {
            LogItems.Clear();
        }

        private bool Filter(object obj)
        {
            var loggingEvent = (LoggingEvent)obj;

            if (loggingEvent.Level == Level.Debug && _showDebug)
            {
                return true;
            }

            if (loggingEvent.Level == Level.Info && _showInfo)
            {
                return true;
            }

            if (loggingEvent.Level == Level.Warn && _showWarning)
            {
                return true;
            }

            if (loggingEvent.Level == Level.Error && _showError)
            {
                return true;
            }

            return false;
        }

        public ObservableCollection<LoggingEvent> LogItems { get; } = new ObservableCollection<LoggingEvent>();

        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                _autoScroll = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand ClearCommand { get; private set; }

        public bool ShowExpanded
        {
            get => _showExpanded;
            set
            {
                _showExpanded = value;
                UpdateLogSize();
            }
        }

        public bool ShowDebug
        {
            get => _showDebug;
            set
            {
                _showDebug = value;
                Refresh();
            }
        }

        public bool ShowInfo
        {
            get => _showInfo;
            set
            {
                _showInfo = value;
                Refresh();
            }
        }

        public bool ShowWarning
        {
            get => _showWarning;
            set
            {
                _showWarning = value;
                Refresh();
            }
        }

        public bool ShowError
        {
            get => _showError;
            set
            {
                _showError = value;
                Refresh();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Refresh()
        {
            CollectionViewSource.GetDefaultView(LogListView.ItemsSource).Refresh();
        }

        private void TimerCallback(object sender, EventArgs eventArgs)
        {
            if (LogControlAppender.LoggingEventCount <= LogItems.Count)
            {
                return;
            }

            List<LoggingEvent> newLogItems = null;
            try
            {
                newLogItems = LogControlAppender.GetLoggingEvents();
                var max = newLogItems.Count - 1;
                for (var i = _upto + 1; i <= max; i++)
                {
                    LogItems.Add(newLogItems[i]);

                    if (LogItems.Count > MaxItems)
                    {
                        LogItems.RemoveAt(0);
                    }
                }
                _upto = max;
            }
            catch (Exception ex)
            {
                Log.Error($"Error while trying to add log message to LogControl - LogItems: {LogItems.Count} NewLogItems: {newLogItems?.Count ?? 0}", ex);

            }
        }

        private void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            ShowExpanded = !ShowExpanded;
        }

        private void UpdateLogSize()
        {
            if (ShowExpanded)
            {
                LogRow.Height = new GridLength(300);
            }
            else
            {
                LogRow.Height = new GridLength(0);
            }
        }
    }
}