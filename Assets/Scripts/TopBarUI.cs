using UnityEngine;
using TMPro;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TopBarUI : MonoBehaviour
{
    public static TopBarUI Instance;

    [Header("UI Elementos")]
    public TMP_Text textoMonedas;
    public TMP_Text textoTrofeos;

    // La memoria interna de la economía
    private int monedasActuales;
    private int trofeosActuales;
    private bool economiaCargada = false; // Candado de seguridad

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    // ========================================================
    // 0. RESTAURACIÓN DE SESIÓN (Al volver de una partida)
    // ========================================================
    private async void Start()
    {
        try
        {
            // Comprobamos si UGS ya está inicializado y el jugador ya está logueado.
            // Esto solo será VERDADERO cuando volvamos al menú tras haber jugado una partida.
            if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized && 
                Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[TOPBAR] Sesión previa detectada. Recargando economía de la nube...");
                await CargarEconomiaNube();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TOPBAR] Ignorando comprobación inicial: {e.Message}");
        }
    }
    

    // ========================================================
    // 1. CARGA DESDE LA NUBE (Llamado por SessionNetworkManager)
    // ========================================================
    public async Task CargarEconomiaNube()
    {
        try
        {
            var query = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { "MisMonedas", "MisTrofeos" });

            if (query.TryGetValue("MisMonedas", out var itemMonedas))
            {
                monedasActuales = itemMonedas.Value.GetAs<int>();
                Debug.Log($"[ECONOMÍA] Monedas descargadas de la nube: {monedasActuales}");
            }
            else
            {
                monedasActuales = 450;
                Debug.Log("[ECONOMÍA] Jugador nuevo. Asignando 450 monedas de regalo.");
            }

            if (query.TryGetValue("MisTrofeos", out var itemTrofeos))
            {
                trofeosActuales = itemTrofeos.Value.GetAs<int>();
                Debug.Log($"[TROFEOS] Trofeos descargados de la nube: {trofeosActuales}");
            }
            else
            {
                trofeosActuales = 100;
                Debug.Log("[TROFEOS] Jugador nuevo. Asignando 100 trofeos de regalo.");
            }

            economiaCargada = true;

            // ===============================================================
            // ANTI-RAGEQUIT: Castigar si cerró la app a la fuerza en móvil
            // ===============================================================
            if (PlayerPrefs.GetInt("PartidaEnCurso", 0) == 1)
            {
                int penaltyTrofeos = PlayerPrefs.GetInt("RageQuit_Trophies", 0);
                Debug.LogWarning($"[ANTI-RAGEQUIT] Cierre abrupto (deslizar app) detectado de la sesión anterior. Aplicando penalización: {penaltyTrofeos} trofeos.");
                
                int prev = PlayerPrefs.GetInt("PendingTrophyDelta", 0);
                PlayerPrefs.SetInt("PendingTrophyDelta", prev + penaltyTrofeos);
                
                // Las monedas ya fueron restadas y encoladas al iniciar la partida (fee)
                
                PlayerPrefs.SetInt("PartidaEnCurso", 0);
                PlayerPrefs.Save();
            }

            // ===============================================================
            // APLICAR DELTAS PENDIENTES (guardados desde la escena de juego)
            // ===============================================================
            int pendingTrofeos = PlayerPrefs.GetInt("PendingTrophyDelta", 0);
            int pendingMonedas = PlayerPrefs.GetInt("PendingCoinDelta", 0);

            if (pendingTrofeos != 0 || pendingMonedas != 0)
            {
                trofeosActuales = Mathf.Max(0, trofeosActuales + pendingTrofeos);
                monedasActuales += pendingMonedas;
                PlayerPrefs.SetInt("PendingTrophyDelta", 0);
                PlayerPrefs.SetInt("PendingCoinDelta", 0);
                PlayerPrefs.Save();
                Debug.Log($"[ECONOMÍA] Aplicando deltas pendientes. Monedas: {pendingMonedas:+#;-#;0} | Trofeos: {pendingTrofeos:+#;-#;0}");
            }

            await GuardarEconomiaNube();
            ActualizarPantalla();
            LoadingManager.Instance?.OcultarCargando();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ECONOMÍA] Error al conectar con la base de datos: {e.Message}");
        }
    }

    // ========================================================
    // 2. ACTUALIZACIÓN Y GUARDADO EN LA NUBE
    // ========================================================
    public async void ActualizarMonedas(int cantidadASumar)
    {
        if (!economiaCargada) 
        {
            Debug.LogWarning("Intento de sumar monedas antes de cargar la economía.");
            return;
        }

        monedasActuales += cantidadASumar;
        ActualizarPantalla();
        await GuardarEconomiaNube();
    }

    public async void ActualizarTrofeos(int cantidadASumar)
    {
        if (!economiaCargada)
        {
            Debug.LogWarning("Intento de sumar trofeos antes de cargar la economía.");
            return;
        }

        trofeosActuales = Mathf.Max(0, trofeosActuales + cantidadASumar); // Nunca por debajo de 0
        ActualizarPantalla();
        await GuardarEconomiaNube();
        Debug.Log($"[TROFEOS] Nube actualizada. Trofeos actuales: {trofeosActuales}");
    }

    public int GetTrofeos() => trofeosActuales;

    // ========================================================
    // COLA OFFLINE (llamable desde la escena de juego sin TopBarUI)
    // ========================================================
    /// <summary>
    /// Encola un cambio de trofeos y/o monedas para aplicar al volver al menú.
    /// Seguro de llamar aunque TopBarUI.Instance sea null.
    /// </summary>
    public static void QueuePendingDelta(int trofeoDelta, int monedaDelta = 0)
    {
        int prevTrofeos = PlayerPrefs.GetInt("PendingTrophyDelta", 0);
        int prevMonedas = PlayerPrefs.GetInt("PendingCoinDelta", 0);
        PlayerPrefs.SetInt("PendingTrophyDelta", prevTrofeos + trofeoDelta);
        PlayerPrefs.SetInt("PendingCoinDelta", prevMonedas + monedaDelta);
        PlayerPrefs.Save();
        Debug.Log($"[ECONOMÍA] Delta encolado. Trofeos: {trofeoDelta:+#;-#;0} | Monedas: {monedaDelta:+#;-#;0}");
    }

    private async Task GuardarEconomiaNube()
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                { "MisMonedas", monedasActuales },
                { "MisTrofeos", trofeosActuales }
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log($"[ECONOMÍA] Nube actualizada. Monedas: {monedasActuales} | Trofeos: {trofeosActuales}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ECONOMÍA] Fallo al guardar en la nube: {e.Message}");
        }
    }

    // ========================================================
    // 3. MÉTODOS DE CONSULTA (SIN CAMBIOS)
    // ========================================================
    public bool TieneSuficientes(int coste)
    {
        return monedasActuales >= coste;
    }    

    public bool GastarMonedas(int coste)
    {
        if (monedasActuales >= coste)
        {
            ActualizarMonedas(-coste); 
            return true; 
        }
        
        Debug.Log("No tienes suficientes monedas.");
        return false; 
    }

    private void ActualizarPantalla()
    {
        if (textoMonedas != null)
            textoMonedas.text = monedasActuales.ToString();

        if (textoTrofeos != null)
            textoTrofeos.text = trofeosActuales.ToString();
    }
}