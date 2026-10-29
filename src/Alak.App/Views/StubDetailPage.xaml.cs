using System;
using Alak.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class StubDetailPage : ContentPage, IQueryAttributable
{
    private string? _loadedId;

    public StubDetailPage() : this(AppServices.Current?.GetRequiredService<StubDetailViewModel>()!)
    {
    }

    public StubDetailPage(StubDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string id && !string.IsNullOrEmpty(id))
        {
            var unescapedId = Uri.UnescapeDataString(id);
            if (_loadedId != unescapedId && BindingContext is StubDetailViewModel vm)
            {
                _loadedId = unescapedId;
                _ = vm.LoadAsync(unescapedId);
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is StubDetailViewModel vm)
        {
            var loc = Shell.Current?.CurrentState?.Location?.OriginalString;
            if (!string.IsNullOrEmpty(loc))
            {
                var qIdx = loc.IndexOf('?');
                if (qIdx >= 0 && qIdx < loc.Length - 1)
                {
                    var query = loc.Substring(qIdx);
                    var rawId = System.Web.HttpUtility.ParseQueryString(query)["id"];
                    if (!string.IsNullOrEmpty(rawId))
                    {
                        var unescapedId = Uri.UnescapeDataString(rawId);
                        if (_loadedId != unescapedId)
                        {
                            _loadedId = unescapedId;
                            await vm.LoadAsync(unescapedId);
                        }
                    }
                }
            }
        }
    }
}