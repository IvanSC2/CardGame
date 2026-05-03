using UnityEngine;
using TMPro; 

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [Header("1. HUB Central")]
    public GameObject panelHub;
    public GameObject pToolBar;
    
    [Header("2. Perfil & Stats")]
    public GameObject panelProfile;
    
    [Header("3. Tienda (Shop)")]
    public GameObject panelShop;

    [Header("4A. Modo Práctica")]
    public GameObject panelPractice;
    public OptionController practicePlayers;
    public OptionController practiceTime;
    public OptionController practiceBotAI;
    public TMP_Text textoPremioPractica; 

    [Header("4B. Modo Público (Matchmaking)")]
    public GameObject panelMatchmakingLobby;

    [Header("4C. Modo Privado (Private)")]
    public GameObject panelPrivateChoice;  
    public GameObject panelPrivateJoin;   
    public GameObject panelPrivateLobby;   
    public GameObject panelClientLobby;    
    
    [Header("Selectores Modo Privado")]
    public OptionController privatePlayers;
    public OptionController privateTime;
    public OptionController privateDifficulty;
    public OptionController privateEntryFee;
    public TMP_Text textoPrecioPrivada; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        MostrarHub();
        
        if(practicePlayers != null) CalcularPremioPractica();
        if(privatePlayers != null) CalcularPremioPrivada();
    }

    // --- MÁQUINA DE ESTADOS ---

    public void MostrarHub() { ApagarTodosLosPaneles(); if(panelHub != null) panelHub.SetActive(true); pToolBar.SetActive(true);}
    public void MostrarPerfil() { ApagarTodosLosPaneles(); if(panelProfile != null) panelProfile.SetActive(true); }
    public void MostrarTienda() { ApagarTodosLosPaneles(); if(panelShop != null) panelShop.SetActive(true); }

    public void IniciarFlujoPractica() { ApagarTodosLosPaneles(); if(panelPractice != null) panelPractice.SetActive(true); }
    public void IniciarFlujoPublico() { ApagarTodosLosPaneles(); if(panelMatchmakingLobby != null) panelMatchmakingLobby.SetActive(true); }
    
    // Al pulsar "Private" en el Hub, venimos aquí:
    public void IniciarFlujoPrivado() { ApagarTodosLosPaneles(); if(panelPrivateChoice != null) panelPrivateChoice.SetActive(true); }

    // Al pulsar "Join" en pPrivate, venimos aquí:
    public void MostrarPrivateJoin() { ApagarTodosLosPaneles(); if(panelPrivateJoin != null) panelPrivateJoin.SetActive(true); }
    
    // Al pulsar "Create" (tras cargar la red), venimos aquí:
    public void MostrarPrivateLobby() { ApagarTodosLosPaneles(); if(panelPrivateLobby != null) panelPrivateLobby.SetActive(true); }
    
    // Al pulsar "Join" (tras buscar el código), venimos aquí:
    public void MostrarClientLobby() { ApagarTodosLosPaneles(); if(panelClientLobby != null) panelClientLobby.SetActive(true); } 

    private void ApagarTodosLosPaneles()
    {
        if (panelHub != null) panelHub.SetActive(false);
        if (panelProfile != null) panelProfile.SetActive(false);
        if (panelShop != null) panelShop.SetActive(false);
        if (panelPractice != null) panelPractice.SetActive(false);
        if (panelMatchmakingLobby != null) panelMatchmakingLobby.SetActive(false);
        if (panelPrivateChoice != null) panelPrivateChoice.SetActive(false);
        if (panelPrivateJoin != null) panelPrivateJoin.SetActive(false);
        if (panelPrivateLobby != null) panelPrivateLobby.SetActive(false);
        if (panelClientLobby != null) panelClientLobby.SetActive(false);
    }

    // --- ECONOMÍA DINÁMICA ---

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

    public int ObtenerPremioPrivadaCalculado()
    {
        if (privateEntryFee == null || privatePlayers == null) return 0;

        string textoFee = privateEntryFee.opciones[privateEntryFee.ObtenerIndice()];
        int entryFee = int.Parse(textoFee);
        int cantidadJugadores = privatePlayers.ObtenerIndice() + 2;

        int bonusTiempo = (2 - privateTime.ObtenerIndice()) * 15;
        int bonusIA = privateDifficulty.ObtenerIndice() * 20;

        return (cantidadJugadores * entryFee) + bonusTiempo + bonusIA;
    }

    public void CalcularPremioPrivada()
    {
        if (textoPrecioPrivada != null)
        {
            textoPrecioPrivada.text = ObtenerPremioPrivadaCalculado().ToString();
        }
    }
}