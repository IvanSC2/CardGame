using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AIController : MonoBehaviour
{
    public static AIController Instance;

    private void Awake()
    {
        Instance = this;
    }

    // =================================================================================
    // 🧠 FASE 1: CEREBRO DE APUESTAS NORMALES
    // =================================================================================
    
    public int CalculateAIBet(List<Card> myHand, int sumBets, int totalCardsInRound, bool amILast)
    {
        float estimatedWins = 0;

        foreach (Card card in myHand)
        {
            float power = 0;
            if (card.value >= 12) power = 1.0f;      // Q, K, A
            else if (card.value >= 10) power = 0.75f; 
            else if (card.value >= 7) power = 0.4f;   
            else power = 0.1f;                        

            float suitBonus = 0;
            switch (card.suit)
            {
                case "Diamantes": suitBonus = 0.15f; break; 
                case "Corazones": suitBonus = 0.10f; break;
                case "Picas":     suitBonus = 0.05f; break;
                case "Tréboles":  suitBonus = 0.00f; break; 
            }
            estimatedWins += (power + suitBonus);
        }

        int finalBet = Mathf.RoundToInt(estimatedWins);

        // FACTOR MESA CALIENTE (Si la suma de apuestas ya es muy alta)
        if (sumBets > totalCardsInRound)
        {
            finalBet = Mathf.Max(0, finalBet - 1);
        }
            
        // La suma prohibida (Solo si soy el último)
        if (amILast)
        {
            int forbiddenBet = totalCardsInRound - sumBets;
            if (finalBet == forbiddenBet)
            {
                if (estimatedWins > finalBet) finalBet++; 
                else finalBet--; 
            }
        }

        return Mathf.Clamp(finalBet, 0, totalCardsInRound);
    }

    // =================================================================================
    // FASE 1B: CEREBRO DE APUESTAS CIEGAS
    // =================================================================================
    
    public int CalculateBlindBet(List<Card> otherVisibleCards, int sumBets, bool amILast)
    {
        // 1. Buscamos cuál es la carta más alta que estamos viendo en la mesa
        Card highestVisibleCard = null;
        int maxScore = -1;

        foreach (Card c in otherVisibleCards)
        {
            int score = GetCardScore(c);
            if (score > maxScore)
            {
                maxScore = score;
                highestVisibleCard = c;
            }
        }

        // 2. Calculamos probabilidad frente al RIVAL MÁS FUERTE
        float probabilityOfWinning = 0.5f; 
        if (highestVisibleCard != null)
        {
            probabilityOfWinning = 1.0f - (highestVisibleCard.value / 14.0f);
        }

        // 3. FACTOR PSICOLÓGICO MÚLTIPLE
        if (amILast)
        {
            if (sumBets >= otherVisibleCards.Count) 
                probabilityOfWinning -= 0.20f; 
        }

        return probabilityOfWinning > 0.5f ? 1 : 0;
    }


    // =================================================================================
    // FASE 2: CEREBRO DE JUEGO
    // =================================================================================

    public UICard ChooseCardToPlay(List<UICard> aiHandUI, List<Card> cardsOnTable, int myCurrentWins, int myTargetBet)
    {
        bool needToWin = myCurrentWins < myTargetBet;
        bool needToLose = myCurrentWins >= myTargetBet;

        if (aiHandUI.Count == 1) return aiHandUI[0];

        // --- ESCENARIO A: SOY EL PRIMERO EN TIRAR ---
        if (cardsOnTable.Count == 0)
        {
            if (needToWin) return GetHighestPowerCard(aiHandUI);
            else return GetLowestPowerCard(aiHandUI);
        }

        // --- ESCENARIO B: LA MESA YA TIENE CARTAS ---
        Card currentWinningCard = GetCurrentWinningCard(cardsOnTable);
        int winningScore = GetCardScore(currentWinningCard);

        List<UICard> winningCards = aiHandUI.Where(c => GetCardScore(c.cardData) > winningScore).ToList();
        List<UICard> losingCards = aiHandUI.Where(c => GetCardScore(c.cardData) < winningScore).ToList();

        if (needToWin)
        {
            if (winningCards.Count > 0) return GetLowestPowerCard(winningCards); // Gano con lo justo
            else return GetLowestPowerCard(losingCards); // No puedo ganar, tiro basura
        }
        else // Necesito perder
        {
            if (losingCards.Count > 0) return GetHighestPowerCard(losingCards); // Tiro carta alta que no sirva para ganar
            else return GetHighestPowerCard(winningCards); // Obligado a ganar, me quito la más fuerte de encima
        }
    }

    // =================================================================================
    // 🔧 HERRAMIENTAS MATEMÁTICAS MULTIJUGADOR
    // =================================================================================

    private Card GetCurrentWinningCard(List<Card> tableCards)
    {
        Card bestCard = tableCards[0];
        int bestScore = GetCardScore(bestCard);

        for (int i = 1; i < tableCards.Count; i++)
        {
            int currentScore = GetCardScore(tableCards[i]);
            if (currentScore > bestScore)
            {
                bestCard = tableCards[i];
                bestScore = currentScore;
            }
        }
        return bestCard;
    }

    private UICard GetHighestPowerCard(List<UICard> cards) { return cards.OrderByDescending(c => GetCardScore(c.cardData)).First(); }
    private UICard GetLowestPowerCard(List<UICard> cards) { return cards.OrderBy(c => GetCardScore(c.cardData)).First(); }

    private int GetCardScore(Card c)
    {
        int suitScore = 0;
        switch (c.suit)
        {
            case "Diamantes": suitScore = 4; break;
            case "Corazones": suitScore = 3; break;
            case "Picas":     suitScore = 2; break;
            case "Tréboles":  suitScore = 1; break;
        }
        return (c.value * 10) + suitScore;
    }
}