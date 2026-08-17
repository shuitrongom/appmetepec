using appmetepec.Services;
using appmetepec.ViewModels;
using appmetepec.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

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
            builder.Services.AddSingleton<NavigationState>();
            builder.Services.AddSingleton<PendingTicketsService>();

            builder.Services.AddTransient<SplashViewModel>();
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
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
