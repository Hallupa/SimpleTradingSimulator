using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TraderTools.Core.UI.ViewModels
{
    public class StrategyRunResult : INotifyPropertyChanged
    {
        private string _name;
        private int _completedTrades;
        private decimal _percentSuccessfulTrades;
        private decimal _avRWinningTrades;
        private decimal _avRLosingTrades;
        private decimal _rSum;
        private decimal _avAdverseRFor10Candles;
        private decimal _avPositiveRFor20Candles;
        private decimal _expectancyR;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public int CompletedTrades
        {
            get => _completedTrades;
            set
            {
                _completedTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal PercentSuccessfulTrades
        {
            get => _percentSuccessfulTrades;
            set
            {
                _percentSuccessfulTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvRWinningTrades
        {
            get => _avRWinningTrades;
            set
            {
                _avRWinningTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvRLosingTrades
        {
            get => _avRLosingTrades;
            set
            {
                _avRLosingTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvAdverseRFor10Candles
        {
            get => _avAdverseRFor10Candles;
            set
            {
                _avAdverseRFor10Candles = value;
                OnPropertyChanged();
            }
        }

        public decimal RExpectancy
        {
            get => _expectancyR;
            set
            {
                _expectancyR = value;
                OnPropertyChanged();
            }

        }

        public decimal AvPositiveRFor20Candles
        {
            get => _avPositiveRFor20Candles;
            set
            {
                _avPositiveRFor20Candles = value;
                OnPropertyChanged();
            }
        }

        public decimal RSum
        {
            get => _rSum;
            set
            {
                _rSum = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}