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
    private bool? _ticketResuelto;

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

            // Si venimos con un ticket real, la pregunta de "¿se resolvio?" se muestra junto con
            // las demas preguntas de la encuesta (un solo formulario, un solo envio) en vez de
            // ser un paso aparte antes de la encuesta.
            ResueltoBorder.IsVisible = _ticket is not null;
            _ticketResuelto = null;
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
                var entryNumerico = new Entry { Keyboard = Keyboard.Numeric, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#333") };
                _numericaControles[pregunta.Id] = entryNumerico;
                contenedor.Children.Add(entryNumerico);
                break;
            default: // TEXTO
                var editorTexto = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 70, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#333") };
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
            var seleccionada = i < valor;
            estrellas[i].Text = seleccionada ? "★" : "☆";
            estrellas[i].TextColor = seleccionada ? Color.FromArgb("#F89A1C") : Colors.White;
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

    private void OnResueltoSiClicked(object sender, EventArgs e) => SeleccionarResuelto(true);

    private void OnResueltoNoClicked(object sender, EventArgs e) => SeleccionarResuelto(false);

    private void SeleccionarResuelto(bool resuelto)
    {
        _ticketResuelto = resuelto;
        ResueltoSiButton.BackgroundColor = resuelto ? Color.FromArgb("#4CAF50") : Color.FromArgb("#DDD");
        ResueltoSiButton.TextColor = resuelto ? Colors.White : Color.FromArgb("#28113E");
        ResueltoNoButton.BackgroundColor = resuelto == false ? Color.FromArgb("#C62828") : Color.FromArgb("#DDD");
        ResueltoNoButton.TextColor = resuelto == false ? Colors.White : Color.FromArgb("#28113E");
    }

    private async void OnEnviarClicked(object sender, EventArgs e)
    {
        if (_tipoEncuesta is null)
        {
            return;
        }

        if (_ticket is not null && _ticketResuelto is null)
        {
            await DisplayAlert("Falta un dato", "Indica si tu problema se resolvió antes de enviar.", "Aceptar");
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

            // El cierre/apertura del ticket se resuelve hasta aqui, junto con el envio de la
            // encuesta completa (un solo formulario, un solo submit) -- asi el ticket nunca se
            // cierra antes de que el ciudadano de verdad termine y envie la encuesta. Por ahora
            // no se reabren tickets desde la app (ReabrirTicketAsync se deja construido pero sin
            // usar aqui): si no se resolvio, se manda a levantar un reporte nuevo.
            if (_ticket is not null && _ticketResuelto == true)
            {
                try
                {
                    await _api.ConfirmarResolucionTicketAsync(_ticket.Id);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Encuesta enviada", $"Tu encuesta se registro, pero no se pudo confirmar el cierre del reporte: {ex.Message}", "Aceptar");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Gracias", "Tu encuesta fue registrada y tu reporte quedo cerrado.", "Aceptar");
                await Shell.Current.GoToAsync("..");
            }
            else if (_ticket is not null && _ticketResuelto == false)
            {
                await DisplayAlert("Gracias", "Tu encuesta fue registrada. Levanta un reporte nuevo para el problema que sigue pendiente.", "Aceptar");
                await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
            }
            else
            {
                await DisplayAlert("Gracias", "Tu encuesta fue registrada correctamente.", "Aceptar");
                await Shell.Current.GoToAsync("..");
            }
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

    private async void OnCancelarTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
