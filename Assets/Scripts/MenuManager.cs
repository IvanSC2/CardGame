using UnityEngine;
using TMPro; // Necesario para los textos de Premio y Precio

public class MenuManager : MonoBehaviour
{
    // Singleton para llamarlo desde otros scripts (ej: al ganar la partida)
    public static MenuManager Instance;

    [Header("1. HUB Central")]
    public GameObject panelHub;
    
    [Header("2. Perfil & Stats")]
    public GameObject panelProfile;
    
    [Header("3. Tienda (Shop)")]
    public GameObject panelShop;

    [Header("4A. Modo Práctica")]
    public GameObject panelPractice;
    public OptionController practicePlayers;
    public OptionController practiceTime;
    public OptionController practiceBotAI;
    public TMP_Text textoPremioPractica; // El texto del premio

    [Header("4B. Modo Público (Matchmaking)")]
    public GameObject panelMatchmakingLobby;

    [Header("4C. Modo Privado (Private)")]
    public GameObject panelPrivateChoice;  
    public GameObject panelPrivateCreate;  
    public GameObject panelPrivateJoin;    
    public GameObject panelPrivateLobby;   
    
    [Header("Selectores Modo Privado")]
    public OptionController privatePlayers;
    public OptionController privateTime;
    public OptionController privateDifficulty;
    public OptionController privateEntryFee;
    public TMP_Text textoPrecioPrivada; // El texto del coste de entrada

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        MostrarHub();
        
        // Calculamos los números iniciales al arrancar para que no salgan vacíos
        if(practicePlayers != null) CalcularPremioPractica();
        if(privatePlayers != null) CalcularPremioPrivada();
    }

    // --- MÁQUINA DE ESTADOS ---

    public void MostrarHub() { ApagarTodosLosPaneles(); panelHub.SetActive(true); }
    public void MostrarPerfil() { ApagarTodosLosPaneles(); panelProfile.SetActive(true); }
    public void MostrarTienda() { ApagarTodosLosPaneles(); panelShop.SetActive(true); }

    public void IniciarFlujoPractica() { ApagarTodosLosPaneles(); panelPractice.SetActive(true); }
    public void IniciarFlujoPublico() { ApagarTodosLosPaneles(); panelMatchmakingLobby.SetActive(true); }
    public void IniciarFlujoPrivado() { ApagarTodosLosPaneles(); panelPrivateChoice.SetActive(true); }

    public void MostrarPrivateCreate() { ApagarTodosLosPaneles(); panelPrivateCreate.SetActive(true); }
    public void MostrarPrivateJoin() { ApagarTodosLosPaneles(); panelPrivateJoin.SetActive(true); }
    public void MostrarPrivateLobby() { ApagarTodosLosPaneles(); panelPrivateLobby.SetActive(true); }

    private void ApagarTodosLosPaneles()
    {
        panelHub.SetActive(false);
        panelProfile.SetActive(false);
        panelShop.SetActive(false);
        panelPractice.SetActive(false);
        //panelMatchmakingLobby.SetActive(false);
        panelPrivateChoice.SetActive(false);
        //panelPrivateCreate.SetActive(false);
        //panelPrivateJoin.SetActive(false);
        //panelPrivateLobby.SetActive(false);
    }

    // ECONOMÍA DINÁMICA 

    public void CalcularPremioPractica()
    {
      
        if (practicePlayers == null || textoPremioPractica == null) return;

        int premioBase = 50;
        int bonusJugadores = practicePlayers.ObtenerIndice() * 20;
        int idxTiempo = practiceTime.ObtenerIndice();
        int bonusTiempo = (2 - idxTiempo) * 30;
        int bonusIA = practiceBotAI.ObtenerIndice() * 50;

        int premioTotal = premioBase + bonusJugadores + bonusTiempo + bonusIA;
        textoPremioPractica.text = premioTotal.ToString();
    }

    public void CalcularPremioPrivada()
    {
        
        if (privateEntryFee == null || privatePlayers == null || textoPrecioPrivada == null) return;

        // Entry Fee
        
        string textoFee = privateEntryFee.opciones[privateEntryFee.ObtenerIndice()];
        int entryFee = int.Parse(textoFee);

        // Cantidad de jugadores 
        
        int cantidadJugadores = privatePlayers.ObtenerIndice() + 2; 

        // Ligeros Bonus (Para que no rompan la economía, solo dan un pellizco extra)
        // Tiempo: Índice 0 (15s) da +30, Índice 1 (30s) da +15, Índice 2 (60s) da +0.
        int bonusTiempo = (2 - privateTime.ObtenerIndice()) * 15; 
        
        // IA: Índice 0 (Easy) da +0, Índice 1 (Normal) da +20, Índice 2 (Imposible) da +40.
        int bonusIA = privateDifficulty.ObtenerIndice() * 20;

        // Cálculo Final: (Jugadores * Fee) + Bonus
        int premioTotal = (cantidadJugadores * entryFee) + bonusTiempo + bonusIA;

        // Actualizamos el texto en pantalla
        textoPrecioPrivada.text = premioTotal.ToString();
    }
}