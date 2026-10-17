using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class StubDetailPage : ContentPage, IQueryAttributable
{
    private string? _loadedId;

    public StubDetailPage(StubDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string id && !string.IsNullOrEmpty(id))
        {
            if (_loadedId != id && BindingContext is StubDetailViewModel vm)
            {
                _loadedId = id;
                _ = vm.LoadAsync(id);
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
                    var id = System.Web.HttpUtility.ParseQueryString(query)["id"];
                    if (!string.IsNullOrEmpty(id) && _loadedId != id)
                    {
                        _loadedId = id;
                        await vm.LoadAsync(id);
                    }
                }
            }
        }
    }
}