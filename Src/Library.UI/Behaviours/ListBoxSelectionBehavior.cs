using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace Hallupa.Library.UI.Behaviours
{
    public class ListBoxSelectionBehavior : Behavior<ListBox>
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(nameof(SelectedItems), typeof(List<object>),
                typeof(ListBoxSelectionBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedItemsChanged));

        private static void OnSelectedItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var behavior = (ListBoxSelectionBehavior)sender;
            if (behavior._modelHandled) return;

            if (behavior.AssociatedObject == null)
                return;


            //behavior._modelHandled = true;
            behavior.SelectItems();
            //behavior._modelHandled = false;
        }

        private bool _viewHandled;
        private bool _modelHandled;

        public List<object> SelectedItems
        {
            get { return (List<object>)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        // Propagate selected items from model to view
        private void SelectItems()
        {
            //_viewHandled = true;
            //AssociatedObject.SelectedItems.Clear();
            //if (SelectedItems != null)
            //{
            SelectedItems.Clear();

            foreach (var item in AssociatedObject.SelectedItems)
                SelectedItems.Add(item);
            //}
            //_viewHandled = false;
        }

        // Propagate selected items from view to model
        private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (_viewHandled) return;
            if (AssociatedObject.Items.SourceCollection == null) return;
            SelectedItems = AssociatedObject.SelectedItems.Cast<object>().ToList();
        }

        // Re-select items when the set of items changes
        /*private void OnListBoxItemsChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (_viewHandled) return;
            if (AssociatedObject.Items.SourceCollection == null) return;
            SelectItems();
        }*/

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.SelectionChanged += OnListBoxSelectionChanged;
            //((INotifyCollectionChanged)AssociatedObject.Items).CollectionChanged += OnListBoxItemsChanged;
        }

        /// <inheritdoc />
        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged -= OnListBoxSelectionChanged;
                //((INotifyCollectionChanged)AssociatedObject.Items).CollectionChanged -= OnListBoxItemsChanged;
            }
        }
    }
}