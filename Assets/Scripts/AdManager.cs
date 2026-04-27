using UnityEngine;
using UnityEngine.Advertisements;

public class AdManager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
{
    public static AdManager Instance;

    [Header("Configuración Unity Ads")]
    [Tooltip("El ID de Android que te da el Dashboard de Unity")]
    public string androidGameId = "6098341"; 
    public bool testMode = true; 

    // Nombres estándar de los anuncios en Unity
    private string interstitialAdUnitId = "Interstitial_Android";
    private string rewardedAdUnitId = "Rewarded_Android";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Inicializamos el SDK de anuncios al arrancar el juego
        Advertisement.Initialize(androidGameId, testMode, this);
    }

    // ANUNCIO DE LA TIENDA 
    public void MostrarAnuncioRecompensado()
    {
        Debug.Log("Cargando anuncio recompensado...");
        Advertisement.Load(rewardedAdUnitId, this);
    }

    //  ANUNCIO ENTRE PARTIDAS 
    public void MostrarAnuncioIntersticial()
    {
        // Comprobamos si el jugador pagó por quitar la publicidad
        if (PlayerPrefs.GetInt("NoAds", 0) == 1)
        {
            Debug.Log("El jugador es Premium (NoAds). Saltando anuncio.");
            return;
        }

        Debug.Log("Cargando anuncio intersticial...");
        Advertisement.Load(interstitialAdUnitId, this);
    }

    // CALLBACKS
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        if (adUnitId.Equals(rewardedAdUnitId) && showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
        {
            Debug.Log("¡Anuncio visto completo! Dando 100 monedas...");
            if (TopBarUI.Instance != null)
            {
                TopBarUI.Instance.ActualizarMonedas(100);
            }
        }
    }

    // Interfaces obligatorias 
    public void OnInitializationComplete() => Debug.Log("Unity Ads Inicializado.");
    public void OnInitializationFailed(UnityAdsInitializationError e, string m) => Debug.LogError($"Fallo Ads: {e} - {m}");
    public void OnUnityAdsAdLoaded(string adUnitId) => Advertisement.Show(adUnitId, this);
    public void OnUnityAdsFailedToLoad(string id, UnityAdsLoadError e, string m) => Debug.LogError($"Error carga: {m}");
    public void OnUnityAdsShowFailure(string id, UnityAdsShowError e, string m) => Debug.LogError($"Error show: {m}");
    public void OnUnityAdsShowStart(string adUnitId) { }
    public void OnUnityAdsShowClick(string adUnitId) { }
}