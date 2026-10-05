using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class ScanPage : ContentPage
{
    public ScanPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Start scan animation
        AnimateScanLine();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.AbortAnimation("ScanLineAnimation");
    }

    private void AnimateScanLine()
    {
        if (ScanLine == null) return;

        var animation = new Animation(
            callback: v => ScanLine.TranslationY = v,
            start: 0,
            end: 220,
            easing: Easing.Linear
        );

        animation.Commit(this, "ScanLineAnimation", length: 2000, repeat: () => true);
    }
}