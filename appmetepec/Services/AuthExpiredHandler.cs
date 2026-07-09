using System.Net;
using appmetepec.Views;
using CommunityToolkit.Maui.Alerts;

namespace appmetepec.Services;

public sealed class AuthExpiredHandler : DelegatingHandler
{
    private readonly PreferencesService _preferences;
    private bool _redirecting;

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

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint && !_redirecting && _preferences.IsLoggedIn)
        {
            _redirecting = true;
            _preferences.Logout();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Toast.Make("Tu sesion expiro, inicia sesion de nuevo.").Show(cancellationToken);
                    await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
                }
                finally
                {
                    _redirecting = false;
                }
            });
        }

        return response;
    }
}
