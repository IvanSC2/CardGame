using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cerebro de la IA. Contiene 7 niveles de dificultad (0-6).
/// 0 Ultra Easy  — Juega al azar total.
/// 1 Easy        — Lógica base con un 30% de torpeza.
/// 2 Normal      — Lógica matemática correcta.
/// 3 Difficult   — Memoria de cartas ya jugadas en esta ronda.
/// 4 Hard        — Sanguinario: sabotea al humano desde 2 vidas. Consciente de sus propias vidas.
/// 5 UltraHard   — Trampa ligera: lee el mazo restante y elimina cartas de rivales.
/// 6 Impossible  — Trampa total: lee la mano del jugador humano + apuestas rivales.
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

            // Nivel 3+: si TODAS las cartas más altas del mismo palo ya fueron jugadas,
            // mi carta es ahora dominante en ese palo → sube de valor.
            // CORRECCIÓN: antes la lógica era al revés (boosteaba cuando quedaban superiores).
            if (diff >= 3 && _cardsPlayedThisRound.Count > 0)
            {
                // ¿Existe alguna carta de mayor valor en el mismo palo que NO haya sido jugada?
                // Si NO existe (todas fueron jugadas), mi carta es líder de palo.
                bool myCardIsLeaderOfSuit = !_cardsPlayedThisRound
                    .Any(c => c.suit == card.suit && c.value > card.value)
                    && !myHand.Any(c => c != card && c.suit == card.suit && c.value > card.value);

                if (myCardIsLeaderOfSuit) power = Mathf.Min(power + 0.35f, 1.0f);
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

        // Nivel 4+: conciencia de vidas propias
        // Si el bot tiene 1 vida, apuesta conservador (prefiere 0 o la estimación exacta)
        if (diff >= 4 && InteractionManager.Instance != null)
        {
            int mySeat = GetMyAISeat();
            if (mySeat >= 0)
            {
                int myLives = InteractionManager.Instance.vidas[mySeat];
                if (myLives == 1)
                {
                    // Con 1 vida: redondear a la baja para ser conservador
                    finalBet = Mathf.Max(0, Mathf.FloorToInt(estimatedWins));
                }
            }
        }

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

        // Nivel 4+: si mi carta (oculta) es potencialmente mala, apuesta 0 con 1 vida
        if (diff >= 4 && InteractionManager.Instance != null)
        {
            int mySeat = GetMyAISeat();
            if (mySeat >= 0 && InteractionManager.Instance.vidas[mySeat] == 1)
                prob -= 0.30f; // Con 1 vida, apuesta 0 en ciega casi siempre
        }

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
        // Prioriza hundir al jugador humano si tiene 2 vidas o menos.
        if (diff >= 4)
        {
            UICard antiPlayerCard = TryAntiPlayerMove(aiHandUI, cardsOnTable, needToWin);
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

        // Nivel 3+: si tenemos memoria y la carta de la mesa es imbatible,
        // no desperdiciamos cartas altas intentando superarla.
        if (diff >= 3 && _cardsPlayedThisRound.Count > 0)
        {
            // La carta de la mesa es imbatible si no ha sido jugada ninguna superior
            // Y en nuestra mano tampoco hay ninguna que la supere de ese palo
            bool tableCardIsUnbeatable = !_cardsPlayedThisRound
                .Any(c => c.suit == winningOnTable.suit && c.value > winningOnTable.value);

            if (tableCardIsUnbeatable && needToWin)
            {
                // Esa carta es la reina del palo → tiramos basura para no desperdiciar
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

    /// <summary>
    /// Nivel 4: sabotea al jugador humano si tiene 2 vidas o menos.
    /// MEJORADO: también actúa cuando el bot va primero (no solo cuando responde).
    /// </summary>
    private UICard TryAntiPlayerMove(List<UICard> myHand, List<Card> table, bool needToWin)
    {
        if (InteractionManager.Instance == null) return null;

        int humanSeat  = InteractionManager.Instance.MySeatIndex;
        int humanLives = InteractionManager.Instance.vidas[humanSeat];

        // Activar modo sanguinario si el humano tiene 2 vidas o menos (antes solo 1)
        if (humanLives > 2) return null;

        int humanBazas   = InteractionManager.Instance.bazasGanadas[humanSeat];
        int humanApuesta = InteractionManager.Instance.apuestas[humanSeat];

        // Determinar si el humano necesita ganar o perder bazas para cumplir su apuesta
        bool humanNeedsWin  = humanBazas < humanApuesta;
        bool humanNeedsLose = humanBazas >= humanApuesta;

        List<UICard> sorted = myHand.OrderBy(c => GetCardScore(c.cardData)).ToList();

        // ── El humano ya tiró en esta baza ──────────────────────────────────────
        if (table.Count > 0)
        {
            Card winnerOnTable = GetCurrentWinningCard(table);
            int  winScore      = GetCardScore(winnerOnTable);

            // Si el humano está ganando la baza y NO le conviene ganarla → la robamos
            bool humanIsWinning = winnerOnTable.id == GetHumanCardOnTable(table)?.id;
            if (humanIsWinning && humanNeedsLose)
            {
                List<UICard> beaters = myHand
                    .Where(c => GetCardScore(c.cardData) > winScore)
                    .OrderBy(c => GetCardScore(c.cardData))
                    .ToList();
                if (beaters.Count > 0) return beaters[0]; // robarle la baza con lo mínimo
            }

            // Si el humano necesita ganar y está perdiendo → NO le dejamos ganar (ya lo hace la lógica base)
            // Pero si nosotros podemos ganar para impedírselo, mejor
            if (humanNeedsWin)
            {
                List<UICard> beaters = myHand
                    .Where(c => GetCardScore(c.cardData) > winScore)
                    .OrderBy(c => GetCardScore(c.cardData))
                    .ToList();
                if (beaters.Count > 0) return beaters[0];
            }
        }

        // ── El bot va primero ────────────────────────────────────────────────────
        // Si el humano necesita perder bazas: tiramos nuestra carta más fuerte para forzarle a ganar
        if (table.Count == 0 && humanNeedsLose)
        {
            return GetHighestPowerCard(myHand);
        }
        // Si el humano necesita ganar bazas: tiramos nuestra carta más fuerte para robarle la baza
        if (table.Count == 0 && humanNeedsWin)
        {
            return GetHighestPowerCard(myHand);
        }

        return null;
    }

    /// <summary>
    /// Nivel 5: lee el mazo restante Y elimina las cartas conocidas para
    /// deducir qué tienen los rivales. Elige la carta mínima garantizada.
    /// </summary>
    private UICard ChooseCardDeckReader(List<UICard> hand, List<Card> table, bool needToWin)
    {
        // Construimos el conjunto de IDs de cartas que NO están en manos ajenas:
        // = las que quedan en el mazo (aún no repartidas) + las ya jugadas + las que YO tengo
        HashSet<int> myHandIds = new HashSet<int>(hand.Select(c => c.cardData.id));
        HashSet<int> playedIds = new HashSet<int>(_cardsPlayedThisRound.Select(c => c.id));
        HashSet<int> deckIds   = new HashSet<int>();
        if (CardDatabase.deck != null)
            foreach (Card c in CardDatabase.deck) deckIds.Add(c.id);

        // Cartas que DEFINITIVAMENTE están en manos de rivales:
        // ni en el mazo, ni jugadas, ni en mi mano
        // (no podemos listarlo exactamente sin saber IDs totales, pero sí podemos
        //  saber cuántas cartas hay en juego que no controlamos)

        List<UICard> sorted = hand.OrderByDescending(c => GetCardScore(c.cardData)).ToList();

        if (table.Count == 0)
            return needToWin ? sorted[0] : sorted[sorted.Count - 1];

        Card tableWinner = GetCurrentWinningCard(table);
        int  tableScore  = GetCardScore(tableWinner);

        // Cartas que nos permiten ganar
        List<UICard> guaranteed = sorted
            .Where(c => GetCardScore(c.cardData) > tableScore)
            .ToList();
        // Ya están ordenadas de mayor a menor; para ganar con lo mínimo tomamos la última
        List<UICard> losers = sorted
            .Where(c => GetCardScore(c.cardData) < tableScore)
            .ToList();

        // Verificar si la carta ganadora en mesa es realmente imbatible
        // (todas las cartas superiores ya fueron jugadas o están en nuestro mazo → nadie más puede ganarle)
        bool tableIsUnbeatable = !_cardsPlayedThisRound
            .Any(c => c.suit == tableWinner.suit && c.value > tableWinner.value)
            && !deckIds.Any(id => {
                // Si hay una carta superior en el mazo aún sin repartir, puede estar en manos de alguien
                return false; // simplificación: el mazo ya fue repartido en la fase de juego
            });

        if (needToWin)
            // Ganar con la carta mínima necesaria (preservar las buenas para más bazas)
            return guaranteed.Count > 0 ? guaranteed[guaranteed.Count - 1] : sorted[sorted.Count - 1];
        else
            // Perder con la carta más alta posible (no desperdiciar bazas)
            return losers.Count > 0 ? losers[0] : sorted[0];
    }

    /// <summary>
    /// Nivel 6: lee la mano del jugador humano para jugar el contra exacto.
    /// MEJORADO: también considera cuántas bazas le quedan por ganar/perder al humano.
    /// </summary>
    private UICard ChooseCardOmniscient(List<UICard> myHand, List<Card> table, bool needToWin)
    {
        // Leer la mano del humano directamente desde los GameObjects
        List<Card> humanCards = new List<Card>();
        int humanBazas   = 0;
        int humanApuesta = 0;

        if (InteractionManager.Instance != null)
        {
            int humanSeat = InteractionManager.Instance.MySeatIndex;
            humanBazas    = InteractionManager.Instance.bazasGanadas[humanSeat];
            humanApuesta  = InteractionManager.Instance.apuestas[humanSeat];

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

        bool humanNeedsWin  = humanBazas < humanApuesta;
        bool humanNeedsLose = humanBazas >= humanApuesta;

        List<UICard> sorted = myHand.OrderBy(c => GetCardScore(c.cardData)).ToList();

        // ── El humano ya tiró en esta baza ──────────────────────────────────────
        if (table.Count > 0)
        {
            Card tableWinner = GetCurrentWinningCard(table);
            int  tableScore  = GetCardScore(tableWinner);

            if (needToWin)
            {
                // Ganar con lo mínimo posible (dejando las cartas buenas para más tarde)
                UICard minBeater = sorted
                    .FirstOrDefault(c => GetCardScore(c.cardData) > tableScore);
                return minBeater ?? sorted[sorted.Count - 1];
            }
            else
            {
                // Perder: si el humano necesita perder esta baza, dejarle ganar
                // Si el humano necesita ganarla, robarle con lo mínimo posible
                if (humanNeedsLose)
                {
                    // Robarle la baza al humano aunque nosotros no la necesitemos
                    UICard minBeater = sorted
                        .FirstOrDefault(c => GetCardScore(c.cardData) > tableScore);
                    if (minBeater != null) return minBeater;
                }
                // Perder con la carta más alta posible que no gane
                UICard maxLoser = sorted
                    .LastOrDefault(c => GetCardScore(c.cardData) < tableScore);
                return maxLoser ?? sorted[sorted.Count - 1];
            }
        }

        // ── El bot tira primero ──────────────────────────────────────────────────
        if (humanCards.Count > 0)
        {
            int humanBestScore  = humanCards.Max(c => GetCardScore(c));
            int humanWorstScore = humanCards.Min(c => GetCardScore(c));

            if (needToWin)
            {
                // Tirar algo que el humano no pueda superar → garantizamos la baza
                UICard impossible = sorted
                    .LastOrDefault(c => GetCardScore(c.cardData) > humanBestScore);
                if (impossible != null) return impossible;

                // Si no podemos garantizarlo, tiramos la más alta de todas
                return sorted[sorted.Count - 1];
            }
            else
            {
                // No necesitamos ganar esta baza
                if (humanNeedsWin)
                {
                    // El humano necesita ganar → tirar algo que él PUEDA ganar pero que nos
                    // cueste poco (tiramos la carta más alta que el humano aún puede superar)
                    UICard bait = sorted
                        .LastOrDefault(c => GetCardScore(c.cardData) < humanBestScore);
                    if (bait != null) return bait;
                }
                else
                {
                    // El humano NO necesita ganar → tiramos algo muy alto para
                    // obligarle a ganar y que falle su apuesta
                    UICard trap = sorted
                        .LastOrDefault(c => GetCardScore(c.cardData) > humanBestScore);
                    if (trap != null) return trap;
                }
                // Fallback: carta más baja
                return sorted[0];
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

    /// <summary>
    /// Intenta encontrar la carta del jugador humano en la mesa actual.
    /// Compara contra las cartas de la mano del humano registradas.
    /// </summary>
    private Card GetHumanCardOnTable(List<Card> table)
    {
        if (InteractionManager.Instance == null) return null;
        int humanSeat = InteractionManager.Instance.MySeatIndex;
        if (humanSeat < 0 || humanSeat >= InteractionManager.Instance.playerHands.Count) return null;

        // Recogemos los IDs de las cartas que tiene el humano
        Transform humanHand = InteractionManager.Instance.playerHands[humanSeat].transform;
        HashSet<int> humanHandIds = new HashSet<int>();
        foreach (Transform t in humanHand)
        {
            UICard c = t.GetComponent<UICard>();
            if (c != null) humanHandIds.Add(c.cardData.id);
        }

        // La carta del humano en la mesa es la que NO está en su mano ahora
        // (ya la tiró), así que simplemente devolvemos la primera en la mesa
        // que no esté en su mano actual → aproximación razonable
        foreach (Card c in table)
        {
            if (!humanHandIds.Contains(c.id)) return c;
        }
        return null;
    }

    /// <summary>
    /// Obtiene el índice de asiento del bot en la partida actual.
    /// El humano es MySeatIndex; el bot es el otro jugador (en partida 1v1).
    /// </summary>
    private int GetMyAISeat()
    {
        if (InteractionManager.Instance == null) return -1;
        int humanSeat = InteractionManager.Instance.MySeatIndex;
        int totalPlayers = InteractionManager.Instance.vidas.Length;
        // En partidas con más jugadores, los bots son todos los que no son el humano
        // Devolvemos el primer asiento que no sea el humano
        for (int i = 0; i < totalPlayers; i++)
        {
            if (i != humanSeat) return i;
        }
        return -1;
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