using System.Text.RegularExpressions;
using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class LoginPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public LoginPage(PreferencesService preferences)
    {
        InitializeComponent();
        _preferences = preferences;

        var user = _preferences.CurrentUser;
        NameEntry.Text = user.Name;
        PhoneEntry.Text = user.Phone;
        EmailEntry.Text = user.Email;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? "";
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name) || phone.Length != 10 || !phone.All(char.IsDigit) || !EmailRegex.IsMatch(email))
        {
            await DisplayAlert("Datos incompletos", "Captura nombre, telefono de 10 digitos y correo valido.", "Aceptar");
            return;
        }

        _preferences.CurrentUser = new UserProfile(name, email, phone);
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }

    private async void OnTermsClicked(object sender, EventArgs e)
    {
        await Launcher.Default.OpenAsync(AppConstants.PrivacyUrl);
    }

    private async void OnTermsTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync(AppConstants.PrivacyUrl);
    }
}
