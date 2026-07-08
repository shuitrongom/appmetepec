namespace appmetepec.Services;

public static class AppConstants
{
    public const string AppName = "Metepec *7311";
    public const string BaseZendeskUrl = "https://metepec.zendesk.com";
    public const string BaseZendeskNaranjaUrl = "https://metepecnaranja.zendesk.com";
    public const string EnvConsultingUrl = "http://env_consulting_api.mobzilla.com/";
    public const string ZendeskCredentialsUrl = "http://smidesk.mobzilla.com/zen_metepec.json";
    public const string ServerDtUrl = "http://api.mobzilla.com/index.aspx";
    public const string RecoleccionUrl = "https://recolecciongt.mx/api_external/";
    public const string MetepecApiUrl = "http://metepec-api.mobzilla.com";
    public const string RecoleccionApiKey = "M3T3p3c_Sm1_2022";
    public const string PrivacyUrl = "https://metepec7311.com/privacidad/";
    public const string SamUrl = "https://rebrand.ly/uqqh70m";
    public const string TwilioServiceUrl = "https://verify.twilio.com/v2/Services/VAe95228e82fe5721889956945b50a6fe3";
    public const string MetepecBackendUrl = "http://dess-ti.ddns.net:8069/api";

    // Corre en paralelo con la inicializacion (no sumado, a diferencia del delay de 4s secuencial de Android)
    public const int MinimumSplashDurationMs = 1800;
}
