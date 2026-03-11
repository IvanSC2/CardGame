using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro; 
//Controlador de la mesa fisica y arbitro del juego
public class TableZone : MonoBehaviour, IPointerClickHandler
{
    public static TableZone Instance;

    [Header("Marcadores Bazas")]
    public TMP_Text scoreTextP1; 
    public TMP_Text scoreTextP2; 

    [Header("Marcadores Vidas")]
    public TMP_Text livesTextP1; 
    public TMP_Text livesTextP2; 

    [Header("Lógica Interna")]
    public int p1Wins = 0;
    public int p2Wins = 0;
    public int bazasJugadas = 0;

    private UICard currentCardP1;
    private UICard currentCardP2;

    private void Awake()
    {
        Instance = this;
    }

    public void ResetStats()
    {
        p1Wins = 0;
        p2Wins = 0;
        bazasJugadas = 0;
        
        ClearTableNow();
        UpdateUI(); 
    }

    public void ClearTableNow()
    {
        foreach (Transform child in this.transform) Destroy(child.gameObject);

        currentCardP1= null;
        currentCardP2= null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InteractionManager.Instance.HasCardSelected())
        {
            UICard cardToMove = InteractionManager.Instance.SelectedCard;
            // Identificamos al dueño ANTES de cambiarle el Parent ---
            bool isP1 = cardToMove.transform.parent == InteractionManager.Instance.handGroupP1.transform;
            bool isP2 = cardToMove.transform.parent == InteractionManager.Instance.handGroupP2.transform;

            // --- Lógica Visual y Movimiento ---
            CardResizer resizer = cardToMove.GetComponent<CardResizer>();
            Vector3 finalScale = Vector3.one;
            if (resizer != null)
            {
                finalScale = resizer.targetVisuals.localScale;
                resizer.enabled = false;
            }
            
            // Mover a la mesa
            cardToMove.transform.SetParent(this.transform);
            cardToMove.transform.localPosition = Vector3.zero;
            cardToMove.transform.localScale = Vector3.one;

            if (isP1) currentCardP1 = cardToMove;
            if (isP2) currentCardP2 = cardToMove;
            
            if (resizer != null) resizer.targetVisuals.localScale = finalScale;
            cardToMove.GetComponent<UnityEngine.UI.Image>().color = Color.white;

            // Desbloquear Raycasts
            CanvasGroup group = cardToMove.GetComponent<CanvasGroup>();
            if (group == null) group = cardToMove.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            InteractionManager.Instance.ClearSelection();

            // Lógica de Juego (Si ya hay 2 cartas)
            if (this.transform.childCount == 2)
            {
                // 1. Pausa visual
                InteractionManager.Instance.isPaused = true;
                InteractionManager.Instance.UpdateVisualStates(); 

                // 2. Comprobar resultado de la baza
                CheckWinner();
                
                // Nota: La limpieza (CleanTableRoutine) se llama desde CheckWinner
                // dependiendo de si se acabó la ronda o no.
            }
            else
            {
                // Si solo hay 1 carta, cambia turno
                InteractionManager.Instance.ChangeTurn();
            }
        }
    }

    // Método para la Ronda Ciega
    public void ForceEndRoundAnalysis()
    {
        // Actualizamos textos visuales
        UpdateUI(); 
        
        Debug.Log("--- FIN DE RONDA CIEGA ---");
        
        // Llamamos a la lógica de apuestas existente
        StartCoroutine(WaitAndResolveRound());
    }
    private void CheckWinner()
    {
        // Seguridad por si algo falla
        if (currentCardP1 == null || currentCardP2 == null) 
        {
            Debug.LogError("Error en Mesa: Falta la carta de uno de los jugadores.");
            return;
        }

        int score1 = CalculateScore(currentCardP1.cardData);
        int score2 = CalculateScore(currentCardP2.cardData);
        
        bazasJugadas++; 
        GameState winnerTurn = GameState.WAITING;
        // --- 1. Determinar Ganador de BAZA ---
        if (score1 > score2)
        {
            p1Wins++;
            InteractionManager.Instance.SetInfoMessage("¡JUGADOR 1 GANA LA BAZA!\n");
            winnerTurn = GameState.P1_TURN;
        }
        else if (score2 > score1)
        {
            p2Wins++;
            InteractionManager.Instance.SetInfoMessage("¡JUGADOR 2 GANA LA BAZA!\n");
            winnerTurn = GameState.P2_TURN;
        }

        UpdateUI(); 

        // --- 2. Comprobar Fin de RONDA ---
        int limiteRonda = InteractionManager.Instance.currentRoundCards;

        if (bazasJugadas >= limiteRonda) 
        {
            // === FIN DE LA MANO ===
            Debug.Log($"--- 🏁 FIN DE LA RONDA DE {limiteRonda} CARTAS ---");
            
            // Iniciamos la secuencia de resolución con espera para leer el mensaje anterior
            StartCoroutine(WaitAndResolveRound());
        }
        else
        {
            // === SOLO FIN DE BAZA ===
            StartCoroutine(CleanTableRoutine(winnerTurn));
        }
    }

    // --- Secuencia de Resolución de Ronda ---
    IEnumerator WaitAndResolveRound()
    {
        // Esperamos 1.5s para que el jugador lea "JUGADOR X GANA LA BAZA"
        yield return new WaitForSeconds(1.5f);
        
        ResolverApuestas(); // Esto actualiza el mensaje a "P1 PIERDE VIDA", etc.
        
        // Si NO es Game Over, procedemos a limpiar y preparar la siguiente ronda
        if (InteractionManager.Instance.p1Vidas > 0 && InteractionManager.Instance.p2Vidas > 0)
        {
            StartCoroutine(ResetRoundAfterDelay());
        }
    }

    private void ResolverApuestas()
    {
        int apuestaP1 = BettingManager.Instance.p1Bet;
        int apuestaP2 = BettingManager.Instance.p2Bet;
        
        string mensajeResultado = "";


        InteractionManager.Instance.rondasJugadasTotales++;
        InteractionManager.Instance.p1BazasTotales += p1Wins;
        InteractionManager.Instance.p2BazasTotales += p2Wins;

        // JUGADOR 1
        if (p1Wins == apuestaP1) {
            mensajeResultado += "P1: CUMPLE.\n";
            InteractionManager.Instance.p1ApuestasAcertadas++;
        } else {
            InteractionManager.Instance.p1Vidas--;
            mensajeResultado += "P1: PIERDE VIDA.\n";
        }

        // JUGADOR 2
        if (p2Wins == apuestaP2) {
            mensajeResultado += "P2: CUMPLE.\n";
            InteractionManager.Instance.p1ApuestasAcertadas++;
        } else {
            InteractionManager.Instance.p2Vidas--;
            mensajeResultado += "P2: PIERDE VIDA.\n";
        }

        // Mostrar resultado final en el Panel InfoLine
        InteractionManager.Instance.SetInfoMessage(mensajeResultado);
        UpdateUI();

        // Chequeo de Muerte
        if (InteractionManager.Instance.p1Vidas <= 0 || InteractionManager.Instance.p2Vidas <= 0)
        {
            string ganador = "";
            if (InteractionManager.Instance.p1Vidas > 0) ganador = "JUGADOR 1";
            else if (InteractionManager.Instance.p2Vidas > 0) ganador = "JUGADOR 2";
            else ganador = "NADIE";

            PauseManager.Instance.TriggerGameOver(ganador);
        }
    }

    // --- Corrutinas de Limpieza ---

    IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(3.0f); // 3s para leer quién perdió vida
        
        ClearTableNow();
        
        bazasJugadas = 0;
        p1Wins = 0;
        p2Wins = 0;
        UpdateUI();

        // Preparamos la siguiente ronda (5 -> 4 -> 3...)
        InteractionManager.Instance.AdvanceRoundSequence();
        
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetInfoMessage($"Ronda terminada.\nSiguiente ronda: {InteractionManager.Instance.currentRoundCards} cartas.");
    }

    IEnumerator CleanTableRoutine(GameState nextTurn)
    {
        yield return new WaitForSeconds(1.5f);
        ClearTableNow();
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetTurn(nextTurn);
    }

    IEnumerator GameOverSequence()
    {
        InteractionManager.Instance.isPaused = true;
        
        yield return new WaitForSeconds(4.0f); // Tiempo para celebrar

        ClearTableNow();
        bazasJugadas = 0;
        p1Wins = 0;
        p2Wins = 0;
        UpdateUI();

        // Reinicio Total
       // InteractionManager.Instance.ResetGameTotal();

        InteractionManager.Instance.isPaused = false;
        UpdateUI(); 

        InteractionManager.Instance.SetInfoMessage("NUEVA PARTIDA.\nPulsa 'Get Hand' para empezar.");
    }

    // --- Helpers UI y Lógica ---

    private void UpdateUI()
    {
        scoreTextP1?.SetText($"P1: {p1Wins}");
        scoreTextP2?.SetText($"P2: {p2Wins}");

        if (InteractionManager.Instance != null)
        {
            string p1Corazones = GetHeartsString(InteractionManager.Instance.p1Vidas);
            string p2Corazones = GetHeartsString(InteractionManager.Instance.p2Vidas);

            livesTextP1?.SetText(p1Corazones);
            livesTextP2?.SetText(p2Corazones);
            
            if (livesTextP1 != null) 
                livesTextP1.color = (InteractionManager.Instance.p1Vidas == 1) ? Color.red : Color.white;
                
            if (livesTextP2 != null) 
                livesTextP2.color = (InteractionManager.Instance.p2Vidas == 1) ? Color.red : Color.white;
        }
    }

    private string GetHeartsString(int lives)
    {
        if (lives <= 0) return "";
        return new string('♥', lives); 
    }

    private int CalculateScore(Card card)
    {
        int suitValue = 0;
        switch (card.suit)
        {
            case "Tréboles": suitValue = 1; break;
            case "Picas": suitValue = 2; break;
            case "Corazones": suitValue = 3; break;
            case "Diamantes": suitValue = 4; break;
        }
        return (card.value * 10) + suitValue;
    }
}