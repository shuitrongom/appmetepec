using appmetepec.Models;

namespace appmetepec.Services;

// Datos de EJEMPLO para desarrollar/probar el modulo de Eventos mientras se conecta el
// backend real (GET api/eventos). NO es dato de produccion; se reemplaza por el API cuando
// EventosUsarDatosDeEjemplo = false. Las actividades usan LINEAS con formato (negrita,
// cursiva, color, tamano) igual que el contrato real, para probar el render premium tal
// como se vera con datos del admin. Las posiciones posX/posY son porcentajes aproximados
// sobre la imagen de mapa; el admin real las marcara con precision.
internal static class EventosEjemplo
{
    // Mapa de muestra incluido como recurso local de la app (el mapa real lo subira el
    // admin via backend). Debe existir el archivo:
    //   appmetepec/Resources/Images/mapa_eventos.png
    // (MAUI empaqueta Resources/Images/* automaticamente). Al ser recurso local, se ve sin
    // depender de internet. Para produccion, EventosService toma imagenMapaUrl del backend.
    private const string ImagenMapa = "mapa_eventos.png";

    // Portada del evento. Si existe el recurso, la app muestra la intro animada una vez antes
    // del mapa. Debe existir el archivo appmetepec/Resources/Images/portada_eventos.png
    // (recurso local empaquetado por MAUI). Para datos reales, el backend manda imagenPortadaUrl.
    private const string? ImagenPortada = "portada_eventos.png";

    // Paleta por sede (coincide con el Color del escenario) para los titulos de cada bloque.
    private const string FuenteTitulo = "OpenSans-Bold";

    public static List<BackendEventoDto> Listado() =>
    [
        new BackendEventoDto
        {
            Id = 1,
            Nombre = "Quimera 2026",
            Descripcion = "Festival Internacional Metepec Quimera. Toca un escenario en el mapa para ver su programación.",
            ImagenMapaUrl = ImagenMapa,
            ImagenPortadaUrl = ImagenPortada,
            ImagenAncho = 616,
            ImagenAlto = 709,
            FechaInicio = Hoy.AddDays(-1),
            FechaFin = Hoy.AddDays(14),
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

    // --- Helpers para construir lineas con formato de forma legible ---

    // Titulo del bloque: negrita, con el color de la sede y un poco mas grande.
    private static BackendActividadLineaDto Titulo(string texto, string color) => new()
    {
        Texto = texto, Negrita = true, Color = color, Fuente = FuenteTitulo, Tamano = 16
    };

    // Subtitulo/descripcion: texto normal, gris, tamano medio.
    private static BackendActividadLineaDto Sub(string texto) => new()
    {
        Texto = texto, Color = "#555555", Tamano = 13.5
    };

    // Nota en cursiva (ej. compania, pais, procedencia).
    private static BackendActividadLineaDto Nota(string texto) => new()
    {
        Texto = texto, Cursiva = true, Color = "#777777", Tamano = 12.5
    };

    private static BackendActividadDto Act(int id, DateTime fecha, string? ini, string? fin,
        params BackendActividadLineaDto[] lineas) => new()
    {
        Id = id, Fecha = fecha, HoraInicio = ini, HoraFin = fin,
        Lineas = [.. lineas]
    };

    // Posiciones (posX/posY en %) aproximadas sobre el mapa isometrico de Metepec
    // (Resources/Images/mapa_eventos.png). Son de MUESTRA; el admin real las marcara con
    // precision. Nombres/direcciones tomados del programa Q (12 escenarios).
    private static List<BackendEscenarioDto> Escenarios() =>
    [
        new BackendEscenarioDto
        {
            Id = 1, Numero = 1, Nombre = "Plaza Juárez",
            Direccion = "Gral. José Vicente Villada, Espíritu Santo",
            Color = "#4CAF50", PosX = 41, PosY = 38,
            Actividades =
            [
                Act(101, Hoy, "17:00", null,
                    Titulo("Inauguración Quimera", "#4CAF50"),
                    Sub("Ceremonia de apertura del festival"),
                    Nota("Autoridades municipales y artistas invitados")),
                Act(102, Hoy.AddDays(1), "19:30", "21:00",
                    Titulo("Concierto de gala", "#4CAF50"),
                    Sub("Orquesta Sinfónica del Estado de México"),
                    Nota("México")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 2, Numero = 2, Nombre = "Escalinatas del Calvario",
            Direccion = "Av. Estado de México, Barrio del Espíritu Santo",
            Color = "#E91E8C", PosX = 44, PosY = 25,
            Actividades =
            [
                Act(201, Hoy, "18:00", null,
                    Titulo("Transe Express — Poupées Géantes", "#E91E8C"),
                    Sub("Espectáculo circense de gran formato"),
                    Nota("Francia")),
                Act(202, Hoy, "19:00", null,
                    Titulo("Transe Express — Mobile Home", "#E91E8C"),
                    Sub("Carrusel aéreo musical sobre el público"),
                    Nota("Francia")),
                Act(203, Hoy.AddDays(4), "20:00", null,
                    Titulo("Compañía Aérea Nacional", "#E91E8C"),
                    Sub("Danza aérea contemporánea"),
                    Nota("México")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 3, Numero = 3, Nombre = "Teatro Quimera",
            Direccion = "Miguel Hidalgo s/n, Piso 1, Barrio del Espíritu Santo",
            Color = "#7E57C2", PosX = 76, PosY = 30,
            Actividades =
            [
                Act(301, Hoy.AddDays(2), "18:00", "19:30",
                    Titulo("El Principito", "#7E57C2"),
                    Sub("Teatro para toda la familia"),
                    Nota("Compañía Teatro del Estado")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 4, Numero = 4, Nombre = "Museo del Barro",
            Direccion = "Av. Estado de México no. 10, Barrio de Santiaguito",
            Color = "#FF7043", PosX = 9, PosY = 30,
            Actividades =
            [
                Act(401, Hoy, "10:00", "18:00",
                    Titulo("Exposición de alfarería", "#FF7043"),
                    Sub("Arte tradicional metepequense"),
                    Nota("Entrada libre todo el día")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 5, Numero = 5, Nombre = "Librería del Fondo de Cultura Económica Isidro Fabela",
            Direccion = "Miguel Hidalgo s/n, Piso 2, Barrio del Espíritu Santo",
            Color = "#26C6DA", PosX = 66, PosY = 26,
            Actividades =
            [
                Act(501, Hoy.AddDays(1), "17:00", null,
                    Titulo("Presentación de libro", "#26C6DA"),
                    Sub("Charla y firma con el autor"),
                    Nota("Narrativa mexicana contemporánea")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 6, Numero = 6, Nombre = "Ex Convento Franciscano de San Juan Bautista",
            Direccion = "5 de Mayo, Barrio de Santiaguito",
            Color = "#9575CD", PosX = 29, PosY = 42,
            Actividades =
            [
                Act(601, Hoy, "19:00", "20:30",
                    Titulo("Concierto de cámara", "#9575CD"),
                    Sub("Música clásica en el claustro"),
                    Nota("Cuarteto de cuerdas")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 7, Numero = 7, Nombre = "Town Square Metepec",
            Direccion = "Av. Confort 1100, Col. Providencia",
            Color = "#5E35B1", PosX = 16, PosY = 60,
            Actividades =
            [
                Act(701, Hoy, "13:00", "22:00",
                    Titulo("Festival gastronómico", "#5E35B1"),
                    Sub("Cocina tradicional mexiquense"),
                    Nota("Más de 20 cocineras tradicionales")),
                Act(702, Hoy, "16:00", null,
                    Titulo("Música en vivo", "#5E35B1"),
                    Sub("Grupos locales")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 8, Numero = 8, Nombre = "Quimeritas | Mercado Artesanal",
            Direccion = "Miguel Hidalgo esq. Ignacio Allende, Barrio de Santa Cruz",
            Color = "#FFA726", PosX = 79, PosY = 52,
            Actividades =
            [
                Act(801, Hoy, "11:00", "21:00",
                    Titulo("Mercado de artesanías", "#FFA726"),
                    Sub("Artesanos locales todo el día"),
                    Nota("Barro, textiles y orfebrería")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 9, Numero = 9, Nombre = "Casa de Cultura Enrique Bátiz Campbell",
            Direccion = "16 de Septiembre 95, San Jerónimo Chicahualco",
            Color = "#7986CB", PosX = 17, PosY = 86,
            Actividades =
            [
                Act(901, Hoy.AddDays(2), "11:00", "13:00",
                    Titulo("Taller de pintura infantil", "#7986CB"),
                    Sub("Actividad para niñas y niños"),
                    Nota("Cupo limitado, registro en sitio")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 10, Numero = 10, Nombre = "Casa de Cultura Leopoldo Flores Valdés",
            Direccion = "Miguel Hidalgo esq. con Reforma s/n, Santa María Magdalena Ocotitlán",
            Color = "#5C6BC0", PosX = 82, PosY = 68,
            Actividades =
            [
                Act(1001, Hoy.AddDays(3), "10:00", "19:00",
                    Titulo("Exposición de muralismo", "#5C6BC0"),
                    Sub("Homenaje a Leopoldo Flores"),
                    Nota("Obra plástica y bocetos originales")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 11, Numero = 11, Nombre = "Casa de Cultura Carlos Olvera Avelar",
            Direccion = "Av. de Los Gobernadores s/n, Fracc. Rancho San Francisco",
            Color = "#42A5F5", PosX = 54, PosY = 70,
            Actividades =
            [
                Act(1101, Hoy.AddDays(1), "18:00", null,
                    Titulo("Recital de poesía", "#42A5F5"),
                    Sub("Voces de Metepec"),
                    Nota("Poetas locales e invitados")),
            ]
        },
        new BackendEscenarioDto
        {
            Id = 12, Numero = 12, Nombre = "Casa de Cultura Margarita García Luna Ortega",
            Direccion = "Prolongación Josefa Ortiz de Domínguez s/n, San Bartolomé Tlaltelulco",
            Color = "#26A69A", PosX = 73, PosY = 84,
            Actividades =
            [
                Act(1201, Hoy.AddDays(2), "19:00", "20:30",
                    Titulo("Danza folclórica", "#26A69A"),
                    Sub("Ballet regional"),
                    Nota("Repertorio del Estado de México")),
            ]
        },
    ];
}
