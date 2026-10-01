using System.Text.Json.Serialization;

namespace appmetepec.Models;

// Modelos del modulo de Eventos interactivos (mapa de escenarios). Los nombres JSON
// ([JsonPropertyName]) son la propuesta de contrato con el backend; si el API final
// usa otros nombres, basta ajustar estos atributos sin tocar la UI.

/// <summary>Evento (contenedor): Quimera, feria, etc. Uno o varios activos a la vez.</summary>
public sealed class BackendEventoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    // URL de la imagen interactiva (mapa de escenarios), en alta resolucion.
    [JsonPropertyName("imagenUrl")]
    public string ImagenUrl { get; set; } = "";

    // Dimensiones reales de la imagen (px): sirven para mantener la proporcion y ubicar
    // los hotspots por porcentaje sin deformar.
    [JsonPropertyName("imagenAncho")]
    public int ImagenAncho { get; set; }

    [JsonPropertyName("imagenAlto")]
    public int ImagenAlto { get; set; }

    [JsonPropertyName("fechaInicio")]
    public DateTime FechaInicio { get; set; }

    [JsonPropertyName("fechaFin")]
    public DateTime FechaFin { get; set; }

    // El admin lo apaga -> el backend deja de devolverlo -> desaparece de la app.
    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("orden")]
    public int Orden { get; set; }

    // Presentes en el detalle (GET /eventos/{id}); vacias en el listado (GET /eventos).
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

    // Color del escenario (lo define el admin en el front), en formato hex "#RRGGBB".
    // Permite identificar cada sede con su color propio (como el programa impreso). Si el
    // backend no lo manda, la app usa un color por defecto.
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    // Posicion del hotspot en PORCENTAJE sobre la imagen (0-100). Clave del diseno:
    // la app los dibuja relativos, asi funcionan en cualquier tamano y con zoom.
    [JsonPropertyName("posX")]
    public double PosX { get; set; }

    [JsonPropertyName("posY")]
    public double PosY { get; set; }

    [JsonPropertyName("actividades")]
    public List<BackendActividadDto> Actividades { get; set; } = [];
}

/// <summary>Una actividad/espectaculo en un escenario, un dia y una hora.</summary>
public sealed class BackendActividadDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = "";

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    // Pais opcional (para bandera/etiqueta, ej. "FR" para Francia como en el PDF).
    [JsonPropertyName("pais")]
    public string? Pais { get; set; }

    [JsonPropertyName("fecha")]
    public DateTime Fecha { get; set; }

    [JsonPropertyName("horaInicio")]
    public TimeSpan? HoraInicio { get; set; }

    [JsonPropertyName("horaFin")]
    public TimeSpan? HoraFin { get; set; }

    [JsonPropertyName("orden")]
    public int Orden { get; set; }
}
