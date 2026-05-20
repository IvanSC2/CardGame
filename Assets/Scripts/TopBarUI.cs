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

    // La memoria interna de las monedas
    private int monedasActuales;
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
            // Pedimos a la nube el valor de "MisMonedas" de ESTE jugador (PlayerID)
            var query = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "MisMonedas" });

            if (query.TryGetValue("MisMonedas", out var item))
            {
                monedasActuales = item.Value.GetAs<int>();
                Debug.Log($"[ECONOMÍA] Monedas descargadas de la nube: {monedasActuales}");
            }
            else
            {
                // Es un jugador NUEVO en la base de datos. Le damos el regalo y lo subimos.
                monedasActuales = 450;
                Debug.Log("[ECONOMÍA] Jugador nuevo. Asignando 450 monedas de regalo.");
                await GuardarMonedasNube();
            }

            economiaCargada = true;
            ActualizarPantalla();
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
        ActualizarPantalla(); // Refresco visual instantáneo para UX fluida

        // Guardado asíncrono en la base de datos de Unity
        await GuardarMonedasNube();
    }

    private async Task GuardarMonedasNube()
    {
        try
        {
            // Formato JSON/Diccionario requerido por bases de datos NoSQL
            var data = new Dictionary<string, object> { { "MisMonedas", monedasActuales } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log($"[ECONOMÍA] Nube actualizada. Saldo actual: {monedasActuales}");
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
        {
            textoMonedas.text = monedasActuales.ToString();
        }
    }
}