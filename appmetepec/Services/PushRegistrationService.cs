#if ANDROID || IOS
using Plugin.Firebase.CloudMessaging;
#endif

namespace appmetepec.Services;

// Centraliza el registro del dispositivo para push (ver UsuarioDispositivoPushService.RegistrarAsync
// en el back-end, upsert por Token) para que tanto LoginPage como HomePage lo disparen sin duplicar
// el manejo de Firebase. Deliberadamente no lleva ningun "ya se registro" -- el back-end hace upsert
// por token, asi que repetir la llamada es inofensivo y evita que un fallo silencioso anterior
// (permiso no otorgado, Firebase aun inicializando, etc.) deje al dispositivo sin registrar para
// siempre.
public class PushRegistrationService
{
    // Valor que el back-end guarda en UsuarioDispositivoPush.Plataforma.
#if IOS
    public const string Plataforma = "IOS";
#else
    public const string Plataforma = "ANDROID";
#endif

    private readonly MetepecApiService _api;

    public PushRegistrationService(MetepecApiService api)
    {
        _api = api;
    }

    public async Task RegistrarSiAplicaAsync(int idCiudadano)
    {
        if (idCiudadano <= 0) return;

        try
        {
#if ANDROID || IOS
#if ANDROID
            // Android 13+ (API 33) exige que el usuario otorgue el permiso de notificaciones en
            // tiempo de ejecucion -- declararlo en AndroidManifest.xml no basta. Sin este request
            // explicito, CheckIfValidAsync de abajo falla siempre y el dispositivo nunca se
            // registra, aunque Firebase este bien configurado.
            var permiso = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (permiso != PermissionStatus.Granted) return;
#endif

            // Lanza si el dispositivo no puede recibir cloud messages o el usuario no otorgo el
            // permiso de notificaciones (en iOS es este mismo metodo el que muestra el dialogo de
            // permiso); el catch de abajo absorbe ese caso igual que cualquier otro fallo.
            await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();

            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;

            await _api.RegistrarDispositivoPushAsync(idCiudadano, token, Plataforma, DeviceInfo.Current.Model, AppInfo.Current.VersionString);
#endif
        }
        catch (Exception ex)
        {
            // Best-effort: si falla el registro del dispositivo no debe afectar el login ni la
            // navegacion a Home.
            System.Diagnostics.Debug.WriteLine($"[Push] No se pudo registrar el dispositivo: {ex}");
        }
    }
}
