using System.Globalization;
using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class NewsDetailPage : ContentPage
{
    private static readonly CultureInfo SpanishCulture = new("es-MX");

    private readonly NavigationState _navigationState;
    private readonly MetepecApiService _api;
    private readonly PreferencesService _preferences;
    private NewsLetter? _news;
    private string? _miReaccion;

    public NewsDetailPage(NavigationState navigationState, MetepecApiService api, PreferencesService preferences)
    {
        InitializeComponent();
        _navigationState = navigationState;
        _api = api;
        _preferences = preferences;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _news = _navigationState.SelectedNews;
        if (_news is null)
        {
            await Shell.Current.GoToAsync("..");
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

        var idCiudadano = _preferences.CiudadanoId;
        ReactionRow.IsVisible = idCiudadano > 0;

        _ = _api.RegistrarVistaPublicacionAsync(_news.id, idCiudadano);

        if (idCiudadano > 0)
        {
            _miReaccion = await _api.GetMiReaccionPublicacionAsync(_news.id, idCiudadano);
            ActualizarBotonesReaccion();
        }
    }

    // Alternar: tocar el mismo tipo que ya estaba activo quita la reaccion; tocar el otro la
    // reemplaza (solo se puede tener una reaccion activa por publicacion, ver PublicacionReaccion).
    private async Task ReaccionarAsync(string tipo)
    {
        if (_news is null) return;
        var idCiudadano = _preferences.CiudadanoId;
        if (idCiudadano <= 0) return;

        LikeButton.IsEnabled = false;
        DislikeButton.IsEnabled = false;

        try
        {
            if (string.Equals(_miReaccion, tipo, StringComparison.OrdinalIgnoreCase))
            {
                await _api.QuitarReaccionPublicacionAsync(_news.id, idCiudadano);
                _miReaccion = null;
            }
            else
            {
                _miReaccion = await _api.ReaccionarPublicacionAsync(_news.id, idCiudadano, tipo);
            }
        }
        finally
        {
            ActualizarBotonesReaccion();
            LikeButton.IsEnabled = true;
            DislikeButton.IsEnabled = true;
        }
    }

    private void ActualizarBotonesReaccion()
    {
        var esLike = string.Equals(_miReaccion, "Like", StringComparison.OrdinalIgnoreCase);
        var esDislike = string.Equals(_miReaccion, "Dislike", StringComparison.OrdinalIgnoreCase);

        LikeButton.TextColor = esLike ? Colors.White : Color.FromArgb("#28113E");
        LikeButton.BackgroundColor = esLike ? Color.FromArgb("#F89A1C") : Color.FromArgb("#F1EEFB");
        DislikeButton.TextColor = esDislike ? Colors.White : Color.FromArgb("#28113E");
        DislikeButton.BackgroundColor = esDislike ? Color.FromArgb("#F89A1C") : Color.FromArgb("#F1EEFB");
    }

    private async void OnLikeTapped(object sender, EventArgs e) => await ReaccionarAsync("Like");

    private async void OnDislikeTapped(object sender, EventArgs e) => await ReaccionarAsync("Dislike");

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
