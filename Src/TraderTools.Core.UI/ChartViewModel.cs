using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Abt.Controls.SciChart;
using Abt.Controls.SciChart.Visuals.Axes;

namespace TraderTools.Core.UI
{
    public class ChartViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private IndexRange _xAxisVisibleRange;

        public ObservableCollection<ChartPaneViewModel> ChartPaneViewModels { get; private set; } = new ObservableCollection<ChartPaneViewModel>();

        public IViewportManager ViewportManager { get; private set; } = new DefaultViewportManager();

        ///<summary>
        /// Shared XAxis VisibleRange for all charts
        ///</summary>
        public IndexRange XVisibleRange
        {
            get { return _xAxisVisibleRange; }
            set
            {
                if (Equals(_xAxisVisibleRange, value)) return;

                _xAxisVisibleRange = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}