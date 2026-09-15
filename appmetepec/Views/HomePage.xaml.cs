using System.Text.RegularExpressions;
using appmetepec.Models;
using appmetepec.Services;
using CommunityToolkit.Maui.Behaviors;
#if ANDROID
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.CloudMessaging.EventArgs;
#endif

namespace appmetepec.Views;

public partial class HomePage : ContentPage
{
    private readonly ReportCatalogService _catalog;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PreferencesService _preferences;
    private readonly PushRegistrationService _pushRegistration;
    private bool _bannerTimerStarted;
    private bool _isDrawerOpen;
    private bool _newsLoaded;

    public HomePage(ReportCatalogService catalog, MetepecApiService api, NavigationState navigationState, PreferencesService preferences, PushRegistrationService pushRegistration)
    {
        InitializeComponent();
        _catalog = catalog;
        _api = api;
        _navigationState = navigationState;
        _preferences = preferences;
        _pushRegistration = pushRegistration;
        DrawerVersionLabel.Text = $"v{AppInfo.Current.VersionString}";
        DarkThemeSwitch.IsToggled = _preferences.DarkThemeEnabled;
        SetActiveTab(reportsActive: true);
        BannerCarousel.ItemsSource = BuildBanners();
        LogoImage.Behaviors.Add(new TouchBehavior
        {
            LongPressDuration = 3000,
            LongPressCommand = new Command(OnLogoLongPressed)
        });

#if ANDROID
        // Firebase puede rotar el token del dispositivo en cualquier momento (no solo al
        // instalar la app); esta suscripcion vive mientras la app este viva, no solo en Home.
        CrossFirebaseCloudMessaging.Current.TokenChanged += OnFirebaseTokenChanged;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (CategoriesHost.Children.Count == 0)
        {
            RenderCategories(_catalog.GetCategories());
        }

        if (!_newsLoaded)
        {
            await LoadNewsAsync();
        }

        StartBannerTimer();
        await VerificarEncuestaExperienciaAsync();
        // El login ya intenta este mismo registro (ver LoginPage.OnLoginClicked); esta llamada es
        // el respaldo para cuando la app arranca ya logueada (sin pasar por LoginPage) o el intento
        // del login fallo en silencio. PushRegistrationService hace upsert por token en el back-end,
        // asi que repetirla en cada OnAppearing es inofensivo.
        await _pushRegistration.RegistrarSiAplicaAsync(_preferences.CiudadanoId);
    }

#if ANDROID
    private async void OnFirebaseTokenChanged(object? sender, FCMTokenChangedEventArgs e)
    {
        var idCiudadano = _preferences.CiudadanoId;
        if (idCiudadano <= 0) return;

        try
        {
            await _api.RegistrarDispositivoPushAsync(idCiudadano, e.Token, "ANDROID", DeviceInfo.Current.Model, AppInfo.Current.VersionString);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] No se pudo actualizar el token rotado: {ex}");
        }
    }
#endif

    private async Task VerificarEncuestaExperienciaAsync()
    {
        if (_preferences.CiudadanoId <= 0)
        {
            return;
        }

        try
        {
            var proximaFecha = _preferences.ProximaFechaEncuestaExperienciaApp;
            if (proximaFecha is not null && DateTime.UtcNow < proximaFecha.Value)
            {
                return;
            }

            var ciudadano = await _api.GetMyCiudadanoDetailsAsync();
            if (ciudadano?.FechaPrimerAcceso is null)
            {
                return;
            }

            var diasDeUso = (DateTime.UtcNow - ciudadano.FechaPrimerAcceso.Value).TotalDays;
            if (diasDeUso < AppConstants.DiasMinimosEncuestaExperienciaApp)
            {
                return;
            }

            var tipoEncuesta = await _api.GetTipoEncuestaByClaveAsync(AppConstants.ClaveEncuestaExperienciaApp);
            if (tipoEncuesta is null)
            {
                return;
            }

            var yaContestada = await _api.ExisteEncuestaCiudadanoAsync(_preferences.CiudadanoId, tipoEncuesta.Id);
            if (yaContestada)
            {
                return;
            }

            var deseaContestar = await DisplayAlert(
                "Tu opinion es importante",
                "Llevas varios dias usando la app Metepec *7311. ¿Nos ayudas contestando una breve encuesta?",
                "Si", "Ahora no");

            if (deseaContestar)
            {
                IrAEncuestaExperiencia();
            }
            else
            {
                _preferences.ProximaFechaEncuestaExperienciaApp = DateTime.UtcNow.AddDays(AppConstants.DiasCooldownEncuestaExperienciaApp);
            }
        }
        catch
        {
            // No bloquea la vista principal si falla la verificacion de encuesta.
        }
    }

    private void IrAEncuestaExperiencia()
    {
        _navigationState.SelectedTicket = null;
        _navigationState.EncuestaClave = AppConstants.ClaveEncuestaExperienciaApp;
        _ = Shell.Current.GoToAsync(nameof(EncuestaPage));
    }

    private async void OnLogoLongPressed()
    {
        var forzar = await DisplayAlert(
            "Modo pruebas",
            "¿Mostrar ahora la encuesta de experiencia de la app (sin esperar los 8 dias)?",
            "Mostrar ahora", "Cancelar");

        if (forzar)
        {
            IrAEncuestaExperiencia();
        }
    }

    private void StartBannerTimer()
    {
        if (_bannerTimerStarted)
        {
            return;
        }

        _bannerTimerStarted = true;
        Dispatcher.StartTimer(TimeSpan.FromSeconds(4), () =>
        {
            if (BannerCarousel.ItemsSource is not IReadOnlyList<BannerItem> banners || banners.Count == 0)
            {
                return true;
            }

            BannerCarousel.Position = (BannerCarousel.Position + 1) % banners.Count;
            return true;
        });
    }

    private void RenderCategories(IReadOnlyList<DependenciaCategoria> categories)
    {
        CategoriesHost.Children.Clear();

        foreach (var category in categories)
        {
            var categoryBlock = new VerticalStackLayout { Spacing = 10 };

            categoryBlock.Children.Add(new Label
            {
                Text = category.Nombre.ToUpperInvariant(),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#555555")
            });

            var itemsRow = new HorizontalStackLayout { Spacing = 6 };

            foreach (var report in category.Reportes)
            {
                itemsRow.Children.Add(CreateReportItem(report));
            }

            categoryBlock.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                Content = itemsRow
            });

            CategoriesHost.Children.Add(categoryBlock);
        }
    }

    private VerticalStackLayout CreateReportItem(ScreenReport report)
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 4,
            WidthRequest = 110,
            HorizontalOptions = LayoutOptions.Center,
            BindingContext = report
        };

        layout.Children.Add(new Image
        {
            Source = report.IconSource,
            HeightRequest = 100,
            WidthRequest = 100,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        });

        layout.Children.Add(new Label
        {
            Text = report.Title,
            FontSize = 12,
            TextColor = Color.FromArgb("#555555"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 4
        });

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenReportAsync(report);
        layout.GestureRecognizers.Add(tap);

        return layout;
    }

    private static IReadOnlyList<BannerItem> BuildBanners() =>
    [
        /*new(Color.FromArgb("#B8D927"), "btn_predial.png", "PREDIAL", "PAGO EN LINEA",
            Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA"),
        new(Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS",
            Color.FromArgb("#24AEE4"), "btn_visita_camion_basura.png", "MUEVETEX", "Transformamos la movilidad"),
        new(Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA",
            Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS")*/
        new(Color.FromArgb("#B8D927"), "btn_predial.png", "PREDIAL", "PAGO EN LINEA",
            Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA"),

        new(Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA",
            Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS"),

        new(Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS",
            Color.FromArgb("#B8D927"), "btn_predial.png", "PREDIAL", "PAGO EN LINEA")
        
    ];

    private NewsLetter? _featuredNews;

    // El primer intento de red justo despues del arranque en frio de la app a veces falla
    // (el stack de red/DNS del dispositivo todavia esta calentando), aunque la conexion si
    // funcione bien un par de segundos despues -- por eso "deslizar para refrescar" siempre lo
    // recupera. Reintentar aqui mismo evita que el ciudadano tenga que descubrir ese gesto.
    private const int MaxIntentosCargaNoticias = 3;

    private async Task LoadNewsAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            List<NewsLetter>? news = null;
            Exception? ultimoError = null;
            for (var intento = 1; intento <= MaxIntentosCargaNoticias && news is null; intento++)
            {
                try
                {
                    news = await _api.GetPublicacionesAsync();
                }
                catch (Exception ex)
                {
                    ultimoError = ex;
                    if (intento < MaxIntentosCargaNoticias)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(intento));
                    }
                }
            }

            if (news is null)
            {
                throw ultimoError!;
            }

            System.Diagnostics.Debug.WriteLine($"[Noticias] {news.Count} publicaciones cargadas.");
            ShowNews(news);
            _newsLoaded = true;
        }
        catch (Exception ex)
        {
            var status = (ex as HttpRequestException)?.StatusCode;
            System.Diagnostics.Debug.WriteLine($"[Noticias] Error al cargar publicaciones: {ex.GetType().Name} {status} - {ex.Message}");
            ShowNews([]);
            // _newsLoaded se queda en false a proposito: si el ciudadano vuelve a esta pantalla
            // mas tarde (navega a otra pantalla y regresa), OnAppearing reintenta solo, sin
            // depender de que descubra el gesto de "deslizar para refrescar".
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    // La primera noticia (marcada Destacada, o la mas reciente si ninguna lo esta) se
    // muestra como tarjeta grande con imagen de fondo; el resto en la lista compacta.
    private void ShowNews(List<NewsLetter> news)
    {
        _featuredNews = news.FirstOrDefault(n => n.destacada) ?? news.FirstOrDefault();

        if (_featuredNews is null)
        {
            FeaturedNewsCard.IsVisible = false;
            NewsView.IsVisible = true;
            NewsView.ItemsSource = news;
            return;
        }

        FeaturedNewsCard.IsVisible = true;
        FeaturedNewsImage.Source = _featuredNews.image;
        FeaturedNewsTitle.Text = _featuredNews.title;
        FeaturedNewsSubtitle.Text = BuildExcerpt(_featuredNews);

        // Si tras sacar la destacada no queda ninguna otra noticia, se oculta la lista por
        // completo: de lo contrario su EmptyView ("Sin noticias cargadas") se mostraria
        // aunque si haya una noticia (la destacada, ya visible arriba).
        var resto = news.Where(n => n != _featuredNews).ToList();
        NewsView.IsVisible = resto.Count > 0;
        NewsView.ItemsSource = resto;
    }

    // Debajo del titulo de la tarjeta destacada va el resumen; si la noticia no trae
    // resumen, se usa un fragmento del contenido (sin las etiquetas HTML) y "...Ver mas".
    private static string BuildExcerpt(NewsLetter news)
    {
        if (!string.IsNullOrWhiteSpace(news.subtitle))
        {
            return news.subtitle!;
        }

        var texto = StripHtml(news.content ?? news.shortContent ?? string.Empty);
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        const int maxLength = 90;
        var recortado = texto.Length > maxLength ? texto[..maxLength].TrimEnd() : texto;
        return $"{recortado}...Ver mas";
    }

    private static string StripHtml(string html) =>
        Regex.Replace(html, "<.*?>", string.Empty).Trim();

    private async void OnFeaturedNewsTapped(object sender, TappedEventArgs e)
    {
        if (_featuredNews is null) return;
        await AbrirNoticiaAsync(_featuredNews);
    }

    private async Task AbrirNoticiaAsync(NewsLetter news)
    {
        _navigationState.SelectedNews = news;
        await Shell.Current.GoToAsync(nameof(NewsDetailPage));
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadNewsAsync();
        ((RefreshView)sender).IsRefreshing = false;
    }

    private async void OnReportSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ScreenReport report)
        {
            return;
        }

        ((CollectionView)sender).SelectedItem = null;

        await OpenReportAsync(report);
    }

    private async Task OpenReportAsync(ScreenReport report)
    {
        if (report.Title == "Llamada")
        {
            await Launcher.Default.OpenAsync("tel:*7311");
            return;
        }

        if (report.IdSeccion == 10)
        {
            await Launcher.Default.OpenAsync(AppConstants.SamUrl);
            return;
        }

        if (report.IdSeccion == 11)
        {
            await Launcher.Default.OpenAsync(AppConstants.PrivacyUrl);
            return;
        }

        _navigationState.SelectedReport = report;
        await Shell.Current.GoToAsync(nameof(ReportPage));
    }

    private async void OnNewsSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NewsLetter news)
        {
            return;
        }

        NewsView.SelectedItem = null;
        await AbrirNoticiaAsync(news);
    }

    private async void OnRecoleccionClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecoleccionPage));
    }

    private async void OnAlertaClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AlertaNaranjaPage));
    }

    private async void OnAlertaTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AlertaNaranjaPage));
    }

    // Mismo numero que OpenReportAsync usa para el reporte "Llamada" (*7311), pero accesible
    // directo desde el encabezado sin tener que entrar a un reporte primero.
    private async void OnLlamarTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync("tel:*7311");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo iniciar la llamada", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    private async void OnMenuTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_isDrawerOpen)
            {
                await CloseDrawerAsync();
            }
            else
            {
                await OpenDrawerAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Menu] Error al abrir/cerrar el drawer: {ex}");
            await DisplayAlert("Menu", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    private async void OnDrawerBackdropTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
    }

    private async Task OpenDrawerAsync()
    {
        _isDrawerOpen = true;
        DrawerUserLabel.Text = string.IsNullOrWhiteSpace(_preferences.CurrentUser.Name)
            ? "Metepec *7311"
            : _preferences.CurrentUser.Name;
        DrawerBackdrop.IsVisible = true;
        DrawerBackdrop.Opacity = 0;
        await Task.WhenAll(
            DrawerBackdrop.FadeTo(1, 200),
            DrawerPanel.TranslateTo(0, 0, 220, Easing.CubicOut));
    }

    private async Task CloseDrawerAsync()
    {
        _isDrawerOpen = false;
        await Task.WhenAll(
            DrawerBackdrop.FadeTo(0, 180),
            DrawerPanel.TranslateTo(-272, 0, 200, Easing.CubicIn));
        DrawerBackdrop.IsVisible = false;
    }

    private async void OnDrawerHomeTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
    }

    private async void OnDrawerMyTicketsTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync(nameof(MyTicketsPage));
    }

    private async void OnDrawerRecoleccionTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync(nameof(RecoleccionPage));
    }

    private async void OnDrawerAlertaTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync(nameof(AlertaNaranjaPage));
    }

    private async void OnDrawerNoticiasTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
        OnNewsTabTapped(sender, e);
    }

    private void OnDarkThemeToggled(object sender, ToggledEventArgs e)
    {
        _preferences.DarkThemeEnabled = e.Value;
        Application.Current!.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
        SetActiveTab(reportsActive: ReportsContent.IsVisible);
    }

    private async void OnDrawerLogoutTapped(object sender, TappedEventArgs e)
    {
        await CloseDrawerAsync();
        var confirm = await DisplayAlert("Cerrar sesion", "Se cerrara tu sesion actual.", "Cerrar sesion", "Cancelar");
        if (!confirm)
        {
            return;
        }

        _preferences.Logout();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }

    private static bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;
    private static Color ActiveTabBackground => IsDarkTheme ? Color.FromArgb("#33294D") : Color.FromArgb("#F1EEFB");
    private static Color ActiveTabTextColor => IsDarkTheme ? Colors.White : Color.FromArgb("#28113E");
    private static Color InactiveTabTextColor => Color.FromArgb("#9AA5B1");

    private void SetActiveTab(bool reportsActive)
    {
        ReportsTab.BackgroundColor = reportsActive ? ActiveTabBackground : Colors.Transparent;
        NewsTab.BackgroundColor = reportsActive ? Colors.Transparent : ActiveTabBackground;
        ReportsTabLabel.TextColor = reportsActive ? ActiveTabTextColor : InactiveTabTextColor;
        NewsTabLabel.TextColor = reportsActive ? InactiveTabTextColor : ActiveTabTextColor;
    }

    private void OnReportsTabTapped(object sender, TappedEventArgs e)
    {
        ReportsContent.IsVisible = true;
        NewsContent.IsVisible = false;
        SetActiveTab(reportsActive: true);
    }

    private void OnNewsTabTapped(object sender, TappedEventArgs e)
    {
        ReportsContent.IsVisible = false;
        NewsContent.IsVisible = true;
        SetActiveTab(reportsActive: false);

        // El CollectionView recibe su ItemsSource en OnAppearing mientras NewsContent sigue oculto
        // (IsVisible=False), y MAUI no siempre relayoutea bien un CollectionView que estaba oculto
        // cuando se le asignaron los datos. Reasignar el ItemsSource ahora que ya es visible fuerza
        // el relayout sin volver a llamar a la API.
        var items = NewsView.ItemsSource;
        if (items is not null)
        {
            NewsView.ItemsSource = null;
            NewsView.ItemsSource = items;
        }
    }

    private sealed record BannerItem(
        Color LeftBackground,
        string LeftIcon,
        string LeftTitle,
        string LeftSubtitle,
        Color RightBackground,
        string RightIcon,
        string RightTitle,
        string RightSubtitle);
}
