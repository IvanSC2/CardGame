using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode; // ¡NUEVO!

public class BettingManager : NetworkBehaviour 
{
    public static BettingManager Instance;

    [Header("UI References")]
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public Button[] betButtons;

    [Header("Referencias de Escena")]
    public GameObject tableObject;

    [Header("Game State")]
    public int cardsInRound = 5;
    
    // VARIABLES DE CONTROL DEL SERVIDOR
    private int currentBetterIndex = 0; 
    private int betsPlaced = 0;         

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);

        for (int i = 0; i < betButtons.Length; i++)
        {
            int val = i;
            // Cuando pulsas el botón, llamo a la petición de red
            betButtons[i].onClick.AddListener(() => IntentarApostar(val));
        }
    }

    // ========================================================================
    // 1. INICIO ORQUESTADO POR EL SERVIDOR
    // ========================================================================
    public void StartBettingPhase(int numCards)
    {
        if (!IsServer) return; // Solo el Host inicia esto

        cardsInRound = numCards;
        betsPlaced = 0;
        currentBetterIndex = InteractionManager.Instance.manoMesaIndex;

        // Limpiamos las apuestas de todos en el servidor
        int totalPlayers = InteractionManager.Instance.totalPlayers;
        for (int i = 0; i < totalPlayers; i++)
        {
            InteractionManager.Instance.apuestas[i] = -1;
        }

        // Avisamos a todos de que abran sus paneles
        PrepararFaseApuestasClientRpc(numCards);
        
        // Empezamos a procesar los turnos
        ProcessNextBetServer();
    }

    // ========================================================================
    // 2. LÓGICA DE TURNOS (SOLO SERVIDOR)
    // ========================================================================
    private void ProcessNextBetServer()
    {
        int totalPlayers = InteractionManager.Instance.totalPlayers;
        
        int jugadoresVivosRonda = 0;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivosRonda++;
        }

        // Si ya han apostado todos, cerramos fase
        if (betsPlaced >= jugadoresVivosRonda)
        {
            StartCoroutine(EndBettingPhaseServer());
            return;
        }

        // Saltar muertos
        while (InteractionManager.Instance.vidas[currentBetterIndex] <= 0)
        {
            currentBetterIndex = (currentBetterIndex + 1) % totalPlayers;
        }

        bool isLastToBet = (betsPlaced == jugadoresVivosRonda - 1);

        // Calcular apuesta prohibida (solo afecta al último)
        int forbiddenBet = -1;
        if (isLastToBet) 
        {
            int sumBets = 0;
            for(int i = 0; i < totalPlayers; i++) 
            {
                if (InteractionManager.Instance.apuestas[i] >= 0)
                    sumBets += InteractionManager.Instance.apuestas[i];
            }
            forbiddenBet = cardsInRound - sumBets;
        }

        // Avisar a TODOS de quién es el turno
        AvisarTurnoApuestaClientRpc(currentBetterIndex, forbiddenBet, betsPlaced);

        // Si el jugador NO es un cliente humano conectado, es un BOT. El servidor calcula por él.
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)currentBetterIndex))
        {
            StartCoroutine(AIBetRoutine(currentBetterIndex, isLastToBet, forbiddenBet));
        }
    }

    // ========================================================================
    // 3. COMUNICACIONES HACIA LOS CLIENTES (ClientRpc)
    // ========================================================================

    public void FuerzaPasarTurnoDesconectado(int playerIndex)
{
    if (!IsServer) return;
    if (currentBetterIndex == playerIndex)
    {
        InteractionManager.Instance.apuestas[playerIndex] = 0; // Apuesta neutra
        betsPlaced++;
        currentBetterIndex = (currentBetterIndex + 1) % InteractionManager.Instance.totalPlayers;
        ProcessNextBetServer();
    }
}
    [ClientRpc]
    private void PrepararFaseApuestasClientRpc(int numCards)
    {
        cardsInRound = numCards;
        tableObject.SetActive(false);
        panelRoot.SetActive(true);
        DisableButtons();

        // Limpiamos los arrays visualmente en todos los PCs
        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            InteractionManager.Instance.apuestas[i] = -1;

        InteractionManager.Instance.ActualizarTodosLosPerfilesUI();
    }

    [ClientRpc]
    private void AvisarTurnoApuestaClientRpc(int turnoDe, int forbiddenBet, int apuestasRealizadas)
    {
        int miIdLocal = (int)NetworkManager.Singleton.LocalClientId;

        // Texto informativo en la parte superior
        if (apuestasRealizadas > 0)
        {
            int lastBetter = (turnoDe - 1 + InteractionManager.Instance.totalPlayers) % InteractionManager.Instance.totalPlayers;
            int lastBet = InteractionManager.Instance.apuestas[lastBetter]; 
            titleText.text = $"JUGADOR {lastBetter} APOSTÓ {lastBet}.";
        }
        else
        {
            titleText.text = "FASE DE APUESTAS";
        }

        //Mi turno
        if (turnoDe == miIdLocal)
        {
            titleText.text += "\n¡ES TU TURNO!";
            
            // Activar solo los botones legales
            for (int i = 0; i < betButtons.Length; i++)
            {
                if (i <= cardsInRound)
                {
                    betButtons[i].gameObject.SetActive(true);
                    betButtons[i].interactable = (i != forbiddenBet);
                }
                else
                {
                    betButtons[i].gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // No me toca, o es un Bot
            titleText.text += $"\nEsperando al Jugador {turnoDe}...";
            DisableButtons();
        }
    }

    [ClientRpc]
    private void RegistrarApuestaClientRpc(int playerIndex, int amount)
    {
        // Todos los ordenadores actualizan sus datos y perfiles visuales
        InteractionManager.Instance.apuestas[playerIndex] = amount;
        
        string nombre = (playerIndex == (int)NetworkManager.Singleton.LocalClientId) ? "TÚ" : $"JUGADOR {playerIndex}";
        InteractionManager.Instance.SetInfoMessage($"{nombre} APUESTA: {amount}");
        InteractionManager.Instance.ActualizarTodosLosPerfilesUI();
    }

    [ClientRpc]
    private void CerrarPanelClientRpc()
    {
        panelRoot.SetActive(false);
        tableObject.SetActive(true);
    }

    // ========================================================================
    // 4. ACCIONES DEL HUMANO (ServerRpc)
    // ========================================================================
    private void IntentarApostar(int amount)
    {
        DisableButtons(); // Ocultamos localmente al pulsar para evitar doble clic
        EnviarApuestaServerRpc(amount);
    }

[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
private void EnviarApuestaServerRpc(int amount, RpcParams rpcParams = default) // Cambiado a RpcParams
{
    ulong senderId = rpcParams.Receive.SenderClientId;
    
    // Validación de seguridad
    if ((ulong)currentBetterIndex != senderId) return; 

    // El servidor acepta la apuesta, avisa a todos y pasa turno
    RegistrarApuestaClientRpc(currentBetterIndex, amount);
    
    betsPlaced++;
    currentBetterIndex = (currentBetterIndex + 1) % InteractionManager.Instance.totalPlayers;
    ProcessNextBetServer();
}

    // ========================================================================
    // 5. RUTINAS DEL SERVIDOR (IA y Cierre)
    // ========================================================================
    IEnumerator AIBetRoutine(int botIndex, bool isLast, int forbiddenBet)
    {
        yield return new WaitForSeconds(1.5f); //  Pausa para que no sea instantaneo

        int sumBets = 0;
        for(int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
        {
            if (InteractionManager.Instance.apuestas[i] >= 0)
                sumBets += InteractionManager.Instance.apuestas[i];
        }

        int betAmount = 0;

        if (cardsInRound == 1)
        {
            List<Card> visibleCards = new List<Card>();
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                if (i != botIndex && InteractionManager.Instance.vidas[i] > 0)
                {
                    var hand = GetCardsFromGroup(InteractionManager.Instance.playerHands[i]);
                    if (hand.Count > 0) visibleCards.Add(hand[0]);
                }
            }
            betAmount = AIController.Instance.CalculateBlindBet(visibleCards, sumBets, isLast); 
        }
        else
        {
            var botHand = GetCardsFromGroup(InteractionManager.Instance.playerHands[botIndex]);
            betAmount = AIController.Instance.CalculateAIBet(botHand, sumBets, cardsInRound, isLast); 
        }

        // Si por algún motivo matemático la IA eligió la apuesta prohibida, la forzamos a cambiar
        if (betAmount == forbiddenBet)
        {
            betAmount = (betAmount > 0) ? betAmount - 1 : betAmount + 1;
        }

        RegistrarApuestaClientRpc(botIndex, betAmount);

        yield return new WaitForSeconds(0.5f);

        betsPlaced++;
        currentBetterIndex = (currentBetterIndex + 1) % InteractionManager.Instance.totalPlayers;
        ProcessNextBetServer();
    }
    
    IEnumerator EndBettingPhaseServer()
    {
        CerrarPanelClientRpc();

        if (cardsInRound == 1)
            InteractionManager.Instance.ResolveBlindRoundImmediate();
        else
            InteractionManager.Instance.InitializeGame();

        yield return null;
    }
    
    // UTILIDADES
    private void EnableButtons(bool enable) { foreach (var b in betButtons) b.gameObject.SetActive(enable); }
    private void DisableButtons() { foreach (var b in betButtons) b.gameObject.SetActive(false); }

    private List<Card> GetCardsFromGroup(CanvasGroup group)
    {
        List<Card> list = new List<Card>();
        foreach (Transform t in group.transform)
        {
            UICard ui = t.GetComponent<UICard>();
            if (ui != null) list.Add(ui.cardData);
        }
        return list;
    }
}