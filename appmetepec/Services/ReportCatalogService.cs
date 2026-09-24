using System.Globalization;
using System.Text;
using System.Text.Json;
using appmetepec.Models;

namespace appmetepec.Services;

public sealed class ReportCatalogService
{
    // Copia del ultimo catalogo recibido del API, para poder mostrar los servicios sin conexion.
    private const string CacheKey = "CatalogoReportesCache";

    private readonly MetepecApiService _api;

    public ReportCatalogService(MetepecApiService api)
    {
        _api = api;
    }

    // Arma las categorias desde la BD (GET api/servicios/catalogo-reportes: solo dependencias y
    // servicios activos, ordenados por Dependencia.Orden). Si el API falla usa la ultima copia
    // guardada; si tampoco hay copia regresa solo las opciones fijas de la app (ver
    // AgregarOpcionesFijas) con ServiciosCargados = false para que HomePage avise y permita reintentar.
    public async Task<(IReadOnlyList<DependenciaCategoria> Categorias, bool ServiciosCargados)> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        List<BackendCatalogoReporteDependenciaDto>? catalogo;

        try
        {
            catalogo = await _api.GetCatalogoReportesAsync(cancellationToken);
            Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(catalogo));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Catalogo] No se pudo consultar el catalogo de reportes: {ex}");
            catalogo = LeerCache();
        }

        List<DependenciaCategoria> categories = [];
        foreach (var dependencia in catalogo ?? [])
        {
            var category = new DependenciaCategoria(dependencia.Id, dependencia.Nombre, new List<ScreenReport>());
            foreach (var servicio in dependencia.Servicios)
            {
                // Servicio.Descripcion se muestra como instrucciones en ReportPage (vacia = se oculta).
                category.Reportes.Add(new ScreenReport(servicio.Nombre, servicio.Descripcion?.Trim() ?? "", true, true, true, false,
                    dependencia.Nombre, IconSource: GuessIcon(servicio.Nombre), IdServicio: servicio.Id));
            }

            if (category.Reportes.Count > 0)
            {
                categories.Add(category);
            }
        }

        var serviciosCargados = categories.Count > 0;
        AgregarOpcionesFijas(categories);
        return (categories, serviciosCargados);
    }

    // Opciones que no son servicios de la BD sino accesos directos de la app (ver
    // HomePage.OpenReportAsync): marcar al *7311 y los enlaces de Denuncia / Terminos.
    private static void AgregarOpcionesFijas(List<DependenciaCategoria> categories)
    {
        var otros = new DependenciaCategoria(0, "Otros", new List<ScreenReport>());
        otros.Reportes.Add(new ScreenReport("Llamada", "", true, true, true, false,
            ZendeskDependencia.CallCenter.DisplayName(), IconSource: GuessIcon("Llamada")));
        otros.Reportes.Add(new ScreenReport("Denuncia Ciudadana", "", true, true, true, false,
            ZendeskDependencia.Otros.DisplayName(), IdSeccion: 10, IconSource: GuessIcon("Denuncia Ciudadana")));
        otros.Reportes.Add(new ScreenReport("Terminos y condiciones", "", true, true, true, false,
            ZendeskDependencia.Otros.DisplayName(), IdSeccion: 11, IconSource: GuessIcon("Terminos y condiciones")));
        categories.Add(otros);
    }

    private static List<BackendCatalogoReporteDependenciaDto>? LeerCache()
    {
        try
        {
            var json = Preferences.Default.Get(CacheKey, "");
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<List<BackendCatalogoReporteDependenciaDto>>(json);
        }
        catch
        {
            return null;
        }
    }

    // Catalogo anterior, hardcodeado en la app (reemplazado por GetCategoriesAsync, que lo lee de
    // la BD). Se conserva comentado como referencia de los Ids de servicio y de Zendesk originales.
    // Nota: ScreenReport.Dependencia ahora es string; para reactivarlo habria que pasar
    // dependencia.DisplayName() en lugar del enum.
    //
    // public IReadOnlyList<DependenciaCategoria> GetCategories()
    // {
    //     List<DependenciaCategoria> categories = [];
    //
    //     DependenciaCategoria AddCategory(int id, string name)
    //     {
    //         var category = new DependenciaCategoria(id, name, new List<ScreenReport>());
    //         categories.Add(category);
    //         return category;
    //     }
    //
    //     static void Add(DependenciaCategoria category, string title, ZendeskDependencia dependencia,
    //         int idSeccion = 0, int idRequest = 0, int idGroup = 0, int idPriority = 0, int idStatus = 0,
    //         int idArea = 0, int idForm = 0, string instructions = "", int idServicio = 0)
    //     {
    //         category.Reportes.Add(new ScreenReport(title, instructions, true, true, true, false,
    //             dependencia, idSeccion, idRequest, idGroup, idPriority, idStatus, idArea, idForm, GuessIcon(title), idServicio));
    //     }
    //
    //     var obras = AddCategory(1, "Obras Publicas");
    //     Add(obras, "Atencion de bacheo", ZendeskDependencia.ObrasPublicas, 0, 18, 4, 1, 1, 5, 1, idServicio: 1);
    //     Add(obras, "Atencion a recoleccion de escombro", ZendeskDependencia.ObrasPublicas, 0, 19, 4, 1, 1, 5, 1, idServicio: 3);
    //
    //     var servicios = AddCategory(2, "Servicios Publicos");
    //     Add(servicios, "Atencion al mantenimiento de alumbrado publico", ZendeskDependencia.ServiciosPublicos, 0, 1, 1, 1, 1, 2, 1, idServicio: 4);
    //     Add(servicios, "Atencion a caida de arboles", ZendeskDependencia.ServiciosPublicos, 0, 2, 1, 1, 1, 2, 1, idServicio: 5);
    //     Add(servicios, "Atencion a recoleccion de residuos solidos", ZendeskDependencia.ServiciosPublicos, 0, 3, 1, 1, 1, 2, 1, idServicio: 6);
    //     Add(servicios, "Atencion a camion de basura sin pasar", ZendeskDependencia.ServiciosPublicos, 0, 4, 1, 1, 1, 2, 1, idServicio: 7);
    //     Add(servicios, "Atencion al mantenimiento de cesped en espacios publicos", ZendeskDependencia.ServiciosPublicos, 0, 5, 1, 1, 1, 2, 1, idServicio: 8);
    //     Add(servicios, "Atencion a recoleccion de cadaveres de animales en via publica", ZendeskDependencia.ServiciosPublicos, 0, 6, 1, 1, 1, 2, 1, idServicio: 9);
    //     Add(servicios, "Atencion a recoleccion de residuos verdes", ZendeskDependencia.ServiciosPublicos, 0, 7, 1, 1, 1, 2, 1, idServicio: 10);
    //     Add(servicios, "Atencion a mantenimiento de juegos en mal estado", ZendeskDependencia.ServiciosPublicos, 0, 8, 1, 1, 1, 2, 1, idServicio: 11);
    //
    //     var opdapas = AddCategory(3, "OPDAPAS");
    //     Add(opdapas, "Atencion inmediata a fuga de agua", ZendeskDependencia.Opdapas, 0, 12, 3, 1, 1, 4, 1, idServicio: 12);
    //     Add(opdapas, "Atencion de descarga de aguas residuales", ZendeskDependencia.Opdapas, 0, 13, 3, 1, 1, 4, 1, idServicio: 13);
    //     Add(opdapas, "Atencion en limpieza y/o reparacion de alcantarilla", ZendeskDependencia.Opdapas, 0, 14, 3, 1, 1, 4, 1, idServicio: 14);
    //     Add(opdapas, "Atencion a servicio de desazolve", ZendeskDependencia.Opdapas, 0, 15, 3, 1, 1, 4, 1, idServicio: 15);
    //     Add(opdapas, "Atencion a coladera sin tapa", ZendeskDependencia.Opdapas, 0, 17, 3, 1, 1, 4, 1, idServicio: 16);
    //     Add(opdapas, "Atencion a inundaciones", ZendeskDependencia.Opdapas, 0, 16, 3, 1, 1, 4, 1, idServicio: 17);
    //
    //     var ambiente = AddCategory(4, "Medio Ambiente");
    //     Add(ambiente, "Atencion a control canino", ZendeskDependencia.MedioAmbiente, 0, 20, 5, 1, 1, 6, 1, idServicio: 18);
    //     Add(ambiente, "Atencion a podas y derribos", ZendeskDependencia.MedioAmbiente, 0, 21, 5, 1, 1, 6, 1, idServicio: 19);
    //
    //     var proteccion = AddCategory(5, "Proteccion Civil");
    //     Add(proteccion, "Atencion inmediata a fuga de Gas L.P y Natural", ZendeskDependencia.ProteccionCivil, 0, 41, 8, 1, 1, 9, 1, idServicio: 20);
    //     Add(proteccion, "Atencion a incendio", ZendeskDependencia.ProteccionCivil, 0, 42, 8, 1, 1, 9, 1, idServicio: 21);
    //     Add(proteccion, "Atencion a servicio de ambulancia", ZendeskDependencia.ProteccionCivil, 0, 43, 8, 1, 1, 9, 1, idServicio: 22);
    //     Add(proteccion, "Atencion a enjambre de abejas", ZendeskDependencia.ProteccionCivil, 0, 44, 8, 1, 1, 9, 1, idServicio: 23);
    //     Add(proteccion, "Atencion / supervision de danos estructurales", ZendeskDependencia.ProteccionCivil, 0, 45, 8, 1, 1, 9, 1, idServicio: 24);
    //
    //     var seguridad = AddCategory(6, "Seguridad Publica");
    //     Add(seguridad, "Atencion a semaforo sin funcionar", ZendeskDependencia.SeguridadPublica, 0, 37, 2, 1, 1, 3, 1, idServicio: 25);
    //     Add(seguridad, "Atencion a vehiculos en via publica con huella de desmantelamiento", ZendeskDependencia.SeguridadPublica, 0, 39, 2, 1, 1, 3, 1, idServicio: 26);
    //     Add(seguridad, "Atencion a senalamientos de transito en mal estado", ZendeskDependencia.SeguridadPublica, 0, 38, 2, 1, 1, 3, 1, idServicio: 27);
    //     Add(seguridad, "Atencion y/o dictamen de reductores de velocidad", ZendeskDependencia.SeguridadPublica, 0, 40, 2, 1, 1, 3, 1,
    //         "Requerimientos de la solicitud:\n1. Alto flujo vehicular y exceso de velocidad.\n2. Presencia de escuelas, areas de recreacion u hospitales con flujo peatonal alto.\n3. La colocacion del dispositivo debera atender una necesidad de 24 horas al dia.", idServicio: 28);
    //     Add(seguridad, "Atencion a apoyo vial", ZendeskDependencia.SeguridadPublica, 0, 34, 2, 1, 1, 3, 1, idServicio: 29);
    //
    //     var urbano = AddCategory(7, "Desarrollo Urbano");
    //     Add(urbano, "Atencion a obra sin permiso de construccion", ZendeskDependencia.DesarrolloUrbano, 0, 24, 6, 1, 1, 7, 1, idServicio: 30);
    //     Add(urbano, "Atencion y/o dictamen de topes", ZendeskDependencia.DesarrolloUrbano, 0, 25, 6, 1, 1, 7, 1,
    //         "Requerimientos de la solicitud:\n1. Alto flujo vehicular y exceso de velocidad.\n2. Presencia de escuelas, areas de recreacion u hospitales con flujo peatonal alto.\n3. La colocacion del dispositivo debera atender una necesidad de 24 horas al dia.", idServicio: 31);
    //
    //     var gobernacion = AddCategory(8, "Gobernacion");
    //     Add(gobernacion, "Atencion a verificacion de actividad comercial", ZendeskDependencia.Gobernacion, 0, 49, 9, 1, 1, 10, 1, idServicio: 32);
    //
    //     var consejeria = AddCategory(10, "Consejeria Juridica");
    //     Add(consejeria, "Atencion a llanta averiada por bache", ZendeskDependencia.ConsejeriaJuridica, 0, 51, 10, 1, 1, 11, 1, idServicio: 2);
    //
    //     var c2 = AddCategory(11, "C2");
    //     Add(c2, "Atencion a emergencias (policia)", ZendeskDependencia.C2, 0, 52, 11, 1, 1, 12, 1, idServicio: 33);
    //     Add(c2, "Atencion a accidente vehicular", ZendeskDependencia.C2, 0, 53, 10, 1, 1, 11, 1, idServicio: 34);
    //
    //     var callCenter = AddCategory(9, "Call Center");
    //     Add(callCenter, "Llamada", ZendeskDependencia.CallCenter);
    //     Add(callCenter, "Informativo", ZendeskDependencia.CallCenter, 0, 26, 7, 1, 1, 8, 1, idServicio: 35);
    //
    //     var otros = AddCategory(12, "Otros");
    //     Add(otros, "Denuncia Ciudadana", ZendeskDependencia.Otros, 10);
    //     Add(otros, "Terminos y condiciones", ZendeskDependencia.Otros, 11);
    //
    //     return categories;
    // }

    private static string GuessIcon(string title)
    {
        // Los nombres de la BD pueden traer acentos ("Atención", "césped"); se quitan para que
        // coincidan con las palabras clave de abajo.
        var value = new string(title.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        if (value.Contains("bache")) return "ic_bache_via_publica.png";
        if (value.Contains("escombro")) return "ic_escombro.png";
        if (value.Contains("alumbrado")) return "ic_mant_alumbrado.png";
        if (value.Contains("arbol") || value.Contains("podas")) return "ic_caida_arboles.png";
        if (value.Contains("residuos solidos")) return "ic_recoleccion_resid.png";
        if (value.Contains("basura")) return "ic_basura_sin_pasar.png";
        if (value.Contains("cesped")) return "ic_cesped.png";
        if (value.Contains("cadaveres")) return "ic_cadaveres_via_pub.png";
        if (value.Contains("residuos verdes")) return "ic_residuos_verdes.png";
        if (value.Contains("juegos")) return "ic_juegos_mal_estado.png";
        if (value.Contains("fuga de agua")) return "ic_fuga_agua.png";
        if (value.Contains("aguas residuales")) return "ic_descarga_aguas_resi.png";
        if (value.Contains("alcantarilla")) return "ic_aten_limpieza.png";
        if (value.Contains("desazolve")) return "ic_desazolve.png";
        if (value.Contains("coladera")) return "ic_coladera_sin_tapa.png";
        if (value.Contains("inundaciones")) return "ic_inundaciones.png";
        if (value.Contains("canino")) return "ic_atencion_animal.png";
        if (value.Contains("gas")) return "ic_fuga_gas.png";
        if (value.Contains("incendio")) return "ic_incendio.png";
        if (value.Contains("ambulancia")) return "ic_ambulancia.png";
        if (value.Contains("abejas")) return "ic_enjambre_abejas.png";
        if (value.Contains("danos estructurales")) return "ic_danos_estruc.png";
        if (value.Contains("semaforo")) return "ic_semaforo.png";
        if (value.Contains("vehiculos")) return "ic_vehiculo_desmantelado.png";
        if (value.Contains("senalamiento")) return "ic_balizamiento.png";
        if (value.Contains("reductores")) return "ic_reductores_velocidad.png";
        if (value.Contains("apoyo vial")) return "ic_apoyo_vial.png";
        if (value.Contains("obra")) return "ic_obra_sin_permiso.png";
        if (value.Contains("topes")) return "ic_topes_reductores.png";
        if (value.Contains("actividad comercial")) return "ic_actividad_comercio.png";
        if (value.Contains("llanta")) return "ic_llanta.png";
        if (value.Contains("policia")) return "ic_policia.png";
        if (value.Contains("accidente")) return "ic_accidente_vehicular.png";
        if (value.Contains("llamada")) return "ic_call_center.png";
        if (value.Contains("denuncia")) return "ic_denuncia_ciudadana.png";
        if (value.Contains("terminos")) return "ic_privacy.png";
        return "ic_otros.png";
    }
}
