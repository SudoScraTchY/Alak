using System;
using Alak.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class DetailPage : ContentPage, IQueryAttributable
{
    private string? _loadedId;

    public DetailPage() : this(AppServices.Current?.GetRequiredService<DetailViewModel>()!)
    {
    }

    public DetailPage(DetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string id && !string.IsNullOrEmpty(id))
        {
            var unescapedId = Uri.UnescapeDataString(id);
            if (_loadedId != unescapedId && BindingContext is DetailViewModel vm)
            {
                _loadedId = unescapedId;
                _ = vm.LoadAsync(unescapedId);
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is DetailViewModel vm)
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