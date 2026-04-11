using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Hakufu.MVVM.View.Controls;

/// <summary>
/// An ItemsControl whose UniformGrid panel columns count is bound to <see cref="ColumnsCount"/>.
/// </summary>
public class AdaptiveItemsControl : ItemsControl
{
    public static readonly DependencyProperty ColumnsCountProperty =
        DependencyProperty.Register(
            nameof(ColumnsCount),
            typeof(int),
            typeof(AdaptiveItemsControl),
            new PropertyMetadata(6, OnColumnsCountChanged));

    public int ColumnsCount
    {
        get => (int)GetValue(ColumnsCountProperty);
        set => SetValue(ColumnsCountProperty, value);
    }

    private static void OnColumnsCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AdaptiveItemsControl ctrl)
            ctrl.UpdateGridColumns((int)e.NewValue);
    }

    protected override void OnItemsPanelChanged(ItemsPanelTemplate oldItemsPanel, ItemsPanelTemplate newItemsPanel)
    {
        base.OnItemsPanelChanged(oldItemsPanel, newItemsPanel);
        Dispatcher.InvokeAsync(() => UpdateGridColumns(ColumnsCount));
    }

    private void UpdateGridColumns(int columns)
    {
        if (ItemsPanel is null) return;
        var panel = FindVisualChild<UniformGrid>(this);
        if (panel is not null)
            panel.Columns = columns;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }
}
