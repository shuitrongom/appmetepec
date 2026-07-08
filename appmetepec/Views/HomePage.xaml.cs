using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class HomePage : ContentPage
{
    private readonly ReportCatalogService _catalog;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private bool _bannerTimerStarted;

    public HomePage(ReportCatalogService catalog, MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _catalog = catalog;
        _api = api;
        _navigationState = navigationState;
        BannerCarousel.ItemsSource = BuildBanners();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (CategoriesHost.Children.Count == 0)
        {
            RenderCategories(_catalog.GetCategories());
        }

        if (NewsView.ItemsSource is null)
        {
            await LoadNewsAsync();
        }

        StartBannerTimer();
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
        new(Color.FromArgb("#B8D927"), "btn_predial.png", "PREDIAL", "PAGO EN LINEA",
            Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA"),
        new(Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS",
            Color.FromArgb("#24AEE4"), "btn_visita_camion_basura.png", "MUEVETEX", "Transformamos la movilidad"),
        new(Color.FromArgb("#7BCDEB"), "ic_opdapas.png", "OPDAPAS", "PAGO EN LINEA",
            Color.FromArgb("#9B12B3"), "ic_denuncia_ciudadana.png", "DENUNCIAS", "CIUDADANAS")
    ];

    private async Task LoadNewsAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            await _api.LoadZendeskCredentialsAsync();
            var news = await _api.GetNewsAsync();
            NewsView.ItemsSource = news?.Newsletters ?? [];
        }
        catch
        {
            NewsView.ItemsSource = Array.Empty<NewsLetter>();
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
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
        _navigationState.SelectedNews = news;
        await Shell.Current.GoToAsync(nameof(NewsDetailPage));
    }

    private async void OnRecoleccionClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecoleccionPage));
    }

    private async void OnAlertaClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AlertaNaranjaPage));
    }

    private async void OnRecoleccionTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecoleccionPage));
    }

    private async void OnAlertaTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AlertaNaranjaPage));
    }

    private void OnReportsTabTapped(object sender, TappedEventArgs e)
    {
        ReportsContent.IsVisible = true;
        NewsContent.IsVisible = false;
        ReportsTab.BackgroundColor = Color.FromArgb("#050505");
        NewsTab.BackgroundColor = Color.FromArgb("#1A1A1A");
    }

    private void OnNewsTabTapped(object sender, TappedEventArgs e)
    {
        ReportsContent.IsVisible = false;
        NewsContent.IsVisible = true;
        ReportsTab.BackgroundColor = Color.FromArgb("#1A1A1A");
        NewsTab.BackgroundColor = Color.FromArgb("#050505");
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
