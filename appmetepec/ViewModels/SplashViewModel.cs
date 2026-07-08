using appmetepec.Services;
using appmetepec.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace appmetepec.ViewModels;

public sealed partial class SplashViewModel : ObservableObject
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;

    [ObservableProperty]
    private bool isBusy = true;

    public SplashViewModel(PreferencesService preferences, MetepecApiService api)
    {
        _preferences = preferences;
        _api = api;
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
            await Task.WhenAll(
                LoadRemoteConfigSafeAsync(),
                Task.Delay(AppConstants.MinimumSplashDurationMs));

            var route = _preferences.IsLoggedIn ? nameof(HomePage) : nameof(LoginPage);
            await Shell.Current.GoToAsync($"//{route}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadRemoteConfigSafeAsync()
    {
        try
        {
            await _api.LoadZendeskCredentialsAsync();
        }
        catch (Exception ex)
        {
            // Igual que Splash.kt (error -> view.gotoHomeActivity()): un fallo de red al arrancar no bloquea la navegacion
            System.Diagnostics.Debug.WriteLine($"[Splash] No se pudo cargar configuracion remota: {ex.Message}");
        }
    }
}
