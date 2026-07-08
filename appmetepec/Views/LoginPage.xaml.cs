using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class LoginPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;

    public LoginPage(PreferencesService preferences, MetepecApiService api)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Datos incompletos", "Captura usuario y contrasena.", "Aceptar");
            return;
        }

        try
        {
            LoginButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var result = await _api.LoginAsync(username, password);

            if (result is null)
            {
                await DisplayAlert("Acceso denegado", "Usuario o contrasena incorrectos.", "Aceptar");
                return;
            }

            _preferences.JwtToken = result.Token;
            _preferences.CurrentUser = new UserProfile(
                result.User.FullName,
                result.User.Email,
                result.User.PhoneNumber ?? "");

            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Login] {ex.GetType().Name}: {ex.Message}");
            await DisplayAlert("No se pudo conectar", $"{ex.GetType().Name}: {ex.Message}", "Aceptar");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        EyeLabel.TextColor = PasswordEntry.IsPassword
            ? Color.FromArgb("#BBBBBB")
            : Color.FromArgb("#F89A1C");
    }

    private async void OnTermsTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync(AppConstants.PrivacyUrl);
    }

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }
}
