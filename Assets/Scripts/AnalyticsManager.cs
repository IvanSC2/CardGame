using UnityEngine;
using Unity.Services.Analytics;
using System.Collections.Generic;

/// <summary>
/// Gestor centralizado de Analíticas.
/// Todos los eventos del juego pasan por aquí para mantener un único punto de control.
/// Los Funnels se configuran en el Dashboard de Unity usando estos mismos nombres de eventos.
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance;

    private bool _initialized = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    /// <summary>
    /// Llamar DESPUÉS de que UnityServices.InitializeAsync() haya terminado.
    /// Activa la recolección de datos de analíticas.
    /// </summary>
    public void IniciarRecoleccion()
    {
        try
        {
            AnalyticsService.Instance.StartDataCollection();
            _initialized = true;
            Debug.Log("[ANALYTICS] Recolección de datos iniciada correctamente.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ANALYTICS] No se pudo iniciar la recolección: {e.Message}");
        }
    }

    // =========================================================================
    // FUNNEL 1 - RETENCIÓN: matchmaking_started → match_started → match_completed
    // =========================================================================

    /// <summary> Evento 1 del Funnel de Retención. Se dispara al cargar el Menú Principal. </summary>
    public void EventoAppOpened()
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("app_opened");
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log("[ANALYTICS] Evento: app_opened");
    }

    /// <summary> Evento 2 del Funnel de Retención. Se dispara al pulsar Buscar Partida o Crear Privada. </summary>
    public void EventoMatchmakingStarted(string tipoSala)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("matchmaking_started");
        ev.Add("tipo_sala", tipoSala); // "public" o "private"
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: matchmaking_started (tipo: {tipoSala})");
    }

    /// <summary> Evento 3 del Funnel de Retención. Se dispara al arrancar la partida real (escena de juego). </summary>
    public void EventoMatchStarted(string tipoSala, int numJugadores)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("match_started");
        ev.Add("tipo_sala", tipoSala);
        ev.Add("num_jugadores", numJugadores);
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: match_started ({tipoSala}, {numJugadores} jugadores)");
    }

    /// <summary> Evento 4 del Funnel de Retención. Se dispara al terminar la partida normalmente. </summary>
    public void EventoMatchCompleted(int puesto, int duracionSegundos, int dineroGanado, string tipoSala)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("match_completed");
        ev.Add("puesto", puesto);
        ev.Add("duracion_segundos", duracionSegundos);
        ev.Add("dinero_ganado", dineroGanado);
        ev.Add("tipo_sala", tipoSala);
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: match_completed (puesto {puesto}, {duracionSegundos}s, {dineroGanado}$)");
    }

    // =========================================================================
    // EVENTOS DE ABANDONO (Analíticas sueltas, no del Funnel pero muy útiles)
    // =========================================================================

    /// <summary> El jugador local abandona voluntariamente la partida. </summary>
    public void EventoMatchAbandoned(int duracionSegundos, bool esHost, string tipoSala)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("match_abandoned");
        ev.Add("duracion_segundos", duracionSegundos);
        ev.Add("es_host", esHost);
        ev.Add("tipo_sala", tipoSala);
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: match_abandoned ({duracionSegundos}s, host: {esHost}, sala: {tipoSala})");
    }

    /// <summary> El Host se ha desconectado y los clientes son expulsados. </summary>
    public void EventoHostAbandoned(string tipoSala)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("host_abandoned");
        ev.Add("tipo_sala", tipoSala);
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: host_abandoned (sala: {tipoSala})");
    }

    // =========================================================================
    // FUNNEL 2 - MONETIZACIÓN: shop_opened → purchase_completed
    // =========================================================================

    /// <summary> El jugador abre la tienda. </summary>
    public void EventoShopOpened()
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("shop_opened");
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log("[ANALYTICS] Evento: shop_opened");
    }

    /// <summary> El jugador completa una compra en la tienda (real o simulada en PC). </summary>
    public void EventoPurchaseCompleted(string idProducto)
    {
        if (!_initialized) return;
        CustomEvent ev = new CustomEvent("purchase_completed");
        ev.Add("producto_id", idProducto);
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"[ANALYTICS] Evento: purchase_completed (producto: {idProducto})");
    }
}
