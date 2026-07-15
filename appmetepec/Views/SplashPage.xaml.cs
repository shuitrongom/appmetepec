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
        _ = BackgroundImage.FadeTo(1, 400, Easing.CubicOut);
    }
}
