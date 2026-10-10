using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class DetailPage : ContentPage
{
    public DetailPage(DetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is DetailViewModel vm)
        {
            // Get ID from query parameters
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