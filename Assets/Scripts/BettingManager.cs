using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BettingManager : MonoBehaviour
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
    
    //VARIABLES DE CONTROL 
    private int currentBetterIndex = 0; // A quién le toca apostar ahora mismo
    private int betsPlaced = 0;         // Cuántas personas han apostado ya

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);

        for (int i = 0; i < betButtons.Length; i++)
        {
            int val = i;
            betButtons[i].onClick.AddListener(() => OnBetClicked(val));
        }
    }

    public void StartBettingPhase(int numCards)
    {
        cardsInRound = numCards;
        tableObject.SetActive(false);
        panelRoot.SetActive(true);
        
        if (InteractionManager.Instance != null)
            InteractionManager.Instance.RefreshHandVisibility();

        int totalPlayers = InteractionManager.Instance.totalPlayers;

        // 1. Reseteamos el array maestro de apuestas a -1 (nadie ha apostado)
        for (int i = 0; i < totalPlayers; i++)
        {
            InteractionManager.Instance.apuestas[i] = -1;
        }
        
        betsPlaced = 0;

        // 2. Quien empieza a apostar
        currentBetterIndex = InteractionManager.Instance.manoMesaIndex;
        
        InteractionManager.Instance.ActualizarTodosLosPerfilesUI();

        // 3. Arrancamos apuestas
        ProcessNextBet();
    }

    private void ProcessNextBet()
    {
        int totalPlayers = InteractionManager.Instance.totalPlayers;
        
        //Calculamos cuántos vivos hay en total en la mesa
        int jugadoresVivosRonda = 0;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivosRonda++;
        }

        // Ver si han aspostado todos los vivos
        if (betsPlaced >= jugadoresVivosRonda)
        {
            StartCoroutine(EndBettingPhase());
            return;
        }

        //  Si al que le toca está muerto, pasamos al siguiente automáticamente
        while (InteractionManager.Instance.vidas[currentBetterIndex] <= 0)
        {
            currentBetterIndex = (currentBetterIndex + 1) % totalPlayers;
        }

        // Saber si el que va a apostar ahora es el último VIVO
        bool isLastToBet = (betsPlaced == jugadoresVivosRonda - 1);

        if (currentBetterIndex == 0)
        {
            SetupUIForP1(isLastToBet);
        }
        else
        {
            StartCoroutine(AIBetRoutine(currentBetterIndex, isLastToBet));
        }
    }

    private void SetupUIForP1(bool amILast)
    {
        if (betsPlaced > 0)
        {
            int lastBetter = (currentBetterIndex - 1 + InteractionManager.Instance.totalPlayers) % InteractionManager.Instance.totalPlayers;
            int lastBet = InteractionManager.Instance.apuestas[lastBetter]; // Leemos del Manager
            titleText.text = $"JUGADOR {lastBetter} APOSTÓ {lastBet}. TU TURNO:";
        }
        else
        {
            titleText.text = "¿Cuántas bazas ganarás?";
        }
            
        EnableButtons(true);
        
        //Apuesta prohibida
        int forbiddenBet = -1;
        if (amILast) 
        {
            int sumBets = 0;
            // Sumamos leyendo del array maestro
            for(int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
            {
                if (InteractionManager.Instance.apuestas[i] >= 0)
                    sumBets += InteractionManager.Instance.apuestas[i];
            }
            forbiddenBet = cardsInRound - sumBets;
        }
        
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

    private void OnBetClicked(int amount)
    {
        // Guardar tu EL ARRAY
        InteractionManager.Instance.apuestas[0] = amount;
        InteractionManager.Instance.SetInfoMessage($"TÚ APUESTAS: {amount}");

        //Actualizamos los letreros visuales al instante
        InteractionManager.Instance.ActualizarTodosLosPerfilesUI();

        DisableButtons(); 
        
        // Pasar al siguiente
        betsPlaced++;
        currentBetterIndex = (currentBetterIndex + 1) % InteractionManager.Instance.totalPlayers;
        
        ProcessNextBet();
    }

    // PENSAMIENTO UNIVERSAL DE LA IA
    IEnumerator AIBetRoutine(int botIndex, bool isLast)
    {
        titleText.text = $"BOT {botIndex} ESTÁ PENSANDO...";
        DisableButtons();
        yield return new WaitForSeconds(1.5f);

        int sumBets = 0;
        for(int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
        {
            if (InteractionManager.Instance.apuestas[i] >= 0)
                sumBets += InteractionManager.Instance.apuestas[i];
        }

        int betAmount = 0;

        if (cardsInRound == 1)
        {
            // 1. MIRAMOS LAS CARTAS DE TODOS LOS RIVALES VIVOS EN LA MESA
            List<Card> visibleCards = new List<Card>();
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                // Si el jugador está vivo, y no es el propio bot que está pensando
                if (i != botIndex && InteractionManager.Instance.vidas[i] > 0)
                {
                    var hand = GetCardsFromGroup(InteractionManager.Instance.playerHands[i]);
                    if (hand.Count > 0) visibleCards.Add(hand[0]);
                }
            }
            
            // 2. LE PASAMOS TODAS LAS CARTAS VISIBLES AL CEREBRO
            betAmount = AIController.Instance.CalculateBlindBet(visibleCards, sumBets, isLast); 
        }
        else
        {
            var botHand = GetCardsFromGroup(InteractionManager.Instance.playerHands[botIndex]);
            betAmount = AIController.Instance.CalculateAIBet(botHand, sumBets, cardsInRound, isLast); 
        }

        InteractionManager.Instance.apuestas[botIndex] = betAmount;
        InteractionManager.Instance.SetInfoMessage($"BOT {botIndex} APUESTA: {betAmount}");
        InteractionManager.Instance.ActualizarTodosLosPerfilesUI();

        yield return new WaitForSeconds(1.0f);

        betsPlaced++;
        currentBetterIndex = (currentBetterIndex + 1) % InteractionManager.Instance.totalPlayers;
        
        ProcessNextBet();
    }
    
    IEnumerator EndBettingPhase()
    {
        panelRoot.SetActive(false);
        tableObject.SetActive(true);

        if (cardsInRound == 1)
            InteractionManager.Instance.ResolveBlindRoundImmediate();
        else
            InteractionManager.Instance.InitializeGame();

        yield return null;
    }
    
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