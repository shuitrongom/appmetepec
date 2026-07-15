using appmetepec.Services;
using appmetepec.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace appmetepec.ViewModels;

public sealed partial class SplashViewModel : ObservableObject
{
    private readonly PreferencesService _preferences;

    [ObservableProperty]
    private bool isBusy = true;

    public SplashViewModel(PreferencesService preferences)
    {
        _preferences = preferences;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!IsBusy)
        {
            return;
        }

        try
        {
            await Task.Delay(AppConstants.MinimumSplashDurationMs);

            var route = _preferences.IsLoggedIn ? nameof(HomePage) : nameof(LoginPage);
            await Shell.Current.GoToAsync($"//{route}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
