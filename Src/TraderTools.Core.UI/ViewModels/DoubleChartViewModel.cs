using System.Collections.Generic;
using System.Windows;
using Abt.Controls.SciChart;
using TraderTools.Basics;

namespace TraderTools.Core.UI.ViewModels
{
    public abstract class DoubleChartViewModel : DependencyObject
    {
        private string _selectedLargeChartTimeframe;

        public DoubleChartViewModel()
        {
            LargeChartTimeframes = new List<string>
            {
                "Trade timeframe",
                "M1",
                "H2",
                "H4",
                "D1"
            };

            SelectedLargeChartTimeframe = "Trade timeframe";

            ChartViewModel.XVisibleRange = new IndexRange();
            ChartViewModelSmaller1.XVisibleRange = new IndexRange();
        }

        public List<string> LargeChartTimeframes { get; }

        public string SelectedLargeChartTimeframe
        {
            get => _selectedLargeChartTimeframe;
            set
            {
                _selectedLargeChartTimeframe = value;
                SelectedLargeChartTimeChanged();
            }
        }

        protected virtual void SelectedLargeChartTimeChanged()
        {
        }

        protected Timeframe GetSelectedTimeframe(TradeDetails selectedTrade)
        {
            var timeframe = selectedTrade != null && selectedTrade.Timeframe  != null ? selectedTrade.Timeframe.Value : Timeframe.H2;

            switch (SelectedLargeChartTimeframe)
            {
                case "M1":
                    timeframe = Timeframe.M1;
                    break;
                case "H2":
                    timeframe = Timeframe.H2;
                    break;
                case "H4":
                    timeframe = Timeframe.H4;
                    break;
                case "D1":
                    timeframe = Timeframe.D1;
                    break;
            }

            return timeframe;
        }

        public ChartViewModel ChartViewModel { get; } = new ChartViewModel();
        public ChartViewModel ChartViewModelSmaller1 { get; } = new ChartViewModel();
    }
}