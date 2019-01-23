using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace TraderTools.Core.UI.Services
{
    [Export(typeof(ChartingService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ChartingService : INotifyPropertyChanged
    {
        private ChartMode? _chartMode;

        public ChartMode? ChartMode
        {
            get => _chartMode;
            set
            {
                _chartMode = value;
                OnPropertyChanged();
                _chartModeSubject.OnNext(ChartMode);
            }
        }

        private Subject<(DateTime Time, double Price, Action setIsHandled)> _chartClickSubject = new Subject<(DateTime Time, double Price, Action setIsHandled)>();

        private Subject<ChartMode?> _chartModeSubject = new Subject<ChartMode?>();

        public IObservable<(DateTime Time, double Price, Action setIsHandled)> ChartClickObservable => _chartClickSubject.AsObservable();

        public IObservable<ChartMode?> ChartModeObservable => _chartModeSubject.AsObservable();

        public void RaiseChartClick(DateTime time, double price, Action setIsHandled)
        {
            _chartClickSubject.OnNext((time, price, setIsHandled));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}