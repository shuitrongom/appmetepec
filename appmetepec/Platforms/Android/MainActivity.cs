using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plugin.Firebase.CloudMessaging;

namespace appmetepec
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // El toque en una notificacion push llega como Intent: con la app cerrada en OnCreate y con
        // la app en segundo plano en OnNewIntent (SingleTop). Plugin.Firebase lo convierte en el
        // evento NotificationTapped (ver App.xaml.cs / AperturaTicketPushService).
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ProcesarIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            ProcesarIntent(intent);
        }

        private static void ProcesarIntent(Intent? intent)
        {
            if (intent is not null)
            {
                FirebaseCloudMessagingImplementation.OnNewIntent(intent);
            }
        }
    }
}
