using System.Text.Json.Serialization;

namespace appmetepec.Models;

// Modelos del modulo de Eventos interactivos (mapa de escenarios) para la app movil.
//
// Contrato ALINEADO con el endpoint publico real del backend: GET api/eventos y
// GET api/eventos/{id} (EventoPublicoController -> EventoPublicoDto). Es un DTO de LECTURA
// limpio: sin metadatos de auditoria y con las horas ya formateadas como "HH:mm" (string),
// no como TimeSpan. El contenido de cada actividad NO es texto plano: es una lista de
// LINEAS con formato propio (negrita, cursiva, color, fuente, tamano), para reproducir
// fielmente el programa impreso (titulo en negrita y color, subtitulo en cursiva, etc.).

/// <summary>Evento (contenedor): Quimera, feria, etc. Uno o varios activos a la vez.</summary>
public sealed class BackendEventoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    // Imagen interactiva (mapa de escenarios) en alta resolucion. En el detalle siempre
    // viene; es la base sobre la que se posicionan los hotspots por porcentaje.
    [JsonPropertyName("imagenMapaUrl")]
    public string ImagenMapaUrl { get; set; } = "";

    // Portada opcional del evento: si viene, la app muestra una intro animada una sola vez
    // al abrir el evento y luego pasa al mapa. Si es null/vacia, se va directo al mapa.
    [JsonPropertyName("imagenPortadaUrl")]
    public string? ImagenPortadaUrl { get; set; }

    // Dimensiones reales de la imagen del mapa (px): mantienen la proporcion y permiten
    // ubicar los hotspots por porcentaje sin deformar.
    [JsonPropertyName("imagenAncho")]
    public int ImagenAncho { get; set; }

    [JsonPropertyName("imagenAlto")]
    public int ImagenAlto { get; set; }

    [JsonPropertyName("fechaInicio")]
    public DateTime FechaInicio { get; set; }

    [JsonPropertyName("fechaFin")]
    public DateTime FechaFin { get; set; }

    [JsonPropertyName("orden")]
    public int Orden { get; set; }

    // Presentes en el detalle (GET api/eventos/{id}); vacias en el listado (GET api/eventos).
    [JsonPropertyName("escenarios")]
    public List<BackendEscenarioDto> Escenarios { get; set; } = [];
}

/// <summary>Cada numero marcado sobre la imagen (una sede/escenario).</summary>
public sealed class BackendEscenarioDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    // Color del escenario (lo define el admin), hex "#RRGGBB". Identifica cada sede con su
    // color propio (como el programa impreso). Si el backend no lo manda, la app usa un
    // color por defecto.
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    // Posicion del hotspot en PORCENTAJE sobre la imagen (0-100): la app los dibuja
    // relativos, asi funcionan en cualquier tamano y con zoom.
    [JsonPropertyName("posX")]
    public double PosX { get; set; }

    [JsonPropertyName("posY")]
    public double PosY { get; set; }

    [JsonPropertyName("actividades")]
    public List<BackendActividadDto> Actividades { get; set; } = [];
}

/// <summary>Una actividad/espectaculo en un escenario, un dia y (opcionalmente) una hora.</summary>
public sealed class BackendActividadDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fecha")]
    public DateTime Fecha { get; set; }

    // Horas ya formateadas "HH:mm" (o null) tal como las entrega el backend publico.
    // La app NO parsea a TimeSpan: las usa directo para mostrar.
    [JsonPropertyName("horaInicio")]
    public string? HoraInicio { get; set; }

    [JsonPropertyName("horaFin")]
    public string? HoraFin { get; set; }

    // Contenido de la actividad: lista de lineas con formato (titulo, subtitulo, etc.),
    // en orden. Reemplaza al antiguo titulo/descripcion/pais de texto plano.
    [JsonPropertyName("lineas")]
    public List<BackendActividadLineaDto> Lineas { get; set; } = [];
}

/// <summary>Una linea de texto con formato dentro de una actividad.</summary>
public sealed class BackendActividadLineaDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("texto")]
    public string Texto { get; set; } = "";

    [JsonPropertyName("negrita")]
    public bool Negrita { get; set; }

    [JsonPropertyName("cursiva")]
    public bool Cursiva { get; set; }

    // Color del texto hex "#RRGGBB" (o null -> color por defecto del tema).
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    // Familia tipografica opcional (nombre de la fuente). Si es null, usa la del sistema.
    [JsonPropertyName("fuente")]
    public string? Fuente { get; set; }

    // Tamano de fuente opcional (puntos). Si es null, usa el tamano base de la linea.
    [JsonPropertyName("tamano")]
    public double? Tamano { get; set; }
}
