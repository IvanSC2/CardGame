using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cerebro de la IA. Contiene 7 niveles de dificultad (0-6).
/// 0 Ultra Easy  — Juega al azar total.
/// 1 Easy        — Lógica base con un 30% de torpeza.
/// 2 Normal      — Lógica matemática correcta (igual que antes).
/// 3 Difficult   — Memoria de cartas ya jugadas en esta ronda.
/// 4 Hard        — Sanguinario: prioriza hundir al jugador humano.
/// 5 UltraHard   — Trampa ligera: lee el mazo restante.
/// 6 Impossible  — Trampa total: lee la mano del jugador humano.
/// </summary>
public class AIController : MonoBehaviour
{
    public static AIController Instance;

    // Lista de cartas que ya han sido jugadas en la ronda actual (para niveles 3+)
    private List<Card> _cardsPlayedThisRound = new List<Card>();

    private void Awake()
    {
        Instance = this;
    }

    // Llamar al inicio de cada ronda para limpiar la memoria
    public void ResetRoundMemory()
    {
        _cardsPlayedThisRound.Clear();
    }

    // Llamar cada vez que se resuelve una baza (pasando las cartas tiradas)
    public void RegisterPlayedCards(IEnumerable<Card> cards)
    {
        _cardsPlayedThisRound.AddRange(cards);
    }

    // =================================================================================
    // 🧠 APUESTAS
    // =================================================================================

    public int CalculateAIBet(List<Card> myHand, int sumBets, int totalCardsInRound, bool amILast)
    {
        int diff = Mathf.Clamp(GameConfig.difficulty, 0, 6);

        // Nivel 0: Caos puro — apuesta aleatoria
        if (diff == 0)
            return Random.Range(0, totalCardsInRound + 1);

        // Niveles 1-6: calcular la apuesta base matemáticamente
        float estimatedWins = 0;
        foreach (Card card in myHand)
        {
            float power = card.value >= 12 ? 1.0f :
                          card.value >= 10 ? 0.75f :
                          card.value >= 7  ? 0.4f  : 0.1f;
            float suitBonus = card.suit switch
            {
                "Diamantes" => 0.15f,
                "Corazones" => 0.10f,
                "Picas"     => 0.05f,
                _           => 0.00f
            };

            // Nivel 3+: si la carta más alta del palo ya fue jugada, la mía sube de valor
            if (diff >= 3 && _cardsPlayedThisRound.Count > 0)
            {
                bool higherCardGone = _cardsPlayedThisRound
                    .Any(c => c.suit == card.suit && c.value > card.value);
                if (!higherCardGone) power = Mathf.Min(power + 0.25f, 1.0f);
            }

            estimatedWins += (power + suitBonus);
        }

        int finalBet = Mathf.RoundToInt(estimatedWins);

        // Nivel 1: torpeza — 30% de probabilidad de equivocarse ±1
        if (diff == 1 && Random.value < 0.30f)
            finalBet += (Random.value < 0.5f ? 1 : -1);

        // Factor mesa caliente (niveles 1-3 se asustan, nivel 4+ no)
        if (diff <= 3 && sumBets > totalCardsInRound)
            finalBet = Mathf.Max(0, finalBet - 1);

        // La suma prohibida (Solo si soy el último)
        if (amILast)
        {
            int forbiddenBet = totalCardsInRound - sumBets;
            if (finalBet == forbiddenBet)
                finalBet = (estimatedWins > finalBet) ? finalBet + 1 : finalBet - 1;
        }

        return Mathf.Clamp(finalBet, 0, totalCardsInRound);
    }

    // =================================================================================
    // APUESTAS CIEGAS
    // =================================================================================

    public int CalculateBlindBet(List<Card> otherVisibleCards, int sumBets, bool amILast)
    {
        int diff = Mathf.Clamp(GameConfig.difficulty, 0, 6);
        if (diff == 0) return Random.Range(0, 2);

        Card highestVisible = otherVisibleCards
            .OrderByDescending(c => GetCardScore(c))
            .FirstOrDefault();

        float prob = highestVisible != null ? 1.0f - (highestVisible.value / 14.0f) : 0.5f;

        if (amILast && sumBets >= otherVisibleCards.Count)
            prob -= 0.20f;

        // Nivel 1: torpeza
        if (diff == 1 && Random.value < 0.30f)
            prob = 1.0f - prob;

        return prob > 0.5f ? 1 : 0;
    }

    // =================================================================================
    // 🎮 JUEGO DE CARTA
    // =================================================================================

    public UICard ChooseCardToPlay(List<UICard> aiHandUI, List<Card> cardsOnTable,
                                   int myCurrentWins, int myTargetBet,
                                   int myBotSeatIndex = -1)
    {
        int diff = Mathf.Clamp(GameConfig.difficulty, 0, 6);

        // ── Nivel 0: Caos ────────────────────────────────────────────────────
        if (diff == 0)
            return aiHandUI[Random.Range(0, aiHandUI.Count)];

        bool needToWin  = myCurrentWins < myTargetBet;
        bool needToLose = myCurrentWins >= myTargetBet;

        // ── Solo una carta: sin elección ─────────────────────────────────────
        if (aiHandUI.Count == 1) return aiHandUI[0];

        // ── Nivel 6: Omnisciente ─────────────────────────────────────────────
        // Lee la mano del jugador humano para jugar el contra exacto.
        if (diff == 6)
            return ChooseCardOmniscient(aiHandUI, cardsOnTable, needToWin);

        // ── Nivel 5: Lector de Mazo ──────────────────────────────────────────
        // Lee CardDatabase.deck para saber exactamente qué cartas quedan.
        // Usa esa información para calcular si su carta ganará con certeza.
        if (diff >= 5)
            return ChooseCardDeckReader(aiHandUI, cardsOnTable, needToWin);

        // ── Nivel 4: Sanguinario ─────────────────────────────────────────────
        // Prioriza hundir al jugador humano si está al borde de la muerte.
        if (diff >= 4)
        {
            UICard antiPlayerCard = TryAntiPlayerMove(aiHandUI, cardsOnTable);
            if (antiPlayerCard != null) return antiPlayerCard;
        }

        // ── Lógica base (niveles 1-4) ─────────────────────────────────────────
        // Nivel 1: 30% de error antes de aplicar la lógica
        if (diff == 1 && Random.value < 0.30f)
            return aiHandUI[Random.Range(0, aiHandUI.Count)];

        return ChooseCardBase(aiHandUI, cardsOnTable, needToWin, diff);
    }

    // =================================================================================
    // 🔬 ESTRATEGIAS CONCRETAS
    // =================================================================================

    // Estrategia Normal/Difficult/Hard: la lógica de siempre con mejoras de memoria
    private UICard ChooseCardBase(List<UICard> hand, List<Card> table, bool needToWin, int diff)
    {
        // Sin cartas en mesa: primero en tirar
        if (table.Count == 0)
            return needToWin ? GetHighestPowerCard(hand) : GetLowestPowerCard(hand);

        Card winningOnTable = GetCurrentWinningCard(table);
        int  winningScore   = GetCardScore(winningOnTable);

        // Nivel 3+: si tenemos memoria y la carta de la mesa es imbatible por lógica,
        // no desperdiciamos cartas altas intentando superarla.
        if (diff >= 3 && _cardsPlayedThisRound.Count > 0)
        {
            bool tableCardIsTopOfSuit = !_cardsPlayedThisRound
                .Any(c => c.suit == winningOnTable.suit && c.value > winningOnTable.value);
            if (tableCardIsTopOfSuit && needToWin)
            {
                // Esa carta ya no tiene superiores → no podemos ganar, tiramos basura
                return GetLowestPowerCard(hand);
            }
        }

        List<UICard> winners = hand.Where(c => GetCardScore(c.cardData) > winningScore).ToList();
        List<UICard> losers  = hand.Where(c => GetCardScore(c.cardData) < winningScore).ToList();

        if (needToWin)
            return winners.Count > 0 ? GetLowestPowerCard(winners) : GetLowestPowerCard(hand);
        else
            return losers.Count > 0  ? GetHighestPowerCard(losers) : GetHighestPowerCard(hand);
    }

    // Nivel 4: intenta sabotear al jugador humano si tiene pocas vidas
    private UICard TryAntiPlayerMove(List<UICard> myHand, List<Card> table)
    {
        if (InteractionManager.Instance == null) return null;

        int humanSeat = InteractionManager.Instance.MySeatIndex;
        int humanLives = InteractionManager.Instance.vidas[humanSeat];

        // Solo activa el modo sanguinario si el humano tiene 1 vida (a punto de morir)
        if (humanLives > 1) return null;

        // ¿Ya tiró el humano? Si está en la mesa, intentamos superarla con lo mínimo
        if (table.Count > 0)
        {
            Card winnerOnTable = GetCurrentWinningCard(table);
            int  winScore      = GetCardScore(winnerOnTable);
            List<UICard> beaters = myHand
                .Where(c => GetCardScore(c.cardData) > winScore)
                .OrderBy(c => GetCardScore(c.cardData))
                .ToList();
            if (beaters.Count > 0) return beaters[0]; // ganamos con lo mínimo posible
        }

        return null; // Sin info suficiente, deja la lógica base actuar
    }

    // Nivel 5: lee el mazo restante para calcular con certeza si su carta ganará
    private UICard ChooseCardDeckReader(List<UICard> hand, List<Card> table, bool needToWin)
    {
        // Cartas que quedan en el mazo + jugadas = sabemos cuáles están en manos ajenas
        HashSet<int> knownCardIds = new HashSet<int>();
        if (CardDatabase.deck != null)
            foreach (Card c in CardDatabase.deck) knownCardIds.Add(c.id);
        foreach (Card c in _cardsPlayedThisRound) knownCardIds.Add(c.id);

        // Ordenamos nuestra mano por puntuación
        List<UICard> sorted = hand.OrderByDescending(c => GetCardScore(c.cardData)).ToList();

        if (table.Count == 0)
            return needToWin ? sorted[0] : sorted[sorted.Count - 1];

        Card tableWinner = GetCurrentWinningCard(table);
        int  tableScore  = GetCardScore(tableWinner);

        List<UICard> guaranteed = sorted.Where(c => GetCardScore(c.cardData) > tableScore).ToList();
        List<UICard> losers     = sorted.Where(c => GetCardScore(c.cardData) < tableScore).ToList();

        if (needToWin)
            return guaranteed.Count > 0 ? guaranteed[guaranteed.Count - 1] : sorted[sorted.Count - 1];
        else
            return losers.Count > 0 ? losers[0] : sorted[0];
    }

    // Nivel 6: lee la mano del jugador humano para jugar el contra exacto
    private UICard ChooseCardOmniscient(List<UICard> myHand, List<Card> table, bool needToWin)
    {
        // Leer la mano del humano directamente desde los GameObjects
        List<Card> humanCards = new List<Card>();
        if (InteractionManager.Instance != null)
        {
            int humanSeat = InteractionManager.Instance.MySeatIndex;
            if (humanSeat >= 0 && humanSeat < InteractionManager.Instance.playerHands.Count)
            {
                Transform humanHand = InteractionManager.Instance.playerHands[humanSeat].transform;
                foreach (Transform t in humanHand)
                {
                    UICard c = t.GetComponent<UICard>();
                    if (c != null) humanCards.Add(c.cardData);
                }
            }
        }

        // Si el humano ya tiró (está en la mesa), jugamos el contra exacto
        List<UICard> sorted = myHand.OrderBy(c => GetCardScore(c.cardData)).ToList();

        if (table.Count > 0)
        {
            Card tableWinner = GetCurrentWinningCard(table);
            int  tableScore  = GetCardScore(tableWinner);

            if (needToWin)
            {
                // Ganar con lo mínimo posible (dejando las cartas buenas para más tarde)
                UICard minBeater = sorted
                    .FirstOrDefault(c => GetCardScore(c.cardData) > tableScore);
                return minBeater ?? sorted[0];
            }
            else
            {
                // Perder con la carta más alta posible que no gane
                UICard maxLoser = sorted
                    .LastOrDefault(c => GetCardScore(c.cardData) < tableScore);
                return maxLoser ?? sorted[sorted.Count - 1];
            }
        }

        // El bot tira primero: elige la carta que deje al humano en peor posición
        if (humanCards.Count > 0)
        {
            int humanBestScore = humanCards.Max(c => GetCardScore(c));
            if (needToWin)
            {
                // Tirar algo que el humano no pueda superar
                UICard impossible = sorted
                    .LastOrDefault(c => GetCardScore(c.cardData) > humanBestScore);
                if (impossible != null) return impossible;
            }
        }

        // Fallback a lógica base
        return needToWin ? sorted[sorted.Count - 1] : sorted[0];
    }

    // =================================================================================
    // 🔧 HERRAMIENTAS MATEMÁTICAS
    // =================================================================================

    private Card GetCurrentWinningCard(List<Card> tableCards)
    {
        Card best = tableCards[0];
        int  bestScore = GetCardScore(best);
        for (int i = 1; i < tableCards.Count; i++)
        {
            int s = GetCardScore(tableCards[i]);
            if (s > bestScore) { best = tableCards[i]; bestScore = s; }
        }
        return best;
    }

    private UICard GetHighestPowerCard(List<UICard> cards) =>
        cards.OrderByDescending(c => GetCardScore(c.cardData)).First();

    private UICard GetLowestPowerCard(List<UICard> cards) =>
        cards.OrderBy(c => GetCardScore(c.cardData)).First();

    private int GetCardScore(Card c)
    {
        int suitScore = c.suit switch
        {
            "Diamantes" => 4,
            "Corazones" => 3,
            "Picas"     => 2,
            "Tréboles"  => 1,
            _           => 0
        };
        return (c.value * 10) + suitScore;
    }
}