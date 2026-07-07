using appmetepec.ViewModels;

namespace appmetepec.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage(SplashViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = AnimateEntranceAsync();
    }

    private async Task AnimateEntranceAsync()
    {
        await Task.WhenAll(
            LogoImage.FadeTo(1, 500, Easing.CubicOut),
            AppNameLabel.FadeTo(1, 500, Easing.CubicOut));
    }
}
