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

    public string ZendeskUser
    {
        get => Preferences.Default.Get(nameof(ZendeskUser), "");
        set => Preferences.Default.Set(nameof(ZendeskUser), value);
    }

    public string ZendeskToken
    {
        get => Preferences.Default.Get(nameof(ZendeskToken), "");
        set => Preferences.Default.Set(nameof(ZendeskToken), value);
    }

    public string RecoleccionToken
    {
        get => Preferences.Default.Get(nameof(RecoleccionToken), "");
        set => Preferences.Default.Set(nameof(RecoleccionToken), value);
    }

    public string SchedStartTime
    {
        get => Preferences.Default.Get(nameof(SchedStartTime), "08:00");
        set => Preferences.Default.Set(nameof(SchedStartTime), value);
    }

    public string SchedEndTime
    {
        get => Preferences.Default.Get(nameof(SchedEndTime), "18:00");
        set => Preferences.Default.Set(nameof(SchedEndTime), value);
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

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(JwtToken);

    public void Logout()
    {
        Preferences.Default.Remove(nameof(JwtToken));
        CurrentUser = new UserProfile("", "", "");
    }
}
