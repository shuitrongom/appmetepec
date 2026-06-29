using appmetepec.Services;
using appmetepec.Views;
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

            builder.Services.AddTransient<LoginPage>();
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
