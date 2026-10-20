using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Alak.App.Controls;

public record OptionItem(string Value, string Label);

public partial class OptionComboBox : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(OptionComboBox), string.Empty, propertyChanged: OnTitleChanged);

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(OptionComboBox), string.Empty, propertyChanged: OnDescriptionChanged);

    public static readonly BindableProperty OptionsProperty =
        BindableProperty.Create(nameof(Options), typeof(string), typeof(OptionComboBox), string.Empty, propertyChanged: OnOptionsChanged);

    public static readonly BindableProperty OptionLabelsProperty =
        BindableProperty.Create(nameof(OptionLabels), typeof(string), typeof(OptionComboBox), string.Empty, propertyChanged: OnOptionsChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(string), typeof(OptionComboBox), string.Empty, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Options
    {
        get => (string)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string OptionLabels
    {
        get => (string)GetValue(OptionLabelsProperty);
        set => SetValue(OptionLabelsProperty, value);
    }

    public string SelectedItem
    {
        get => (string)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                UpdateExpandedState();
            }
        }
    }

    public ObservableCollection<OptionItem> Items { get; } = new();

    public OptionComboBox()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(DropdownStack, Items);
    }

    private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OptionComboBox control)
        {
            control.TitleLabel.Text = control.Title;
        }
    }

    private static void OnDescriptionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OptionComboBox control)
        {
            control.DescriptionLabel.Text = control.Description;
            control.DescriptionLabel.IsVisible = !string.IsNullOrEmpty(control.Description);
        }
    }

    private static void OnOptionsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OptionComboBox control)
        {
            control.RebuildItems();
        }
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OptionComboBox control)
        {
            control.UpdateSelectedLabel();
        }
    }

    private void RebuildItems()
    {
        Items.Clear();
        var vals = (Options ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lbls = (OptionLabels ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < vals.Length; i++)
        {
            var val = vals[i];
            var lbl = i < lbls.Length ? lbls[i] : val;
            Items.Add(new OptionItem(val, lbl));
        }

        UpdateSelectedLabel();
    }

    private void UpdateSelectedLabel()
    {
        var match = Items.FirstOrDefault(it => string.Equals(it.Value, SelectedItem, StringComparison.OrdinalIgnoreCase));
        SelectedLabelView.Text = match?.Label ?? SelectedItem ?? string.Empty;
    }

    private void UpdateExpandedState()
    {
        DropdownBorder.IsVisible = _isExpanded;
        ArrowLabel.Text = _isExpanded ? "▴" : "▾";
    }

    private void OnTriggerTapped(object? sender, EventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void OnOptionTapped(object? sender, EventArgs e)
    {
        if (sender is Element elem && elem.BindingContext is OptionItem item)
        {
            SelectedItem = item.Value;
            IsExpanded = false;
        }
    }
}
