using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class StubDetailPage : ContentPage
{
    public StubDetailPage(StubDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is StubDetailViewModel vm)
        {
            var query = Shell.Current.CurrentState.Location.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var id = System.Web.HttpUtility.ParseQueryString(query)["id"];
                if (!string.IsNullOrEmpty(id))
                {
                    await vm.LoadAsync(id);
                }
            }
        }
    }
}