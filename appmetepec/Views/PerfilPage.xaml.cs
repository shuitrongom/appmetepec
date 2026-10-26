using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

// Autoservicio del ciudadano: ver/editar sus propios datos personales. Usa GET/PUT
// api/ciudadanos/me (el PUT solo acepta el subconjunto editable, ver ActualizarMiCiudadanoRequest
// en el back-end -- no se puede tocar Activo/Idusuario/Idestado/Curp desde aqui).
public partial class PerfilPage : ContentPage
{
    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

    private readonly MetepecApiService _api;

    public PerfilPage(MetepecApiService api)
    {
        InitializeComponent();
        _api = api;
        FechaNacimientoPicker.MaximumDate = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarPerfilAsync();
    }

    private async Task CargarPerfilAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var ciudadano = await _api.GetMyCiudadanoDetailsAsync();
            if (ciudadano is null)
            {
                await AvisoAsync("No encontramos tu perfil", "No se encontró tu información de ciudadano.", nota: "Cierra sesión y vuelve a entrar; si continúa, contáctanos.", colores: DialogoPage.Rojo);
                await Shell.Current.GoToAsync("..");
                return;
            }

            NombreEntry.Text = ciudadano.Nombre;
            ApaternoEntry.Text = ciudadano.Apaterno;
            AmaternoEntry.Text = ciudadano.Amaterno;
            TelefonoMovilEntry.Text = ciudadano.Telefonomovil;
            WhatsappEntry.Text = ciudadano.Whatsapp;
            CorreoEntry.Text = ciudadano.Correoelectronico;
            CurpEntry.Text = ciudadano.Curp;
            CodigoPostalEntry.Text = ciudadano.Codigopostal;
            // MAUI 10: DatePicker.Date / MaximumDate son DateTime? (nulos).
            FechaNacimientoPicker.Date = ciudadano.Fechanacimiento
                ?? (FechaNacimientoPicker.MaximumDate ?? DateTime.Today).AddYears(-18);

            var direccion = await _api.GetMyDireccionPrincipalAsync();
            DireccionEditor.Text = direccion?.Direccion;
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo cargar tu perfil", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        var nombre = NombreEntry.Text?.Trim() ?? "";
        var apaterno = ApaternoEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apaterno))
        {
            await AvisoAsync("Datos incompletos", "Captura al menos tu nombre y tu apellido paterno.");
            return;
        }

        var request = new BackendActualizarMiCiudadanoRequest
        {
            Nombre = nombre,
            Apaterno = apaterno,
            Amaterno = AmaternoEntry.Text?.Trim(),
            Curp = CurpEntry.Text?.Trim(),
            Telefonomovil = TelefonoMovilEntry.Text?.Trim(),
            Whatsapp = WhatsappEntry.Text?.Trim(),
            Codigopostal = CodigoPostalEntry.Text?.Trim(),
            // MAUI 10: DatePicker.Date es DateTime? (nulo si no se eligio fecha).
            Fechanacimiento = FechaNacimientoPicker.Date?.ToString("yyyy-MM-dd")
        };

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            await _api.UpdateMyCiudadanoAsync(request);
            await _api.UpdateMyDireccionPrincipalAsync(new BackendActualizarMiDireccionRequest { Direccion = DireccionEditor.Text?.Trim() });
            await AvisoAsync("¡Datos actualizados!", "Tus datos se guardaron correctamente.", icono: "✓", colores: DialogoPage.Verde);
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudieron guardar tus datos", ErrorMessageHelper.Traducir(ex), colores: DialogoPage.Rojo);
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
