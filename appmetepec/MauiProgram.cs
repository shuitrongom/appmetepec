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

            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<PreferencesService>();
            builder.Services.AddSingleton<ReportCatalogService>();
            builder.Services.AddSingleton<MetepecApiService>();
            builder.Services.AddSingleton<NavigationState>();

            builder.Services.AddTransient<SplashViewModel>();
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<ReportPage>();
            builder.Services.AddTransient<ReportSuccessPage>();
            builder.Services.AddTransient<NewsDetailPage>();
            builder.Services.AddTransient<RecoleccionPage>();
            builder.Services.AddTransient<AlertaNaranjaPage>();

            return builder.Build();
        }
    }
}
