using appmetepec.Services;
using appmetepec.ViewModels;
using appmetepec.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    // Debe inicializarse antes de usar CrossFirebaseCloudMessaging (ver
                    // HomePage.RegistrarPushSiAplicaAsync). Verificar este namespace/metodo
                    // contra la version de Plugin.Firebase que quede instalada al restaurar --
                    // cambio de forma entre versiones mayores del paquete.
                    events.AddAndroid(android => android.OnCreate((activity, _) =>
                        CrossFirebase.Initialize(activity)));
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
#endif

            builder.Services.AddSingleton<PreferencesService>();
            builder.Services.AddSingleton(sp => new HttpClient(new AuthExpiredHandler(sp.GetRequiredService<PreferencesService>())));
            builder.Services.AddSingleton<ReportCatalogService>();
            builder.Services.AddSingleton<MetepecApiService>();
            builder.Services.AddSingleton<PushRegistrationService>();
            builder.Services.AddSingleton<NavigationState>();
            builder.Services.AddSingleton<PendingTicketsService>();
            builder.Services.AddSingleton<GeocodingService>();

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
            builder.Services.AddTransient<MyTicketsPage>();
            builder.Services.AddTransient<TicketDetailPage>();
            builder.Services.AddTransient<EncuestaPage>();

            return builder.Build();
        }
    }
}
