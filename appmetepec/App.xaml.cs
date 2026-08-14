namespace appmetepec
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            UserAppTheme = Preferences.Default.Get(nameof(Services.PreferencesService.DarkThemeEnabled), false)
                ? AppTheme.Dark
                : AppTheme.Light;

            MainPage = new AppShell();
        }
    }
}
