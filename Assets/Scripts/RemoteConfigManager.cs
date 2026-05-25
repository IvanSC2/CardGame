using UnityEngine;
using Unity.Services.RemoteConfig;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

/// <summary>
/// Gestor para descargar variables desde Unity Remote Config.
/// Útil para Test A/B, cambiar precios en vivo o activar eventos sin actualizar la App.
/// </summary>
public class RemoteConfigManager : MonoBehaviour
{
    public static RemoteConfigManager Instance;

    [Header("Variables A/B Test")]
    [Tooltip("Estrategia publicitaria descargada de la nube ('aggressive' o 'punitive').")]
    public string adStrategy = "aggressive"; // Por defecto, si no hay internet o falla, somos agresivos.

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    public async Task FetchConfigs()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[REMOTE-CONFIG] No se puede pedir configs sin estar logeado en UGS.");
            return;
        }

        // Suscribirse al evento de cuando se descargan los datos
        RemoteConfigService.Instance.FetchCompleted += ApplyRemoteConfigs;

        try
        {
            Debug.Log("[REMOTE-CONFIG] Pidiendo variables a la nube para el Test A/B...");
            // Descargar variables (así el dashboard nos asigna automáticamente al Grupo A o B del Test)
            await RemoteConfigService.Instance.FetchConfigsAsync(new userAttributes(), new appAttributes());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[REMOTE-CONFIG] Falló la descarga de variables: {e.Message}");
        }
    }

    private void ApplyRemoteConfigs(ConfigResponse response)
    {
        switch (response.requestOrigin)
        {
            case ConfigOrigin.Default:
                Debug.Log("[REMOTE-CONFIG] No se encontraron valores remotos, usando los por defecto locales.");
                break;
            case ConfigOrigin.Cached:
                Debug.Log("[REMOTE-CONFIG] Usando valores en caché del dispositivo.");
                break;
            case ConfigOrigin.Remote:
                Debug.Log("[REMOTE-CONFIG] Nuevos valores descargados correctamente.");
                break;
        }

        // Leer la variable "ad_strategy" de la nube. Si no existe, devuelve "aggressive".
        adStrategy = RemoteConfigService.Instance.appConfig.GetString("ad_strategy", "aggressive");
        Debug.Log($"[REMOTE-CONFIG] Estrategia Publicitaria asignada: {adStrategy}");
    }

    // Estructuras vacías requeridas por la API de Remote Config
    private struct userAttributes { }
    private struct appAttributes { }
}
