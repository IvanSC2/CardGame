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

    // Flag: true solo cuando el jugador ha pulsado el botón explícitamente
    private bool _adRequested = false;
    // Flag: true cuando el anuncio ya está pre-cargado y listo para mostrarse
    private bool _adReady = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // True si estamos en una plataforma donde Unity Ads NO está soportado (PC, Linux, Editor de PC)
    private bool _platformSinSoporte = false;

    private System.Collections.IEnumerator Start()
    {
        #if UNITY_ANDROID
            float elapsed = 0f;
            while (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
            {
                elapsed += Time.deltaTime;
                if (elapsed > 8f)
                {
                    Debug.LogWarning($"[AdManager] Timeout Unity Services ({elapsed:F1}s). Inicializando Ads igualmente...");
                    break;
                }
                yield return null;
            }
            Debug.Log($"[AdManager] Inicializando SDK (testMode={testMode})...");
            Advertisement.Initialize(androidGameId, testMode, this);
        #else
            _platformSinSoporte = true;
            Debug.Log($"[AdManager] Plataforma {Application.platform} no soportada.");
            yield break;
        #endif
    }

    // Muestra un popup de debug en pantalla (solo para diagnosticar)
    private void ShowDebugPopup(string msg)
    {
        Debug.Log(msg);
        if (MenuManager.Instance != null)
            MenuManager.Instance.MostrarPopupInfo(msg);
    }

    // ANUNCIO DE LA TIENDA 
    public void MostrarAnuncioRecompensado()
    {
        // En PC/Linux Unity Ads no existe → recompensa directa sin anuncio
        if (_platformSinSoporte)
        {
            Debug.Log("[AdManager] PC/Linux: dando recompensa directamente (sin anuncio).");
            OnUnityAdsShowComplete(rewardedAdUnitId, UnityAdsShowCompletionState.COMPLETED);
            return;
        }

        if (!Advertisement.isInitialized)
        {
            Debug.LogWarning("Unity Ads no está inicializado. Activando simulación...");
            if (MenuManager.Instance != null)
                MenuManager.Instance.MostrarPopupInfo("El SDK de Unity Ads no está inicializado en tu móvil.\n\nSe entregará la recompensa simulada.");
            OnUnityAdsShowComplete(rewardedAdUnitId, UnityAdsShowCompletionState.COMPLETED);
            return;
        }

        Debug.Log("Cargando anuncio recompensado (solicitado por el jugador)...");
        _adRequested = true;

        if (_adReady)
        {
            // El anuncio ya estaba pre-cargado → lo mostramos directamente sin volver a cargar
            _adReady = false;
            _adRequested = false;
            Debug.Log("Anuncio pre-cargado listo. Mostrando directamente...");
            Advertisement.Show(rewardedAdUnitId, this);
        }
        else
        {
            // No estaba listo todavía → pedimos carga (OnUnityAdsAdLoaded lo mostrará)
            Advertisement.Load(rewardedAdUnitId, this);
        }
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
        AudioManager.Instance?.musicSource.UnPause();
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
    public void OnInitializationComplete() 
    {
        Debug.Log("[AdManager] SDK inicializado. Pre-cargando anuncio en segundo plano...");
        Advertisement.Load(rewardedAdUnitId, this);
    }
    public void OnInitializationFailed(UnityAdsInitializationError e, string m) 
    {
        // Solo mostramos popup de init si el usuario ya intentó ver un anuncio
        Debug.LogError($"[AdManager] FALLO inicializacion: {e} | {m}");
        if (_adRequested) ShowDebugPopup($"[ADS] FALLO al inicializar:\n{e}\n{m}");
    }
    public void OnUnityAdsAdLoaded(string adUnitId) 
    {
        if (_adRequested)
        {
            _adRequested = false;
            _adReady = false;
            Debug.Log($"[AdManager] Anuncio listo y solicitado: {adUnitId}. Mostrando...");
            Advertisement.Show(adUnitId, this);
        }
        else
        {
            // Pre-carga silenciosa en segundo plano — sin popup
            _adReady = true;
            Debug.Log($"[AdManager] Anuncio pre-cargado en segundo plano: {adUnitId}.");
        }
    }
    public void OnUnityAdsFailedToLoad(string id, UnityAdsLoadError e, string m) 
    {
        // Comprobar ANTES de limpiar el flag
        bool eraExplicito = _adRequested;
        _adRequested = false;
        Debug.LogError($"[AdManager] FALLO al cargar '{id}': {e} | {m}");
        // Solo popup si el usuario lo pidió explícitamente (pulsó el botón)
        if (eraExplicito) ShowDebugPopup($"[ADS] FALLO al cargar '{id}':\n{e}\n{m}");
    }
    public void OnUnityAdsShowFailure(string id, UnityAdsShowError e, string m) 
    {
        AudioManager.Instance?.musicSource.UnPause();
        Debug.LogError($"Error show: {m}");
    }
    public void OnUnityAdsShowStart(string adUnitId) 
    {
        AudioManager.Instance?.musicSource.Pause();
    }
    public void OnUnityAdsShowClick(string adUnitId) { }
}