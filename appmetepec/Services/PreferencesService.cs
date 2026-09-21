using appmetepec.Models;

namespace appmetepec.Services;

public sealed class PreferencesService
{
    public UserProfile CurrentUser
    {
        get => new(
            Preferences.Default.Get("CurrentUserName", ""),
            Preferences.Default.Get("CurrentUserEmail", ""),
            Preferences.Default.Get("CurrentUserPhone", ""));
        set
        {
            Preferences.Default.Set("CurrentUserName", value.Name);
            Preferences.Default.Set("CurrentUserEmail", value.Email);
            Preferences.Default.Set("CurrentUserPhone", value.Phone);
        }
    }

    public string RecoleccionToken
    {
        get => Preferences.Default.Get(nameof(RecoleccionToken), "");
        set => Preferences.Default.Set(nameof(RecoleccionToken), value);
    }

    public string NaranjaName
    {
        get => Preferences.Default.Get(nameof(NaranjaName), "");
        set => Preferences.Default.Set(nameof(NaranjaName), value);
    }

    public string JwtToken
    {
        get => Preferences.Default.Get(nameof(JwtToken), "");
        set => Preferences.Default.Set(nameof(JwtToken), value);
    }

    public int CiudadanoId
    {
        get => Preferences.Default.Get(nameof(CiudadanoId), 0);
        set => Preferences.Default.Set(nameof(CiudadanoId), value);
    }

    public bool DarkThemeEnabled
    {
        get => Preferences.Default.Get(nameof(DarkThemeEnabled), false);
        set => Preferences.Default.Set(nameof(DarkThemeEnabled), value);
    }

    public DateTime? ProximaFechaEncuestaExperienciaApp
    {
        get
        {
            var ticks = Preferences.Default.Get(nameof(ProximaFechaEncuestaExperienciaApp), 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
        set => Preferences.Default.Set(nameof(ProximaFechaEncuestaExperienciaApp), value?.Ticks ?? 0L);
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(JwtToken);

    public void Logout()
    {
        Preferences.Default.Remove(nameof(JwtToken));
        Preferences.Default.Remove(nameof(CiudadanoId));
        CurrentUser = new UserProfile("", "", "");
    }
}
