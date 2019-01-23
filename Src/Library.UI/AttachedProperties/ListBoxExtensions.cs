using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Hallupa.Library.UI.AttachedProperties
{
    public static class ListBoxExtensions
    {
        public static readonly DependencyProperty AutoScrollToEndProperty =
            DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(ListBoxExtensions),
                new UIPropertyMetadata(OnAutoScrollToEndChanged));

        private static readonly DependencyProperty AutoScrollToEndHandlerProperty =
            DependencyProperty.RegisterAttached("AutoScrollToEndHandler", typeof(NotifyCollectionChangedEventHandler),
                typeof(ListBoxExtensions));

        private static DispatcherTimer _timer;
        private static bool _moveToLastItem;
        private static ListBox _listView;

        public static bool GetAutoScrollToEnd(DependencyObject obj) => (bool) obj.GetValue(AutoScrollToEndProperty);

        public static void SetAutoScrollToEnd(DependencyObject obj, bool value) =>
            obj.SetValue(AutoScrollToEndProperty, value);

        private static void OnAutoScrollToEndChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            var listView = s as ListBox;

            if (listView == null)
                return;

            var source = (INotifyCollectionChanged) listView.Items.SourceCollection;

            if ((bool) e.NewValue)
            {
                if (_timer == null)
                {
                    _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Normal, Callback, Dispatcher.CurrentDispatcher);
                }
                else
                {
                    _timer.Start();
                }

                _listView = listView;
            }
            else
            {
                _timer.Stop();
            }
        }

        private static void Callback(object sender, EventArgs e)
        {
            if (_listView.Items.Count > 0 && _listView.Items.CurrentItem != _listView.Items[_listView.Items.Count - 1])
            {
                _listView.Items.MoveCurrentToLast();
                _listView.ScrollIntoView(_listView.Items.CurrentItem);
            }
        }
    }
}