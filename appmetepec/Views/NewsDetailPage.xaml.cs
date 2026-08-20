using System.Globalization;
using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class NewsDetailPage : ContentPage
{
    private static readonly CultureInfo SpanishCulture = new("es-MX");

    private readonly NavigationState _navigationState;
    private NewsLetter? _news;

    public NewsDetailPage(NavigationState navigationState)
    {
        InitializeComponent();
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _news = _navigationState.SelectedNews;
        if (_news is null)
        {
            Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = _news.title;
        ContentLabel.Text = _news.content ?? _news.shortContent;
        OpenUrlButton.IsVisible = !string.IsNullOrWhiteSpace(_news.url);

        var tieneResumen = !string.IsNullOrWhiteSpace(_news.subtitle);
        SubtitleBlock.IsVisible = tieneResumen;
        SubtitleLabel.Text = _news.subtitle;

        var textoFecha = FormatearFechaEvento(_news.fechaInicioEvento, _news.fechaFinEvento);
        EventDateRow.IsVisible = textoFecha is not null;
        EventDateLabel.Text = textoFecha;

        if (!string.IsNullOrWhiteSpace(_news.image))
        {
            NewsImage.Source = ImageSource.FromUri(new Uri(_news.image));
            NewsImage.IsVisible = true;
        }
    }

    // FechaInicioEvento/FechaFinEvento son las fechas del evento que anuncia la publicacion
    // (no la fecha de publicacion). Si ambas existen y difieren se muestra como rango.
    private static string? FormatearFechaEvento(DateTime? inicio, DateTime? fin)
    {
        if (inicio is null) return null;

        var textoInicio = inicio.Value.ToString("d 'de' MMMM, yyyy", SpanishCulture);
        if (fin is null || fin.Value.Date == inicio.Value.Date)
        {
            return textoInicio;
        }

        var textoFin = fin.Value.ToString("d 'de' MMMM, yyyy", SpanishCulture);
        return $"Del {textoInicio} al {textoFin}";
    }

    private async void OnOpenUrlClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_news?.url))
        {
            await Launcher.Default.OpenAsync(_news.url);
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
