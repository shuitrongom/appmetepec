using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class TicketDetailPage : ContentPage
{
    private static readonly TimeSpan EsperaEncuestaPendiente = TimeSpan.FromMinutes(2);

    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private BackendTicketDto? _ticket;
    private bool _paginaVisible;
    private bool _encuestaPromptMostrado;
    private bool _abriendoEvidencia;
    private bool _volviendoDeGaleria;

    public TicketDetailPage(MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _api = api;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _paginaVisible = true;

        // Al cerrar el visor de fotos (modal) no se recarga el ticket ni se reinicia el temporizador.
        if (_volviendoDeGaleria)
        {
            _volviendoDeGaleria = false;
            return;
        }

        _ticket = _navigationState.SelectedTicket;
        if (_ticket is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        AsuntoLabel.Text = _ticket.Asunto;
        EstatusLabel.Text = _ticket.Descestatus ?? "";
        EstatusChip.BackgroundColor = TryParseColor(_ticket.Colorestatus) ?? Color.FromArgb("#8A9BA8");
        ServicioLabel.Text = _ticket.Descservicio ?? "";
        DependenciaLabel.Text = _ticket.Dependencia ?? "";
        // Folio consecutivo del ticket (el Id es interno; tickets anteriores tienen Folio = Id).
        FolioLabel.Text = $"Folio {_ticket.FolioMostrar}  •  {_ticket.Fechaalta:dd/MM/yyyy HH:mm}";

        await LoadObservacionesAsync(_ticket.Id);
        IniciarTemporizadorEncuestaPendiente();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _paginaVisible = false;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = SalirConEncuestaPendienteAsync();
        return true;
    }

    // Ya no se muestra la encuesta al entrar al detalle: se espera EsperaEncuestaPendiente
    // mientras el ciudadano sigue en la pantalla, o se dispara antes al intentar regresar
    // (boton de la app o boton fisico de Android), lo que ocurra primero.
    private void IniciarTemporizadorEncuestaPendiente()
    {
        Dispatcher.StartTimer(EsperaEncuestaPendiente, () =>
        {
            if (_paginaVisible)
            {
                _ = VerificarEncuestaPendienteAsync();
            }

            return false;
        });
    }

    private async Task SalirConEncuestaPendienteAsync()
    {
        var abrioEncuesta = await VerificarEncuestaPendienteAsync();
        if (!abrioEncuesta)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task<bool> VerificarEncuestaPendienteAsync()
    {
        if (_encuestaPromptMostrado)
        {
            return false;
        }

        if (_ticket is null ||
            !string.Equals(_ticket.Claveestatus, AppConstants.ClaveEstatusResuelto, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var tipoEncuesta = await _api.GetTipoEncuestaByClaveAsync(AppConstants.ClaveEncuestaSolucionTicket);
            if (tipoEncuesta is null)
            {
                return false;
            }

            var yaContestada = await _api.ExisteEncuestaTicketAsync(_ticket.Id, tipoEncuesta.Id);
            if (yaContestada)
            {
                return false;
            }

            _encuestaPromptMostrado = true;

            // Se navega directo a la encuesta: ahi mismo se pregunta "¿Tu problema se
            // resolvio?" antes de las preguntas de satisfaccion (con opcion de "Ahora no"),
            // asi que preguntar aqui tambien era redundante.
            _navigationState.EncuestaClave = AppConstants.ClaveEncuestaSolucionTicket;
            await Shell.Current.GoToAsync(nameof(EncuestaPage));
            return true;
        }
        catch
        {
            // No bloquea la vista del ticket si falla la verificacion de encuesta.
            return false;
        }
    }

    private async Task LoadObservacionesAsync(int idTicket)
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var observaciones = await _api.GetTicketObservacionesAsync(idTicket);
            BindableLayout.SetItemsSource(ObservacionesView, observaciones
                .Where(obs => string.Equals(obs.Clavetipomensaje, AppConstants.ClaveRespuestaPublica, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }
        catch (Exception ex)
        {
            BindableLayout.SetItemsSource(ObservacionesView, Array.Empty<BackendTicketObservacionDto>());
            await DisplayAlert("No se pudieron cargar las respuestas", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await SalirConEncuestaPendienteAsync();
    }

    // Fotos: se abren en el visor de galeria de la app (GaleriaEvidenciasPage) con todas las fotos
    // de esa respuesta. Otros archivos (video, PDF...) se abren con la app del sistema.
    private async void OnEvidenciaTapped(object sender, TappedEventArgs e)
    {
        if (_abriendoEvidencia ||
            sender is not Element elemento ||
            elemento.BindingContext is not BackendTicketObservacionEvidenciaDto evidencia)
        {
            return;
        }

        _abriendoEvidencia = true;
        try
        {
            if (EsImagen(evidencia))
            {
                // La respuesta (observacion) es el BindingContext de algun contenedor superior.
                var observacion = BuscarObservacion(elemento);
                var fotos = (observacion?.Evidencias ?? [evidencia]).Where(EsImagen).ToList();
                var indice = Math.Max(fotos.IndexOf(evidencia), 0);
                _volviendoDeGaleria = true;
                await Navigation.PushModalAsync(new GaleriaEvidenciasPage(fotos, indice));
                return;
            }

            if (Uri.TryCreate(evidencia.RutaArchivo, UriKind.Absolute, out var uri))
            {
                await Launcher.Default.OpenAsync(uri);
            }
        }
        catch
        {
            _volviendoDeGaleria = false;
            await DisplayAlert("No se pudo abrir", "No se encontró una aplicación para abrir el archivo.", "Aceptar");
        }
        finally
        {
            _abriendoEvidencia = false;
        }
    }

    // Toque sobre el texto de una respuesta: abre su enlace, o deja elegir si trae varios.
    private async void OnRespuestaTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not BackendTicketObservacionDto observacion)
        {
            return;
        }

        var enlaces = observacion.ObservacionesSegmentos
            .Where(s => s.Url is not null && Uri.TryCreate(s.Url, UriKind.Absolute, out _))
            .GroupBy(s => s.Url!)
            .Select(g => g.First())
            .ToList();
        if (enlaces.Count == 0)
        {
            return;
        }

        var elegido = enlaces[0];
        if (enlaces.Count > 1)
        {
            var textos = enlaces.Select(s => s.Texto.Trim()).ToArray();
            var opcion = await DisplayActionSheet("Abrir enlace", "Cancelar", null, textos);
            var indice = Array.IndexOf(textos, opcion);
            if (indice < 0)
            {
                return;
            }
            elegido = enlaces[indice];
        }

        try
        {
            await Launcher.Default.OpenAsync(new Uri(elegido.Url!));
        }
        catch
        {
            await DisplayAlert("No se pudo abrir", "No se encontró una aplicación para abrir el enlace.", "Aceptar");
        }
    }

    private static BackendTicketObservacionDto? BuscarObservacion(Element? elemento)
    {
        while (elemento is not null)
        {
            if (elemento.BindingContext is BackendTicketObservacionDto observacion) return observacion;
            elemento = elemento.Parent;
        }
        return null;
    }

    private static readonly string[] ExtensionesImagen = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic"];

    // Algunas fotos se guardan con TipoMime generico ("application/octet-stream": el picker del
    // telefono no informo el tipo, o se adjunto desde la web) y antes se abrian en el navegador
    // (descarga) en vez del visor. Ahora basta con que el tipo sea image/* O que el archivo
    // (ruta o nombre) tenga extension de imagen.
    private static bool EsImagen(BackendTicketObservacionEvidenciaDto evidencia) =>
        evidencia.TipoMime?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ||
        TieneExtensionImagen(evidencia.RutaArchivo) ||
        TieneExtensionImagen(evidencia.NombreArchivo);

    private static bool TieneExtensionImagen(string? archivo) =>
        !string.IsNullOrWhiteSpace(archivo) &&
        ExtensionesImagen.Contains(Path.GetExtension(archivo.Split('?')[0]), StringComparer.OrdinalIgnoreCase);

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return null;
        }
    }
}
