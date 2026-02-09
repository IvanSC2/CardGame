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
    // 🧠 FASE 1: CEREBRO DE APUESTAS (Betting Logic)
    // =================================================================================
    
    public int CalculateAIBet(List<Card> myHand, int opponentBet, int totalCardsInRound, bool amILast)
    {
        // 1. ANÁLISIS DE FUERZA BRUTA (La IA mira sus cartas)
        // Cuantas cartas creo que son "ganadoras" por sí mismas
        float estimatedWins = 0;

        foreach (Card card in myHand)
        {
            float power = 0;

            // REGLA: A es bajo (1), K es alto (13)
            // Asignamos probabilidad de victoria según el valor
            if (card.value >= 12) power = 1.0f;      // Q y K: Casi seguro ganan
            else if (card.value >= 10) power = 0.75f; // 10 y J: Probables
            else if (card.value >= 7) power = 0.4f;   // Medias: Dudoso
            else power = 0.1f;                        // Bajas: Probablemente pierdan

            // AJUSTE POR PALO (Diamantes > Corazones > Picas > Tréboles)
            // Un Rey de Diamantes es mejor que un Rey de Tréboles
            float suitBonus = 0;
            switch (card.suit)
            {
                case "Diamantes": suitBonus = 0.15f; break; // El más fuerte
                case "Corazones": suitBonus = 0.10f; break;
                case "Picas":     suitBonus = 0.05f; break;
                case "Tréboles":  suitBonus = 0.00f; break; // El más débil
            }

            estimatedWins += (power + suitBonus);
        }

        // Redondeamos al entero más cercano
        int finalBet = Mathf.RoundToInt(estimatedWins);

        // 2. FACTOR HUMANO (Si soy segundo, miro tu apuesta)
        if (amILast)
        {
            // Si el jugador ha apostado MUCHO (más de la mitad), la IA se asusta y reduce 1
            if (opponentBet > (totalCardsInRound / 2))
            {
                finalBet = Mathf.Max(0, finalBet - 1);
                Debug.Log("IA: El jugador va fuerte, yo me protejo y bajo mi apuesta.");
            }
            
            // REGLA DE ORO: La suma prohibida
            int forbiddenBet = totalCardsInRound - opponentBet;
            if (finalBet == forbiddenBet)
            {
                // Conflicto: Tengo que cambiar mi apuesta
                // Si mi cálculo decía 2 y no puedo decir 2:
                // -> Si tengo cartas fuertes, subo a 3.
                // -> Si mis cartas eran mediocres, bajo a 1.
                if (estimatedWins > finalBet) finalBet++; // Tendencia alcista
                else finalBet--; // Tendencia bajista
                
                Debug.Log("IA: Ajuste por Regla Prohibida.");
            }
        }

        // Seguridad (no puede ser < 0 ni > cartas en mano)
        finalBet = Mathf.Clamp(finalBet, 0, totalCardsInRound);

        return finalBet;
    }

    // =================================================================================
    // 🧠 FASE 2: CEREBRO DE JUEGO (Play Logic) - Matemático
    // =================================================================================

    public UICard ChooseCardToPlay(List<UICard> aiHandUI, Card cardOnTable, int myCurrentWins, int myTargetBet)
    {
        // 1. IDENTIFICAR ESTADO: ¿Necesito ganar esta baza?
        bool needToWin = myCurrentWins < myTargetBet;
        
        // Si ya he cumplido mi apuesta (o me he pasado), necesito PERDER a toda costa.
        bool needToLose = myCurrentWins >= myTargetBet;

        // 2. FILTRAR CARTAS LEGALES (Asistir al palo)
        List<UICard> legalCards = GetLegalCards(aiHandUI, cardOnTable);

        // Si solo tengo una, no hay elección
        if (legalCards.Count == 1) return legalCards[0];

        // --- ESCENARIO A: SOY EL PRIMERO EN TIRAR (Mesa vacía) ---
        if (cardOnTable == null)
        {
            if (needToWin)
            {
                // Tiro mi carta MÁS FUERTE para asegurar
                // (Mejor si es Diamantes/Corazones)
                return GetHighestPowerCard(legalCards);
            }
            else
            {
                // Tiro mi carta MÁS DÉBIL para no ganar accidentalmente
                // (Mejor si es Trébol/Picas y valor bajo)
                return GetLowestPowerCard(legalCards);
            }
        }

        // --- ESCENARIO B: SOY EL SEGUNDO (Respondo a una carta) ---
        else 
        {
            // Buscamos cuáles de mis cartas ganan a la de la mesa
            // (Asumiendo que debo asistir al palo para ganar)
            List<UICard> winningCards = legalCards.Where(c => CanBeat(c.cardData, cardOnTable)).ToList();
            List<UICard> losingCards = legalCards.Where(c => !CanBeat(c.cardData, cardOnTable)).ToList();

            if (needToWin)
            {
                // INTENTO GANAR
                if (winningCards.Count > 0)
                {
                    // MATEMÁTICA: Gano con la carta más baja posible que supere a la suya
                    // (Para no desperdiciar un Rey matando a un 2)
                    return GetLowestPowerCard(winningCards);
                }
                else
                {
                    // No puedo ganar. Tiro la carta más inútil (la más baja)
                    return GetLowestPowerCard(losingCards);
                }
            }
            else // needToLose
            {
                // INTENTO PERDER
                if (losingCards.Count > 0)
                {
                    // Tiro la carta más ALTA que pierda.
                    // (Me deshago de cartas peligrosas como Reyes de otros palos)
                    return GetHighestPowerCard(losingCards);
                }
                else
                {
                    // ¡Maldición! Estoy obligado a ganar (solo tengo cartas superiores).
                    // Gano con la más alta para gastar "munición pesada" ya que voy a fallar igual.
                    return GetHighestPowerCard(winningCards);
                }
            }
        }
    }

    // =================================================================================
    // 🔧 HERRAMIENTAS MATEMÁTICAS INTERNAS
    // =================================================================================

    private List<UICard> GetLegalCards(List<UICard> hand, Card tableCard)
    {
        if (tableCard == null) return hand; // Soy mano, todo vale

        // Buscar cartas del mismo palo
        var suitedCards = hand.Where(x => x.cardData.suit == tableCard.suit).ToList();

        // REGLA: Si tengo del palo, OBLIGADO a tirar del palo
        if (suitedCards.Count > 0) return suitedCards;
        
        // Si no tengo, puedo tirar cualquiera (pero no ganaré la baza, salvo triunfo)
        return hand;
    }
    public int CalculateBlindBet(Card playerVisibleCard, int opponentBet, bool amILast)
    {
        // 1. ANÁLISIS VISUAL (Lo que la IA ve en tu frente)
        // Probabilidad base: Si tú tienes carta alta, yo pierdo. Si tienes baja, yo gano.
        float probabilityOfWinning = 0.5f; 

        if (playerVisibleCard != null)
        {
            // Asumimos K=13, A=1. Cuanto más alta tu carta, menos chance tengo yo.
            probabilityOfWinning = 1.0f - (playerVisibleCard.value / 14.0f);
        }

        // 2. PSICOLOGÍA (Leer la mente del jugador)
        // Solo aplica si la IA responde a tu apuesta (amILast = true)
        if (amILast)
        {
            if (opponentBet == 1) 
            {
                // JUGADOR DICE QUE GANA -> La IA asume que ella tiene una carta MALA (baja).
                // "Si él se atreve, es porque yo soy débil".
                probabilityOfWinning -= 0.25f; 
                Debug.Log("IA (Pensamiento): Apuestas ganar... ¿Tan mala es mi carta?");
            }
            else 
            {
                // JUGADOR DICE QUE PIERDE -> La IA asume que ella tiene una carta BUENA (alta).
                // "Si él se retira, es porque yo soy fuerte".
                probabilityOfWinning += 0.25f;
                Debug.Log("IA (Pensamiento): Te achicas... Debo tener un buen naipe.");
            }
        }

        // 3. DECISIÓN FINAL
        // Si tras ver tu carta y tu actitud, me veo con >50% opciones, voy.
        return probabilityOfWinning > 0.5f ? 1 : 0;
    }
    private bool CanBeat(Card myCard, Card enemyCard)
    {
        // 1. Si no son del mismo palo, automáticamente PIERDO 
        // (Asumiendo que no hay triunfo o que no tengo triunfo)
        if (myCard.suit != enemyCard.suit) return false;

        // 2. Si son del mismo palo, gana el valor más alto
        return myCard.value > enemyCard.value;
    }

    // Devuelve la carta con más "Poder" (Valor alto + Palo fuerte)
    private UICard GetHighestPowerCard(List<UICard> cards)
    {
        return cards.OrderByDescending(c => GetCardScore(c.cardData)).First();
    }

    // Devuelve la carta con menos "Poder"
    private UICard GetLowestPowerCard(List<UICard> cards)
    {
        return cards.OrderBy(c => GetCardScore(c.cardData)).First();
    }

    // Tu sistema de puntuación para desempatar:
    // Valor * 10 + Palo (Diamante=3 ... Trébol=0)
    private int GetCardScore(Card c)
    {
        int suitScore = 0;
        switch (c.suit)
        {
            case "Diamantes": suitScore = 3; break;
            case "Corazones": suitScore = 2; break;
            case "Picas":     suitScore = 1; break;
            case "Tréboles":  suitScore = 0; break;
        }
        return (c.value * 10) + suitScore;
    }
}