namespace appmetepec.Services;

public static class AppConstants
{
    public const string AppName = "Metepec *7311";
    public const string EnvConsultingUrl = "http://env_consulting_api.mobzilla.com/";
    public const string ServerDtUrl = "http://api.mobzilla.com/index.aspx";
    public const string RecoleccionUrl = "https://recolecciongt.mx/api_external/";
    public const string MetepecApiUrl = "http://metepec-api.mobzilla.com";
    public const string RecoleccionApiKey = "M3T3p3c_Sm1_2022";
    public const string PrivacyUrl = "https://metepec7311.com/privacidad/";
    public const string SamUrl = "https://rebrand.ly/uqqh70m";
    public const string TwilioServiceUrl = "https://verify.twilio.com/v2/Services/VAe95228e82fe5721889956945b50a6fe3";
    // TODO: configurar antes de produccion (Account SID / Auth Token de Twilio); sin esto, el envio real de SMS devuelve 401.
    public const string TwilioAccountSid = "";
    public const string TwilioAuthToken = "";
    public const string MetepecBackendUrl = "http://dess-ti.ddns.net:8069/api";

    public const string ClaveEstatusResuelto = "RESUELTO";
    public const string ClaveEncuestaSolucionTicket = "ENCUESTA_SOLUCION_TICKET";

    // Corre en paralelo con la inicializacion (no sumado, a diferencia del delay de 4s secuencial de Android)
    public const int MinimumSplashDurationMs = 1800;
}
