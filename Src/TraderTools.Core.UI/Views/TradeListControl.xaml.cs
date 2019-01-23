using System.Windows;
using System.Windows.Controls;

namespace TraderTools.Core.UI.Views
{
    /// <summary>
    /// Interaction logic for TradeListControl.xaml
    /// </summary>
    public partial class TradeListControl : UserControl
    {
        public TradeListControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ShowTradeEntryOnlyProperty = DependencyProperty.Register(
            "ShowTradeEntryOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(false, PropertyChangedCallback));

        public static readonly DependencyProperty ShowBasicDetailsOnlyProperty = DependencyProperty.Register(
            "ShowBasicDetailsOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), ShowBasicDetailsPropertyChangedCallback));

        public static readonly DependencyProperty AllColumnsReadOnlyProperty = DependencyProperty.Register(
            "AllColumnsReadOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), AllColumnsReadOnlyPropertyChanged));

        public static readonly DependencyProperty HideContextMenuProperty = DependencyProperty.Register(
            "HideContextMenu", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), HideContextMenuPropertyChangedCallback));

        private static void HideContextMenuPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            var hide = (bool)e.NewValue;
            c.MainContextMenu.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool HideContextMenu
        {
            get { return (bool) GetValue(HideContextMenuProperty); }
            set { SetValue(HideContextMenuProperty, value); }
        }

        private static void AllColumnsReadOnlyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            foreach (var column in c.MainDataGrid.Columns)
            {
                column.IsReadOnly = true;
            }
        }

        public bool AllColumnsReadOnly
        {
            get { return (bool) GetValue(AllColumnsReadOnlyProperty); }
            set { SetValue(AllColumnsReadOnlyProperty, value); }
        }

        private static void ShowBasicDetailsPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            c.MainDataGrid.Columns[1].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[7].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[13].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[14].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[15].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ShowBasicDetailsOnly
        {
            get { return (bool) GetValue(ShowBasicDetailsOnlyProperty); }
            set { SetValue(ShowBasicDetailsOnlyProperty, value); }
        }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            c.MainDataGrid.Columns[2].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[6].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[9].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[10].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[11].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[12].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[13].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns[15].Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ShowTradeEntryOnly
        {
            get { return (bool)GetValue(ShowTradeEntryOnlyProperty); }
            set { SetValue(ShowTradeEntryOnlyProperty, value); }
        }
    }
}