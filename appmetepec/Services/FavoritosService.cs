using System.Text.Json;
using Microsoft.Maui.Storage;

namespace appmetepec.Services;

// Servicio de FAVORITOS LOCALES del modulo de Eventos.
//
// El ciudadano marca con una estrella las actividades que le interesan dentro de la
// programacion de un escenario. TODO es LOCAL en el dispositivo: no hay backend, ni login,
// ni red. Se persiste con Microsoft.Maui.Storage.Preferences, igual que el resto de los
// datos locales de la app (ver PreferencesService).
//
// FASE 1 (esta): persistir el conjunto de actividades favoritas por su Id (clave estable y
// unica global que entrega el backend). Esta clase es la BASE de fases futuras: "mi agenda"
// y recordatorios se construiran encima de este mismo set, por eso la API se mantiene limpia
// y reutilizable. El conjunto se guarda bajo UNA sola clave de Preferences con la lista de
// IDs serializada en JSON, de modo que mas adelante sea facil extender el formato.
public sealed class FavoritosService
{
    // Clave unica de Preferences bajo la que vive el conjunto completo de favoritos (JSON con
    // la lista de IDs de actividad). Una sola clave facilita leer/escribir el set de una vez.
    private const string ClaveFavoritos = "favoritos_actividades";

    // Cache en memoria del conjunto de IDs favoritos. Carga perezosa: se llena la primera vez
    // que se consulta y, a partir de ahi, se mantiene sincronizada con cada cambio. Evita leer
    // y deserializar Preferences en cada consulta de la lista.
    private HashSet<int>? _favoritos;

    // Devuelve el set en memoria, cargandolo desde Preferences la primera vez (lazy).
    private HashSet<int> Favoritos => _favoritos ??= Cargar();

    // true si la actividad indicada esta marcada como favorita.
    public bool EsFavorito(int actividadId) => Favoritos.Contains(actividadId);

    // Alterna el estado de favorito de la actividad (marca si no estaba, desmarca si estaba) y
    // persiste el cambio. Devuelve el NUEVO estado (true = quedo como favorita) para que la UI
    // pueda refrescar la estrella sin volver a consultar.
    public bool Alternar(int actividadId)
    {
        // Lee-modifica-escribe consistente sobre el set en memoria: todo ocurre en el hilo de
        // UI, asi que no hay carreras; aun asi, se persiste en cada cambio para no perder datos.
        var set = Favoritos;
        bool quedoFavorito;
        if (set.Add(actividadId))
        {
            quedoFavorito = true;
        }
        else
        {
            set.Remove(actividadId);
            quedoFavorito = false;
        }

        Guardar(set);
        return quedoFavorito;
    }

    // Marca la actividad como favorita (idempotente) y persiste si hubo cambio.
    public void Marcar(int actividadId)
    {
        if (Favoritos.Add(actividadId))
        {
            Guardar(Favoritos);
        }
    }

    // Desmarca la actividad (idempotente) y persiste si hubo cambio.
    public void Desmarcar(int actividadId)
    {
        if (Favoritos.Remove(actividadId))
        {
            Guardar(Favoritos);
        }
    }

    // Copia inmutable del conjunto actual de favoritos. Base para "mi agenda" en fases futuras.
    public IReadOnlyCollection<int> ObtenerFavoritos() => Favoritos.ToArray();

    // Lee y deserializa el conjunto desde Preferences. Si no hay nada guardado o el JSON esta
    // corrupto, devuelve un set vacio (nunca lanza): la app no debe romperse por datos locales.
    private static HashSet<int> Cargar()
    {
        var json = Preferences.Default.Get(ClaveFavoritos, "");
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var ids = JsonSerializer.Deserialize<int[]>(json);
            return ids is null ? [] : new HashSet<int>(ids);
        }
        catch (JsonException)
        {
            // Dato local corrupto: se ignora y se arranca con un set vacio.
            return [];
        }
    }

    // Serializa el conjunto a JSON y lo guarda en Preferences (sobre la unica clave).
    private static void Guardar(HashSet<int> set)
    {
        var json = JsonSerializer.Serialize(set.ToArray());
        Preferences.Default.Set(ClaveFavoritos, json);
    }
}
