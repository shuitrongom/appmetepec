using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

// Autoservicio del ciudadano: ver/editar sus propios datos personales. Usa GET/PUT
// api/ciudadanos/me (el PUT solo acepta el subconjunto editable, ver ActualizarMiCiudadanoRequest
// en el back-end -- no se puede tocar Activo/Idusuario/Idestado/Curp desde aqui).
public partial class PerfilPage : ContentPage
{
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
                await DisplayAlert("Mi perfil", "No se encontró tu información de ciudadano.", "Aceptar");
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
            FechaNacimientoPicker.Date = ciudadano.Fechanacimiento ?? FechaNacimientoPicker.MaximumDate.AddYears(-18);

            var direccion = await _api.GetMyDireccionPrincipalAsync();
            DireccionEditor.Text = direccion?.Direccion;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Mi perfil", ErrorMessageHelper.Traducir(ex), "Aceptar");
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
            await DisplayAlert("Datos requeridos", "Captura al menos tu nombre y apellido paterno.", "Aceptar");
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
            Fechanacimiento = FechaNacimientoPicker.Date.ToString("yyyy-MM-dd")
        };

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            await _api.UpdateMyCiudadanoAsync(request);
            await _api.UpdateMyDireccionPrincipalAsync(new BackendActualizarMiDireccionRequest { Direccion = DireccionEditor.Text?.Trim() });
            await DisplayAlert("Mi perfil", "Tus datos se actualizaron correctamente.", "Aceptar");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Mi perfil", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
