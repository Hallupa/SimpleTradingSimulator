using System.Windows;
using System.Windows.Controls;

namespace TraderTools.Core.UI.Views
{
    /// <summary>
    /// Interaction logic for SimulationResultsControl.xaml
    /// </summary>
    public partial class TradesResultsControl : UserControl
    {
        public TradesResultsControl()
        {
            InitializeComponent();
        }

        private void RowDoubleClick(object sender, RoutedEventArgs e)
        {
            var row = (DataGridRow)sender;
            row.DetailsVisibility = row.DetailsVisibility == Visibility.Collapsed ?
                Visibility.Visible : Visibility.Collapsed;
        }
    }
}