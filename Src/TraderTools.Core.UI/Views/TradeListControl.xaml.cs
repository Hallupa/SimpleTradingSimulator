using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TraderTools.Core.UI.Views
{
    [Flags]
    public enum TradeListDisplayOptionsFlag
    {
        None = 0,
        PoundsPerPip = 1,
        Quantity = 2,
        InitialStop = 4,
        InitialLimit = 8,
        OrderDate = 16,
        Broker = 32,
        Comments = 64,
        ResultR = 128,
        ViewTrade = 256,
        OrderPrice = 512,
        ClosePrice = 1024
    }

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
            "ShowTradeEntryOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(false, ShowTradeEntryOnlyPropertyChangedCallback));

        public static readonly DependencyProperty ShowBasicDetailsOnlyProperty = DependencyProperty.Register(
            "ShowBasicDetailsOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), ShowBasicDetailsPropertyChangedCallback));

        public static readonly DependencyProperty AllColumnsReadOnlyProperty = DependencyProperty.Register(
            "AllColumnsReadOnly", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), AllColumnsReadOnlyPropertyChanged));

        public static readonly DependencyProperty HideContextMenuProperty = DependencyProperty.Register(
            "HideContextMenu", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), HideContextMenuPropertyChangedCallback));

        public static readonly DependencyProperty HideContextMenuDeleteOptionProperty = DependencyProperty.Register(
            "HideContextMenuDeleteOption", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), HideContextMenuDeleteOptionChanged));

        private static void HideContextMenuDeleteOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl) d;
            var hide = (bool) e.NewValue;
            c.MainContextMenuDeleteMenuItem.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        
        public bool HideContextMenuDeleteOption
        {
            get { return (bool) GetValue(HideContextMenuDeleteOptionProperty); }
            set { SetValue(HideContextMenuDeleteOptionProperty, value); }
        }

        public static readonly DependencyProperty HideContextMenuEditOptionProperty = DependencyProperty.Register(
            "HideContextMenuEditOption", typeof(bool), typeof(TradeListControl), new PropertyMetadata(default(bool), HideContextMenuEditOptionChanged));

        private static void HideContextMenuEditOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            var hide = (bool)e.NewValue;
            c.MainContextMenuEditMenuItem.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool HideContextMenuEditOption
        {
            get { return (bool) GetValue(HideContextMenuEditOptionProperty); }
            set { SetValue(HideContextMenuEditOptionProperty, value); }
        }

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
            c.MainDataGrid.Columns.First(x => (string)x.Header == "£/pip").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Risking").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Risk %").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Profit").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ShowBasicDetailsOnly
        {
            get { return (bool) GetValue(ShowBasicDetailsOnlyProperty); }
            set { SetValue(ShowBasicDetailsOnlyProperty, value); }
        }

        private static void ShowTradeEntryOnlyPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (TradeListControl)d;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "£/pip").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Risking").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Risk %").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Profit").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Result R").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Status").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Entry date").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            c.MainDataGrid.Columns.First(x => (string)x.Header == "Close date").Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ShowTradeEntryOnly
        {
            get { return (bool)GetValue(ShowTradeEntryOnlyProperty); }
            set { SetValue(ShowTradeEntryOnlyProperty, value); }
        }
    }
}