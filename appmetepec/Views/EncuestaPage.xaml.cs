using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class EncuestaPage : ContentPage
{
    private readonly MetepecApiService _api;
    private readonly PreferencesService _preferences;
    private readonly NavigationState _navigationState;

    private BackendTicketDto? _ticket;
    private BackendTipoEncuestaDto? _tipoEncuesta;
    private bool _quiereAbrirNuevo;

    private readonly Dictionary<int, int> _calificaciones = new();
    private readonly Dictionary<int, bool> _siNoRespuestas = new();
    private readonly Dictionary<int, Editor> _textoControles = new();
    private readonly Dictionary<int, Entry> _numericaControles = new();
    private readonly Dictionary<int, List<Label>> _estrellasControles = new();

    public EncuestaPage(MetepecApiService api, PreferencesService preferences, NavigationState navigationState)
    {
        InitializeComponent();
        _api = api;
        _preferences = preferences;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _ticket = _navigationState.SelectedTicket;
        if (_ticket is null && _preferences.CiudadanoId <= 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await CargarPreguntasAsync();
    }

    private async Task CargarPreguntasAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var clave = _navigationState.EncuestaClave ?? AppConstants.ClaveEncuestaSolucionTicket;
            _tipoEncuesta = await _api.GetTipoEncuestaByClaveAsync(clave);
            if (_tipoEncuesta is null)
            {
                await DisplayAlert("Encuesta no disponible", "No se encontro configurada la encuesta de solucion de reportes.", "Aceptar");
                await Shell.Current.GoToAsync("..");
                return;
            }

            TituloLabel.Text = _tipoEncuesta.Nombre;

            var preguntas = await _api.GetEncuestaPreguntasAsync(_tipoEncuesta.Id);
            PreguntasContainer.Children.Clear();
            _calificaciones.Clear();
            _siNoRespuestas.Clear();
            _textoControles.Clear();
            _numericaControles.Clear();
            _estrellasControles.Clear();

            foreach (var pregunta in preguntas)
            {
                PreguntasContainer.Children.Add(CrearControlPregunta(pregunta));
            }

            // Si venimos con un ticket real, primero se pregunta si de verdad se resolvio antes
            // de dejar calificar la atencion. Sin ticket (entrada legacy), se muestra la encuesta
            // directo, igual que antes.
            if (_ticket is not null)
            {
                CierreContainer.IsVisible = true;
                EncuestaContenidoContainer.IsVisible = false;
            }
            else
            {
                CierreContainer.IsVisible = false;
                EncuestaContenidoContainer.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo cargar la encuesta", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private View CrearControlPregunta(BackendEncuestaPreguntaDto pregunta)
    {
        var contenedor = new VerticalStackLayout { Spacing = 8 };
        contenedor.Children.Add(new Label
        {
            Text = pregunta.Pregunta,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        switch (pregunta.TipoRespuesta.Trim().ToUpperInvariant())
        {
            case "ESTRELLAS":
                contenedor.Children.Add(CrearControlEstrellas(pregunta.Id));
                break;
            case "SI_NO":
                contenedor.Children.Add(CrearControlSiNo(pregunta.Id));
                break;
            case "NUMERICA":
                var entryNumerico = new Entry { Keyboard = Keyboard.Numeric, BackgroundColor = Colors.White };
                _numericaControles[pregunta.Id] = entryNumerico;
                contenedor.Children.Add(entryNumerico);
                break;
            default: // TEXTO
                var editorTexto = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 70, BackgroundColor = Colors.White };
                _textoControles[pregunta.Id] = editorTexto;
                contenedor.Children.Add(editorTexto);
                break;
        }

        return contenedor;
    }

    private View CrearControlEstrellas(int idPregunta)
    {
        var fila = new HorizontalStackLayout { Spacing = 6 };
        var estrellas = new List<Label>();

        for (var i = 1; i <= 5; i++)
        {
            var valor = i;
            var estrella = new Label
            {
                Text = "☆",
                FontSize = 32,
                TextColor = Colors.White
            };
            estrella.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => SeleccionarCalificacion(idPregunta, valor))
            });
            estrellas.Add(estrella);
            fila.Children.Add(estrella);
        }

        _estrellasControles[idPregunta] = estrellas;
        return fila;
    }

    private void SeleccionarCalificacion(int idPregunta, int valor)
    {
        _calificaciones[idPregunta] = valor;
        var estrellas = _estrellasControles[idPregunta];
        for (var i = 0; i < estrellas.Count; i++)
        {
            estrellas[i].Text = i < valor ? "★" : "☆";
        }
    }

    private View CrearControlSiNo(int idPregunta)
    {
        var fila = new HorizontalStackLayout { Spacing = 10 };

        var siButton = new Button { Text = "Si", BackgroundColor = Color.FromArgb("#DDD"), TextColor = Color.FromArgb("#28113E"), WidthRequest = 100 };
        var noButton = new Button { Text = "No", BackgroundColor = Color.FromArgb("#DDD"), TextColor = Color.FromArgb("#28113E"), WidthRequest = 100 };

        siButton.Clicked += (_, _) => SeleccionarSiNo(idPregunta, true, siButton, noButton);
        noButton.Clicked += (_, _) => SeleccionarSiNo(idPregunta, false, siButton, noButton);

        fila.Children.Add(siButton);
        fila.Children.Add(noButton);
        return fila;
    }

    private void SeleccionarSiNo(int idPregunta, bool valor, Button siButton, Button noButton)
    {
        _siNoRespuestas[idPregunta] = valor;
        siButton.BackgroundColor = valor ? Color.FromArgb("#F89A1C") : Color.FromArgb("#DDD");
        siButton.TextColor = valor ? Colors.White : Color.FromArgb("#28113E");
        noButton.BackgroundColor = !valor ? Color.FromArgb("#F89A1C") : Color.FromArgb("#DDD");
        noButton.TextColor = !valor ? Colors.White : Color.FromArgb("#28113E");
    }

    private async void OnEnviarClicked(object sender, EventArgs e)
    {
        if (_tipoEncuesta is null)
        {
            return;
        }

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var respuestas = new List<BackendEncuestaRespuestaItemRequest>();

            foreach (var (idPregunta, valor) in _calificaciones)
            {
                respuestas.Add(new BackendEncuestaRespuestaItemRequest { IdPregunta = idPregunta, RespuestaNumero = valor });
            }

            foreach (var (idPregunta, valor) in _siNoRespuestas)
            {
                respuestas.Add(new BackendEncuestaRespuestaItemRequest { IdPregunta = idPregunta, RespuestaBool = valor });
            }

            foreach (var (idPregunta, entry) in _numericaControles)
            {
                if (int.TryParse(entry.Text, out var numero))
                {
                    respuestas.Add(new BackendEncuestaRespuestaItemRequest { IdPregunta = idPregunta, RespuestaNumero = numero });
                }
            }

            foreach (var (idPregunta, editor) in _textoControles)
            {
                if (!string.IsNullOrWhiteSpace(editor.Text))
                {
                    respuestas.Add(new BackendEncuestaRespuestaItemRequest { IdPregunta = idPregunta, RespuestaTexto = editor.Text.Trim() });
                }
            }

            var request = new BackendSubmitEncuestaRequest
            {
                IdTipoEncuesta = _tipoEncuesta.Id,
                IdTicket = _ticket?.Id,
                IdCiudadano = _preferences.CiudadanoId > 0 ? _preferences.CiudadanoId : _ticket?.Idciudadano,
                Canal = _ticket is null ? "APP_MOVIL" : "RESOLUCION",
                CalificacionGeneral = _calificaciones.Values.Count > 0 ? _calificaciones.Values.First() : null,
                Comentario = string.IsNullOrWhiteSpace(ComentarioEditor.Text) ? null : ComentarioEditor.Text.Trim(),
                Respuestas = respuestas
            };

            await _api.SubmitEncuestaAsync(request);
            await DisplayAlert("Gracias", "Tu encuesta fue registrada correctamente.", "Aceptar");
            await SalirDeEncuestaAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo enviar la encuesta", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    // Si venian de "Abrir nuevo", los mandamos a levantar el reporte nuevo al terminar (o
    // saltarse) la encuesta; si no, se comporta igual que antes (regresa al detalle del ticket).
    private async Task SalirDeEncuestaAsync()
    {
        if (_quiereAbrirNuevo)
        {
            await DisplayAlert("Levanta un reporte nuevo", "Ahora levanta un reporte nuevo para el problema que sigue pendiente.", "Entendido");
            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private async void OnConfirmarResolucionClicked(object sender, EventArgs e)
    {
        if (_ticket is null) return;

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            await _api.ConfirmarResolucionTicketAsync(_ticket.Id);
            CierreContainer.IsVisible = false;
            EncuestaContenidoContainer.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo confirmar", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    // Por ahora no se reabren tickets desde la app: si el problema sigue, se levanta un
    // reporte nuevo. La capacidad de reabrir (ReabrirTicketAsync/api/tickets/{id}/reabrir)
    // se deja construida pero sin usar aqui, por si mas adelante se decide habilitarla.
    // El ticket se queda como esta (no se confirma ni se reabre); solo se desbloquea la
    // encuesta para que la puedan responder igual, y al salir se les manda a levantar el
    // reporte nuevo en vez de regresar al detalle del ticket.
    private void OnAbrirNuevoClicked(object sender, EventArgs e)
    {
        _quiereAbrirNuevo = true;
        CierreContainer.IsVisible = false;
        EncuestaContenidoContainer.IsVisible = true;
    }

    private async void OnCancelarTapped(object sender, TappedEventArgs e)
    {
        await SalirDeEncuestaAsync();
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await SalirDeEncuestaAsync();
    }
}
