using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TraderTools.Core.UI.Services
{
    [Export(typeof(ChartingService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ChartingService : DependencyObject
    {
        public static readonly DependencyProperty ChartModeProperty = DependencyProperty.Register(
            "ChartMode", typeof(ChartMode?), typeof(ChartingService), new PropertyMetadata(default(ChartMode?), PropertyChangedCallback));

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (ChartingService)d;
            c._chartModeSubject.OnNext(c.ChartMode);
        }

        public ChartMode? ChartMode
        {
            get { return (ChartMode?) GetValue(ChartModeProperty); }
            set { SetValue(ChartModeProperty, value); }
        }

        private Subject<(DateTime Time, double Price, Action setIsHandled)> _chartClickSubject = new Subject<(DateTime Time, double Price, Action setIsHandled)>();
        private Subject<(DateTime Time, double Price, Action setIsHandled)> _chartMouseMoveSubject = new Subject<(DateTime Time, double Price, Action setIsHandled)>();

        private Subject<ChartMode?> _chartModeSubject = new Subject<ChartMode?>();

        public IObservable<(DateTime Time, double Price, Action setIsHandled)> ChartClickObservable => _chartClickSubject.AsObservable();
        public IObservable<(DateTime Time, double Price, Action setIsHandled)> ChartMoveObservable => _chartMouseMoveSubject.AsObservable();

        public IObservable<ChartMode?> ChartModeObservable => _chartModeSubject.AsObservable();

        public void RaiseChartClick(DateTime time, double price, Action setIsHandled)
        {
            _chartClickSubject.OnNext((time, price, setIsHandled));
        }

        public void RaiseMouseMove(DateTime time, double price, Action setIsHandled)
        {
            _chartMouseMoveSubject.OnNext((time, price, setIsHandled));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}