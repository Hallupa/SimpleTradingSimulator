using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls.Ribbon;
using System.Windows.Interactivity;
using TraderTools.Basics;

namespace TraderTools.TradingTrainer.Behaviours
{
    public class SelectIndicatorBehaviour : Behavior<RibbonToggleButton>
    {
        private bool _collectionChangedEventRunning = false;

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(MainWindowViewModel), typeof(SelectIndicatorBehaviour), new PropertyMetadata(default(MainWindowViewModel), ViewModelChanged));

        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behaviour = (SelectIndicatorBehaviour) d;
            behaviour.ViewModel.SelectedIndicators.CollectionChanged += behaviour.SelectedIndicatorsOnCollectionChanged;
        }

        public MainWindowViewModel ViewModel
        {
            get { return (MainWindowViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        private void SelectedIndicatorsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _collectionChangedEventRunning = true;
            try
            {
                UpdateIsChecked();
            }
            finally
            {
                _collectionChangedEventRunning = false;
            }
        }

        protected override void OnAttached()
        {
            AssociatedObject.Checked += CheckedChanged;
            AssociatedObject.Unchecked += UncheckedChanged;
            UpdateIsChecked();
        }

        private void UpdateIsChecked()
        {
            var indicator = (IndicatorDisplayOptions)AssociatedObject.DataContext;

            var shouldBeChecked = ViewModel.SelectedIndicators.Contains(indicator);
            if (AssociatedObject.IsChecked != shouldBeChecked) AssociatedObject.IsChecked = shouldBeChecked;
        }

        private void UncheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_collectionChangedEventRunning) return;

            var indicator = (IndicatorDisplayOptions)AssociatedObject.DataContext;
            ViewModel.SelectedIndicators.Remove(indicator);
            ViewModel.SetupChart(false);
        }

        private void CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_collectionChangedEventRunning) return;

            var indicator = (IndicatorDisplayOptions)AssociatedObject.DataContext;

            if (!ViewModel.SelectedIndicators.Contains(indicator))
            {
                ViewModel.SelectedIndicators.Add(indicator);
            }

            ViewModel.SetupChart(false);
        }
    }
}