using Microsoft.Maui.Controls;
using Alak.App.ViewModels;

namespace Alak.App.Views;

public partial class RulesPage : ContentPage
{
    public RulesPage(RulesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnRuleToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch sw && sw.BindingContext is UserRuleViewModel ruleVm)
        {
            ruleVm.Refresh();
        }
    }
}