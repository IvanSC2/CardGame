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

        tableObject.SetActive(false);
        panelRoot.SetActive(true);
        if (InteractionManager.Instance != null)
            InteractionManager.Instance.RefreshHandVisibility();
        // --- Eleccion del sorteo ---
        if (InteractionManager.Instance.currentMano == GameState.P1_TURN)
        {
            // Jugador 1 Apuesta Primero
            isP1Choosing = true;
            SetupUIForP1(false); // No es el último, no hay regla prohibida
        }
        else
        {
            // IA Apuesta Primero
            isP1Choosing = false;
            StartCoroutine(AIBetsFirstRoutine());
        }
    }

    private void SetupUIForP1(bool amILast)
    {
        if (amILast)
            titleText.text = $"IA APOSTÓ {p2Bet}. TU TURNO:";
        else
            titleText.text = "¿Cuántas bazas ganarás?";
        EnableButtons(true);
        // Calcular la apuesta prohibida si somos los segundos en hablar
        int forbiddenBet = -1;
        if (amILast) forbiddenBet = cardsInRound - p2Bet;
        for (int i = 0; i < betButtons.Length; i++)
        {
            if (i <= cardsInRound)
            {
                betButtons[i].gameObject.SetActive(true);
                // Si somos el último, no podemos pulsar el botón que sume el total de cartas
                betButtons[i].interactable = (i != forbiddenBet);
            }
            else
            {
                // Ocultar botones de apuestas mayores al número de cartas de la ronda
                betButtons[i].gameObject.SetActive(false);
            }
        }
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
            if (InteractionManager.Instance.currentMano == GameState.P1_TURN)
            {
                // P1 empezó, así que ahora le toca a la IA (Piensa y responde)
                StartCoroutine(AIBetsSecondRoutine());
            }
            else
            {
                // La IA empezó, y P1 acaba de responder. Terminamos la fase.
                StartCoroutine(EndBettingPhase());
            }
        }
    }

    // --- PENSAMIENTO DE LA IA ---
    IEnumerator AIBetsFirstRoutine()
    {
        titleText.text = "IA (MANO) ESTÁ PENSANDO...";
        DisableButtons();
        yield return new WaitForSeconds(1.5f);

        if (cardsInRound == 1)
        {
            var p1Hand = GetCardsFromGroup(InteractionManager.Instance.handGroupP1);
            Card p1VisibleCard = (p1Hand.Count > 0) ? p1Hand[0] : null;
            p2Bet = AIController.Instance.CalculateBlindBet(p1VisibleCard, 0, false); // false = no es el último
        }
        else
        {
            var p2Hand = GetCardsFromGroup(InteractionManager.Instance.handGroupP2);
            p2Bet = AIController.Instance.CalculateAIBet(p2Hand, 0, cardsInRound, false); // false = no es el último
        }

        InteractionManager.Instance.SetInfoMessage($"IA APUESTA: {p2Bet}");
        yield return new WaitForSeconds(1.0f);

        // Ahora le damos el turno a P1 para que responda a la IA
        isP1Choosing = true;
        SetupUIForP1(true); // true = P1 es el último (Sufre Regla Prohibida)
    }

    // --- CORRUTINA 2: IA ES SEGUNDA (Responde al Jugador) ---
    IEnumerator AIBetsSecondRoutine()
    {
        titleText.text = "IA ESTÁ PENSANDO...";
        yield return new WaitForSeconds(1.5f);

        if (cardsInRound == 1)
        {
            var p1Hand = GetCardsFromGroup(InteractionManager.Instance.handGroupP1);
            Card p1VisibleCard = (p1Hand.Count > 0) ? p1Hand[0] : null;
            p2Bet = AIController.Instance.CalculateBlindBet(p1VisibleCard, p1Bet, true); // true = es el último
        }
        else
        {
            var p2Hand = GetCardsFromGroup(InteractionManager.Instance.handGroupP2);
            p2Bet = AIController.Instance.CalculateAIBet(p2Hand, p1Bet, cardsInRound, true); // true = es el último
        }

        InteractionManager.Instance.SetInfoMessage($"IA APUESTA: {p2Bet}");

        yield return new WaitForSeconds(1.0f);
        StartCoroutine(EndBettingPhase());
    }
    IEnumerator EndBettingPhase()
    {
        panelRoot.SetActive(false);
        tableObject.SetActive(true);

        if (cardsInRound == 1)
            InteractionManager.Instance.ResolveBlindRoundImmediate();
        else
            InteractionManager.Instance.InitializeGame(); // Inicia la fase de jugar cartas

        yield return null;
    }
    // Helpers
    private void EnableButtons(bool enable) { foreach (var b in betButtons) b.gameObject.SetActive(enable); }
    private void DisableButtons() { foreach (var b in betButtons) b.gameObject.SetActive(false); }

    // Método auxiliar para convertir UICards visuales a Datos Card
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