using appmetepec.Services;
using appmetepec.ViewModels;
using appmetepec.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.LocalNotification;
#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#elif IOS
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.Core.Platforms.iOS;
#endif

namespace appmetepec
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                // Habilita <toolkit:MediaElement> para el reproductor de video de las noticias.
                .UseMauiCommunityToolkitMediaElement()
                // Notificaciones LOCALES (Plugin.LocalNotification): habilita el centro de
                // notificaciones para programar/cancelar los recordatorios de actividades
                // favoritas (ver RecordatoriosService). Es LOCAL en el dispositivo; convive
                // con el push remoto de Firebase (ConfigureLifecycleEvents mas abajo) sin
                // reemplazarlo. No reordenar el registro de Firebase.
                .UseLocalNotification()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Fuente "Como" (la del modulo de Eventos). Alias por peso para usarlos como
                    // FontFamily en el XAML/code-behind (p.ej. FontFamily="ComoSemiBold").
                    fonts.AddFont("Como.ttf", "Como");
                    fonts.AddFont("Como-Light.ttf", "ComoLight");
                    fonts.AddFont("Como-Medium.ttf", "ComoMedium");
                    fonts.AddFont("Como-SemiBold.ttf", "ComoSemiBold");
                    fonts.AddFont("Como-Bold.ttf", "ComoBold");
                    fonts.AddFont("Como-ExtraBold.ttf", "ComoExtraBold");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    // Debe inicializarse antes de usar CrossFirebaseCloudMessaging (ver
                    // HomePage.RegistrarPushSiAplicaAsync). Verificar este namespace/metodo
                    // contra la version de Plugin.Firebase que quede instalada al restaurar --
                    // cambio de forma entre versiones mayores del paquete.
                    // Plugin.Firebase 4.x cambio la firma: Initialize ahora exige un
                    // "activityLocator" (Func<Activity>) ademas de la Activity inicial.
                    events.AddAndroid(android => android.OnCreate((activity, _) =>
                        CrossFirebase.Initialize(activity, () => Platform.CurrentActivity)));
#elif IOS
                    // En iOS ademas hay que inicializar CloudMessaging para que registre los
                    // delegados de APNs antes de que termine el arranque.
                    events.AddiOS(iOS => iOS.WillFinishLaunching((_, _) =>
                    {
                        CrossFirebase.Initialize();
                        FirebaseCloudMessagingImplementation.Initialize();
                        return false;
                    }));
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
            // Permite inspeccionar el mapa (HybridWebView) desde chrome://inspect en la PC.
            builder.Services.AddHybridWebViewDeveloperTools();
#endif

#if ANDROID
            // Los Entry/Editor ya llevan un Border propio dibujando el contorno del campo;
            // quitamos el subrayado nativo de Android para no duplicar el borde.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
                handler.PlatformView.Background = null;
            });
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
                handler.PlatformView.Background = null;
            });

            // Reproductor de YouTube de las noticias (NewsDetailPage): es el unico <WebView> de la
            // app (el mapa usa HybridWebView, otro handler). El Android.Webkit.WebView necesita
            // JavaScript y DOM storage para la IFrame API de YouTube, y MediaPlaybackRequiresUser-
            // Gesture=false para que arranque la reproduccion. El WebView carga una URL remota real
            // (el proxy del backend), por eso estos settings bastan para que el embed reproduzca.
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("YoutubeInlinePlayback", (handler, view) =>
            {
                var settings = handler.PlatformView.Settings;
                if (settings is null)
                {
                    return;
                }
                settings.JavaScriptEnabled = true;
                settings.DomStorageEnabled = true;
                settings.MediaPlaybackRequiresUserGesture = false;
            });
#endif

#if IOS
            // Reproductor de YouTube de las noticias en iOS: el WKWebView debe permitir reproducir
            // medios inline (dentro del recuadro) sin exigir un gesto por cada medio; de lo
            // contrario el embed se ve pero no arranca. Se ajusta la configuracion del WKWebView ya
            // creado por MAUI (mapping, NO se reemplaza su creacion) para conservar el render.
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("YoutubeInlinePlaybackiOS", (handler, view) =>
            {
                var config = handler.PlatformView.Configuration;
                if (config is null)
                {
                    return;
                }
                config.AllowsInlineMediaPlayback = true;
                config.MediaTypesRequiringUserActionForPlayback = WebKit.WKAudiovisualMediaTypes.None;
            });
#endif

            builder.Services.AddSingleton<PreferencesService>();
            // Timeout por defecto de HttpClient (100s) no alcanza para subir un video de evidencia
            // de varios minutos por datos moviles; se amplia para toda la app (las demas llamadas
            // son rapidas y nunca se acercan a este limite).
            builder.Services.AddSingleton(sp => new HttpClient(new AuthExpiredHandler(sp.GetRequiredService<PreferencesService>()))
            {
                Timeout = TimeSpan.FromMinutes(10)
            });
            builder.Services.AddSingleton<ReportCatalogService>();
            builder.Services.AddSingleton<MetepecApiService>();
            builder.Services.AddSingleton<PushRegistrationService>();
            builder.Services.AddSingleton<NavigationState>();
            builder.Services.AddSingleton<PendingTicketsService>();
            builder.Services.AddSingleton<GeocodingService>();
            builder.Services.AddSingleton<EvidencePhotoService>();
            builder.Services.AddSingleton<EventosService>();
            // Favoritos LOCALES del modulo de Eventos (estrella en la programacion de un
            // escenario). Todo local en el dispositivo; base para "mi agenda" y recordatorios
            // en fases futuras.
            builder.Services.AddSingleton<FavoritosService>();
            // Recordatorios LOCALES: servicio HERMANO de FavoritosService. Al marcar una
            // actividad como favorita programa una notificacion local 15 min antes del inicio;
            // al desmarcar la cancela. Encapsula Plugin.LocalNotification. Todo LOCAL.
            builder.Services.AddSingleton<RecordatoriosService>();

            builder.Services.AddTransient<SplashViewModel>();
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<RecoverAccountPage>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<ReportPage>();
            builder.Services.AddTransient<ReportSuccessPage>();
            builder.Services.AddTransient<NewsDetailPage>();
            builder.Services.AddTransient<ArticuloDetailPage>();
            builder.Services.AddTransient<RecoleccionPage>();
            builder.Services.AddTransient<AlertaNaranjaPage>();
            builder.Services.AddTransient<PanicoPage>();
            builder.Services.AddTransient<PanicoConfirmarPage>();
            builder.Services.AddTransient<MyTicketsPage>();
            builder.Services.AddTransient<TicketDetailPage>();
            builder.Services.AddTransient<EncuestaPage>();
            builder.Services.AddTransient<PerfilPage>();
            builder.Services.AddTransient<EventoMapaPage>();
            builder.Services.AddTransient<EscenarioProgramaPage>();

            return builder.Build();
        }
    }
}
