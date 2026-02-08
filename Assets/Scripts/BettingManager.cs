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
    public int p1Bet;
    public int p2Bet;

    private bool isP1Choosing = true;

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
        isP1Choosing = true; // Siempre empieza P1 por ahora (puedes rotarlo luego)
        tableObject.SetActive(false);
        panelRoot.SetActive(true);
        SetupUIForP1();
    }

    private void SetupUIForP1()
    {
        titleText.text = "TU TURNO: ¿Cuántas bazas ganarás?";
        EnableButtons(true);
        
        // Regla: Si P1 es último, aplicar prohibida (Aquí asumimos P1 primero por simpleza)
        for (int i = 0; i < betButtons.Length; i++)
            betButtons[i].interactable = (i <= cardsInRound);
    }

    // --- EL BOTÓN CLICADO POR TI ---
    private void OnBetClicked(int amount)
    {
        if (isP1Choosing)
        {
            p1Bet = amount;
            InteractionManager.Instance.SetInfoMessage($"P1 APUESTA: {p1Bet}");
            
            isP1Choosing = false;
            
            // EN LUGAR DE MOSTRAR BOTONES PARA P2, LANZAMOS LA IA
            DisableButtons(); // Que no toques nada
            StartCoroutine(AIBettingRoutine());
        }
    }

    // --- PENSAMIENTO DE LA IA ---
    IEnumerator AIBettingRoutine()
    {
        titleText.text = "JUGADOR 2 (IA) ESTÁ PENSANDO...";
        yield return new WaitForSeconds(1.5f); // Pausa dramática

        // 1. Recopilar datos para el cerebro
        var p2Hand = GetCardsFromGroup(InteractionManager.Instance.handGroupP2);
        
        // 2. Preguntar al cerebro
        // (Assume P2 is Last because P1 was first)
        p2Bet = AIController.Instance.CalculateAIBet(p2Hand, p1Bet, cardsInRound, true);

        InteractionManager.Instance.SetInfoMessage($"P2 (IA) DICE QUE GANARÁ: {p2Bet}");
        
        // 3. Empezar juego
        yield return new WaitForSeconds(1.0f);
        panelRoot.SetActive(false);
        tableObject.SetActive(true);
        InteractionManager.Instance.InitializeGame();
    }

    // Helpers
    private void EnableButtons(bool enable) { foreach(var b in betButtons) b.gameObject.SetActive(enable); }
    private void DisableButtons() { foreach(var b in betButtons) b.gameObject.SetActive(false); }

    // Método auxiliar para convertir UICards visuales a Datos Card
    private List<Card> GetCardsFromGroup(CanvasGroup group)
    {
        List<Card> list = new List<Card>();
        foreach(Transform t in group.transform)
        {
            UICard ui = t.GetComponent<UICard>();
            if(ui != null) list.Add(ui.cardData);
        }
        return list;
    }
}