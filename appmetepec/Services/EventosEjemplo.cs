using appmetepec.Models;

namespace appmetepec.Services;

// Datos de EJEMPLO para desarrollar/probar el modulo de Eventos mientras el backend no
// expone /eventos. NO es dato real de produccion; se reemplaza por el API cuando exista
// (ver EventosService.UsarDatosDeEjemplo). Las posiciones posX/posY son porcentajes
// aproximados sobre una imagen de mapa; el admin real los marcara con precision.
internal static class EventosEjemplo
{
    // Mapa de muestra incluido como recurso local de la app (el mapa real lo subira el
    // admin via backend). Debe existir el archivo:
    //   appmetepec/Resources/Images/mapa_eventos.png
    // (MAUI empaqueta Resources/Images/* automaticamente). Al ser recurso local, se ve sin
    // depender de internet. Para produccion, EventosService toma la imagenUrl del backend.
    private const string ImagenMapa = "mapa_eventos.png";

    public static List<BackendEventoDto> Listado() =>
    [
        new BackendEventoDto
        {
            Id = 1,
            Nombre = "Quimera 2026",
            Descripcion = "Festival Internacional Metepec Quimera. Toca un escenario en el mapa para ver su programación.",
            ImagenUrl = ImagenMapa,
            ImagenAncho = 616,
            ImagenAlto = 709,
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

    // Posiciones (posX/posY en %) aproximadas sobre el mapa isometrico de Metepec
    // (Resources/Images/mapa_eventos.png). Son de MUESTRA; el admin real las marcara con
    // precision al subir su mapa. Nombres/direcciones tomados del programa Q (12 escenarios).
    private static List<BackendEscenarioDto> Escenarios() =>
    [
        new BackendEscenarioDto
        {
            Id = 1, Numero = 1, Nombre = "Plaza Juárez",
            Direccion = "Gral. José Vicente Villada, Espíritu Santo",
            PosX = 52, PosY = 32,
            Actividades =
            [
                new BackendActividadDto { Id = 101, Titulo = "Inauguración Quimera", Descripcion = "Ceremonia de apertura", Fecha = Hoy, HoraInicio = new TimeSpan(17,0,0) },
                new BackendActividadDto { Id = 102, Titulo = "Concierto de gala", Descripcion = "Orquesta sinfónica", Pais = "MX", Fecha = Hoy.AddDays(1), HoraInicio = new TimeSpan(19,30,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 2, Numero = 2, Nombre = "Escalinatas del Calvario",
            Direccion = "Av. Estado de México, Barrio del Espíritu Santo",
            PosX = 50, PosY = 15,
            Actividades =
            [
                new BackendActividadDto { Id = 201, Titulo = "Transe Express — Poupées Géantes", Descripcion = "Espectáculo circense desde Francia", Pais = "FR", Fecha = Hoy, HoraInicio = new TimeSpan(18,0,0) },
                new BackendActividadDto { Id = 202, Titulo = "Transe Express — Mobile home", Descripcion = "Espectáculo circense desde Francia", Pais = "FR", Fecha = Hoy, HoraInicio = new TimeSpan(19,0,0) },
                new BackendActividadDto { Id = 203, Titulo = "Compañía Aérea Nacional", Descripcion = "Danza aérea", Pais = "MX", Fecha = Hoy.AddDays(4), HoraInicio = new TimeSpan(20,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 3, Numero = 3, Nombre = "Teatro Quimera",
            Direccion = "Miguel Hidalgo s/n, Piso 1, Barrio del Espíritu Santo",
            PosX = 85, PosY = 24,
            Actividades =
            [
                new BackendActividadDto { Id = 301, Titulo = "Obra: El Principito", Descripcion = "Teatro para toda la familia", Fecha = Hoy.AddDays(2), HoraInicio = new TimeSpan(18,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 4, Numero = 4, Nombre = "Museo del Barro",
            Direccion = "Av. Estado de México no. 10, Barrio de Santiaguito",
            PosX = 10, PosY = 24,
            Actividades =
            [
                new BackendActividadDto { Id = 401, Titulo = "Exposición de alfarería", Descripcion = "Arte tradicional metepequense", Fecha = Hoy, HoraInicio = new TimeSpan(10,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 5, Numero = 5, Nombre = "Librería del Fondo de Cultura Económica Isidro Fabela",
            Direccion = "Miguel Hidalgo s/n, Piso 2, Barrio del Espíritu Santo",
            PosX = 75, PosY = 20,
            Actividades =
            [
                new BackendActividadDto { Id = 501, Titulo = "Presentación de libro", Descripcion = "Charla con el autor", Fecha = Hoy.AddDays(1), HoraInicio = new TimeSpan(17,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 6, Numero = 6, Nombre = "Ex Convento Franciscano de San Juan Bautista",
            Direccion = "5 de Mayo, Barrio de Santiaguito",
            PosX = 34, PosY = 38,
            Actividades =
            [
                new BackendActividadDto { Id = 601, Titulo = "Concierto de cámara", Descripcion = "Música clásica en el claustro", Fecha = Hoy, HoraInicio = new TimeSpan(19,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 7, Numero = 7, Nombre = "Town Square Metepec",
            Direccion = "Av. Confort 1100, Col. Providencia",
            PosX = 18, PosY = 62,
            Actividades =
            [
                new BackendActividadDto { Id = 701, Titulo = "Festival gastronómico", Descripcion = "Cocina tradicional mexiquense", Fecha = Hoy, HoraInicio = new TimeSpan(13,0,0) },
                new BackendActividadDto { Id = 702, Titulo = "Música en vivo", Descripcion = "Grupos locales", Fecha = Hoy, HoraInicio = new TimeSpan(16,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 8, Numero = 8, Nombre = "Quimeritas | Mercado Artesanal",
            Direccion = "Miguel Hidalgo esq. Ignacio Allende, Barrio de Santa Cruz",
            PosX = 86, PosY = 50,
            Actividades =
            [
                new BackendActividadDto { Id = 801, Titulo = "Mercado de artesanías", Descripcion = "Artesanos locales todo el día", Fecha = Hoy, HoraInicio = new TimeSpan(11,0,0), HoraFin = new TimeSpan(21,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 9, Numero = 9, Nombre = "Casa de Cultura Enrique Bátiz Campbell",
            Direccion = "16 de Septiembre 95, San Jerónimo Chicahualco",
            PosX = 17, PosY = 90,
            Actividades =
            [
                new BackendActividadDto { Id = 901, Titulo = "Taller de pintura infantil", Descripcion = "Actividad para niños", Fecha = Hoy.AddDays(2), HoraInicio = new TimeSpan(11,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 10, Numero = 10, Nombre = "Casa de Cultura Leopoldo Flores Valdés",
            Direccion = "Miguel Hidalgo esq. con Reforma s/n, Santa María Magdalena Ocotitlán",
            PosX = 87, PosY = 70,
            Actividades =
            [
                new BackendActividadDto { Id = 1001, Titulo = "Exposición de muralismo", Descripcion = "Homenaje a Leopoldo Flores", Fecha = Hoy.AddDays(3), HoraInicio = new TimeSpan(10,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 11, Numero = 11, Nombre = "Casa de Cultura Carlos Olvera Avelar",
            Direccion = "Av. de Los Gobernadores s/n, Fracc. Rancho San Francisco",
            PosX = 62, PosY = 73,
            Actividades =
            [
                new BackendActividadDto { Id = 1101, Titulo = "Recital de poesía", Descripcion = "Voces de Metepec", Fecha = Hoy.AddDays(1), HoraInicio = new TimeSpan(18,0,0) },
            ]
        },
        new BackendEscenarioDto
        {
            Id = 12, Numero = 12, Nombre = "Casa de Cultura Margarita García Luna Ortega",
            Direccion = "Prolongación Josefa Ortiz de Domínguez s/n, San Bartolomé Tlaltelulco",
            PosX = 75, PosY = 86,
            Actividades =
            [
                new BackendActividadDto { Id = 1201, Titulo = "Danza folclórica", Descripcion = "Ballet regional", Fecha = Hoy.AddDays(2), HoraInicio = new TimeSpan(19,0,0) },
            ]
        },
    ];
}
