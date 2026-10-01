using appmetepec.Models;

namespace appmetepec.Services;

// Datos de EJEMPLO para desarrollar/probar el modulo de Eventos mientras el backend no
// expone /eventos. NO es dato real de produccion; se reemplaza por el API cuando exista
// (ver EventosService.UsarDatosDeEjemplo). Las posiciones posX/posY son porcentajes
// aproximados sobre una imagen de mapa; el admin real los marcara con precision.
internal static class EventosEjemplo
{
    // Imagen de muestra (el mapa real lo subira el admin). Se usa una imagen remota
    // generica para validar el render de hotspots + zoom en el dispositivo.
    private const string ImagenMapa = "https://raw.githubusercontent.com/shuitrongom/appmetepec/master/README-noexiste.png";

    public static List<BackendEventoDto> Listado() =>
    [
        new BackendEventoDto
        {
            Id = 1,
            Nombre = "Quimera 2026",
            Descripcion = "Festival Internacional Metepec Quimera. Toca un escenario en el mapa para ver su programación.",
            ImagenUrl = ImagenMapa,
            ImagenAncho = 1242,
            ImagenAlto = 1654,
            FechaInicio = Hoy.AddDays(-1),
            FechaFin = Hoy.AddDays(14),
            Activo = true,
            Orden = 1
        }
    ];

    public static BackendEventoDto? Detalle(int idEvento)
    {
        if (idEvento != 1)
        {
            return null;
        }

        var evento = Listado()[0];
        evento.Escenarios = Escenarios();
        return evento;
    }

    private static DateTime Hoy => DateTime.Today;

    private static List<BackendEscenarioDto> Escenarios() =>
    [
        new BackendEscenarioDto
        {
            Id = 2, Numero = 2, Nombre = "Escalinatas del Calvario",
            Direccion = "Av. Estado de México, Barrio del Espíritu Santo",
            PosX = 50, PosY = 16,
            Actividades =
            [
                new BackendActividadDto { Id = 1, Titulo = "Transe Express — Poupées Géantes", Descripcion = "Espectáculo circense desde Francia", Pais = "FR", Fecha = Hoy, HoraInicio = new TimeSpan(18,0,0) },
                new BackendActividadDto { Id = 2, Titulo = "Transe Express — Mobile home", Descripcion = "Espectáculo circense desde Francia", Pais = "FR", Fecha = Hoy, HoraInicio = new TimeSpan(19,0,0) },
                new BackendActividadDto { Id = 3, Titulo = "Compañía Aérea Nacional", Descripcion = "Danza aérea", Pais = "MX", Fecha = Hoy.AddDays(4), HoraInicio = new TimeSpan(20,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 1, Numero = 1, Nombre = "Plaza Juárez",
            Direccion = "Gral. José Vicente Villada, Espíritu Santo",
            PosX = 44, PosY = 32,
            Actividades =
            [
                new BackendActividadDto { Id = 4, Titulo = "Inauguración Quimera", Descripcion = "Ceremonia de apertura", Fecha = Hoy, HoraInicio = new TimeSpan(17,0,0) },
                new BackendActividadDto { Id = 5, Titulo = "Concierto de gala", Descripcion = "Orquesta sinfónica", Fecha = Hoy.AddDays(1), HoraInicio = new TimeSpan(19,30,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 3, Numero = 3, Nombre = "Teatro Quimera",
            Direccion = "Miguel Hidalgo s/n, Piso 1, Barrio del Espíritu Santo",
            PosX = 82, PosY = 25,
            Actividades =
            [
                new BackendActividadDto { Id = 6, Titulo = "Obra: El Principito", Descripcion = "Teatro para toda la familia", Fecha = Hoy.AddDays(2), HoraInicio = new TimeSpan(18,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 7, Numero = 7, Nombre = "Town Square Metepec",
            Direccion = "Av. Confort 1100, Col. Providencia",
            PosX = 20, PosY = 62,
            Actividades =
            [
                new BackendActividadDto { Id = 7, Titulo = "Festival gastronómico", Descripcion = "Cocina tradicional mexiquense", Fecha = Hoy, HoraInicio = new TimeSpan(13,0,0) },
                new BackendActividadDto { Id = 8, Titulo = "Música en vivo", Descripcion = "Grupos locales", Fecha = Hoy, HoraInicio = new TimeSpan(16,0,0) },
            ]
        },
    ];
}
