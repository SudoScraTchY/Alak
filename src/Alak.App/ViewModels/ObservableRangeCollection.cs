using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Alak.App.ViewModels;

/// <summary>
/// An ObservableCollection that allows batch updates (ReplaceRange/AddRange)
/// raising a single Reset notification to prevent N layout passes in virtualized lists.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public ObservableRangeCollection() { }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    public void ReplaceRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _suppressNotification = true;
        Items.Clear();
        foreach (var item in collection)
        {
            Items.Add(item);
        }
        _suppressNotification = false;

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _suppressNotification = true;
        foreach (var item in collection)
        {
            Items.Add(item);
        }
        _suppressNotification = false;

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
