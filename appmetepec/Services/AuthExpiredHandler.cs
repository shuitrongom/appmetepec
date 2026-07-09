using System.Net;
using appmetepec.Views;

namespace appmetepec.Services;

public sealed class AuthExpiredHandler : DelegatingHandler
{
    private readonly PreferencesService _preferences;
    private int _redirecting;

    public AuthExpiredHandler(PreferencesService preferences)
    {
        _preferences = preferences;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var path = request.RequestUri?.AbsolutePath ?? "";
        var isAuthEndpoint = path.EndsWith("/seguridad/login") || path.EndsWith("/ciudadanos/registro");

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint
            && _preferences.IsLoggedIn && Interlocked.CompareExchange(ref _redirecting, 1, 0) == 0)
        {
            _preferences.Logout();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Auth] No se pudo navegar a LoginPage: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _redirecting, 0);
                }
            });
        }

        return response;
    }
}
