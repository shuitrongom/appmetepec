namespace appmetepec
{
    public partial class App : Application
    {
        public App(Services.AperturaTicketPushService aperturaTicketPush)
        {
            InitializeComponent();

            UserAppTheme = Preferences.Default.Get(nameof(Services.PreferencesService.DarkThemeEnabled), false)
                ? AppTheme.Dark
                : AppTheme.Light;

#if ANDROID || IOS
            // Toque en una notificacion push con "idTicket": abre ese reporte (ver
            // AperturaTicketPushService). Se suscribe aqui, al arrancar, para no perder el toque que
            // abrio la app estando cerrada.
            Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.NotificationTapped +=
                (_, e) => aperturaTicketPush.RegistrarToque(e.Notification?.Data);
#endif

            MainPage = new AppShell();
        }
    }
}
