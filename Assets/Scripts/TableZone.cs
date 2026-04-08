using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic; 
using TMPro; 

public class TableZone : MonoBehaviour, IPointerClickHandler
{
    public static TableZone Instance;

    [Header("Marcadores Viejos (Opcional, los Perfiles ya hacen esto)")]
    public TMP_Text scoreTextP1; 
    public TMP_Text scoreTextP2; 
    public TMP_Text livesTextP1; 
    public TMP_Text livesTextP2; 

    [Header("Lógica Interna Multijugador")]
    public int bazasJugadas = 0;
    
    // Guarda la carta que ha tirado cada jugador y su índice
    private Dictionary<int, UICard> cartasEnMesa = new Dictionary<int, UICard>();

    private void Awake()
    {
        Instance = this;
    }

    public void ResetStats()
    {
        if (InteractionManager.Instance != null && InteractionManager.Instance.bazasGanadas != null)
        {
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
            {
                InteractionManager.Instance.bazasGanadas[i] = 0;
            }
        }
        bazasJugadas = 0;
        
        ClearTableNow();
        UpdateUI(); 
    }

    public void ClearTableNow()
    {
        foreach (Transform child in this.transform) Destroy(child.gameObject);
        cartasEnMesa.Clear();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InteractionManager.Instance.HasCardSelected())
        {
            UICard cardToMove = InteractionManager.Instance.SelectedCard;
            Transform manoOriginal = cardToMove.transform.parent;
            
            // --- IDENTIFICAMOS AL DUEÑO UNIVERSAL ---
            int duenoIndex = -1;
            for (int i = 0; i < InteractionManager.Instance.playerHands.Count; i++)
            {
                if (cardToMove.transform.parent == InteractionManager.Instance.playerHands[i].transform)
                {
                    duenoIndex = i;
                    break;
                }
            }

            // --- Lógica Visual y Movimiento ---
            CardResizer resizer = cardToMove.GetComponent<CardResizer>();
            Vector3 finalScale = Vector3.one;
            if (resizer != null)
            {
                finalScale = resizer.targetVisuals.localScale;
                resizer.enabled = false;
            }
            
            cardToMove.transform.SetParent(this.transform);
            cardToMove.transform.localPosition = Vector3.zero;
            cardToMove.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            cardToMove.transform.localEulerAngles = Vector3.zero;

            if (manoOriginal != null)
            {
                HandLayoutFanner fanner = manoOriginal.GetComponent<HandLayoutFanner>();
                if (fanner != null) fanner.ReorganizarCartas();
            }

            if (duenoIndex != -1) cartasEnMesa[duenoIndex] = cardToMove;
            
            if (resizer != null) resizer.targetVisuals.localScale = finalScale;
            cardToMove.GetComponent<UnityEngine.UI.Image>().color = Color.white;

            CanvasGroup group = cardToMove.GetComponent<CanvasGroup>();
            if (group == null) group = cardToMove.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            InteractionManager.Instance.ClearSelection();

            // Detectar si ahn tirado todos los jugadores
            int jugadoresVivos = 0;
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
            }

            if (this.transform.childCount == jugadoresVivos)
            {
                InteractionManager.Instance.isPaused = true;
                InteractionManager.Instance.UpdateVisualStates(); 
                CheckWinner();
            }
            else
            {
                InteractionManager.Instance.ChangeTurn();
            }
        }
    }

    public void ForceEndRoundAnalysis()
    {
        UpdateUI(); 
        Debug.Log("--- FIN DE RONDA CIEGA ---");
        StartCoroutine(WaitAndResolveRound());
    }

    private void CheckWinner()
    {
        bazasJugadas++; 
        
        int maxScore = -1;
        int winnerIndex = -1;
        List<string> registroCartas = new List<string>();

        // --- CALCULAR EL GANADOR ---
        foreach (var kvp in cartasEnMesa)
        {
            int playerIndex = kvp.Key;
            UICard card = kvp.Value;
            
            int score = CalculateScore(card.cardData);
            string nombreJugador = (playerIndex == 0) ? "TÚ" : $"BOT {playerIndex}";
            registroCartas.Add($"{nombreJugador} [{card.cardData.rank} de {card.cardData.suit} = {score} pts]");
            
            if (score > maxScore)
            {
                maxScore = score;
                winnerIndex = playerIndex;
            }
        }

        Debug.Log($"<color=cyan>--- RESULTADO BAZA --- {string.Join(", ", registroCartas)}</color>");

        // --- ASIGNAR LA VICTORIA AL ARRAY MAESTRO ---
        if (winnerIndex != -1)
        {
            InteractionManager.Instance.bazasGanadas[winnerIndex]++;
            
            if (winnerIndex == 0) InteractionManager.Instance.SetInfoMessage("¡TÚ GANAS LA BAZA!\n");
            else InteractionManager.Instance.SetInfoMessage($"¡EL BOT {winnerIndex} GANA LA BAZA!\n");
        }
        
        UpdateUI(); 

        int limiteRonda = InteractionManager.Instance.currentRoundCards;
        if (bazasJugadas >= limiteRonda) 
        {
            Debug.Log($"--- 🏁 FIN DE LA RONDA DE {limiteRonda} CARTAS ---");
            StartCoroutine(WaitAndResolveRound());
        }
        else
        {
            StartCoroutine(CleanTableRoutine(winnerIndex));
        }
    }

    IEnumerator WaitAndResolveRound()
    {
        yield return new WaitForSeconds(1.5f);
        ResolverApuestas(); 
    }

    // =======================================================================
    // JUEZ UNIVERSAL DE LAS APUESTAS 
    // =======================================================================
    private void ResolverApuestas()
    {
        string mensajeResultado = "RESULTADOS:\n";
        InteractionManager.Instance.rondasJugadasTotales++;
        int totalPlayers = InteractionManager.Instance.totalPlayers;

        // Guardamos si estabas vivo ANTES de la sentencia
        bool p1EstabaVivo = InteractionManager.Instance.vidas[0] > 0;
        int jugadoresVivos = 0;

        for (int i = 0; i < totalPlayers; i++)
        {
            // ¡NUEVO!: Si este jugador ya estaba muerto, lo ignoramos por completo
            if (InteractionManager.Instance.vidas[i] <= 0) continue;

            int bazas = InteractionManager.Instance.bazasGanadas[i];
            int apuesta = InteractionManager.Instance.apuestas[i];
            string nombre = (i == 0) ? "TÚ" : $"BOT {i}";

            InteractionManager.Instance.bazasTotales[i] += bazas;

            if (bazas == apuesta)
            {
                mensajeResultado += $"{nombre}: CUMPLE.\n";
                InteractionManager.Instance.apuestasAcertadasTotales[i]++;
            }
            else
            {
                InteractionManager.Instance.vidas[i]--; // ¡Hachazo!
                mensajeResultado += $"{nombre}: FALLA (-1 Vida).\n";
            }

            // Contamos cuántos siguen vivos en la mesa
            if (InteractionManager.Instance.vidas[i] > 0)
            {
                jugadoresVivos++;
            }
        }

        InteractionManager.Instance.SetInfoMessage(mensajeResultado);
        UpdateUI();

        // --- COMPROBACIÓN DE PUESTOS ---
        bool p1SigueVivo = InteractionManager.Instance.vidas[0] > 0;

        if (p1EstabaVivo && !p1SigueVivo)
        {
            // Acabas de morir.
            puestoP1Temp = jugadoresVivos + 1;
            Invoke("TriggerFin", 2.0f);

            
            if (jugadoresVivos > 1) 
            {
                StartCoroutine(ResetRoundAfterDelay());
            }
        }
        else if (jugadoresVivos <= 1)
        {
            // Fin de la partida total Todos han muerto (o solo queda uno)
            if (p1SigueVivo) puestoP1Temp = 1;
            Invoke("TriggerFin", 2.0f);
        }
        else
        {
            // La partida sigue con normalidad
            StartCoroutine(ResetRoundAfterDelay());
        }
    }

   private int puestoP1Temp; // Variable temporal numérica
    private void TriggerFin()
    {
        if(PauseManager.Instance != null)
            PauseManager.Instance.TriggerGameOver(puestoP1Temp);
    }

    

    IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(3.0f); 
        
        ClearTableNow();
        bazasJugadas = 0;
        
        // Reseteamos las bazas y las apuestas para la nueva ronda
        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
        {
            InteractionManager.Instance.bazasGanadas[i] = 0;
            InteractionManager.Instance.apuestas[i] = -1;
        }
        UpdateUI();

        InteractionManager.Instance.AdvanceRoundSequence();
        
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetInfoMessage($"Ronda terminada.\nSiguiente: {InteractionManager.Instance.currentRoundCards} cartas.");
    }

    IEnumerator CleanTableRoutine(int nextTurn)
    {
        yield return new WaitForSeconds(1.5f);
        ClearTableNow();
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetTurn(nextTurn);
    }

    private void UpdateUI()
    {
        // 1. Actualizamos la nueva UI dinámica (Los Perfiles)
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ActualizarTodosLosPerfilesUI();

            // 2. Por si sigues usando los textos antiguos en el Canvas general
            if (InteractionManager.Instance.totalPlayers > 1)
            {
                scoreTextP1?.SetText($"P1: {InteractionManager.Instance.bazasGanadas[0]}");
                scoreTextP2?.SetText($"P2: {InteractionManager.Instance.bazasGanadas[1]}");
                
                livesTextP1?.SetText(GetHeartsString(InteractionManager.Instance.vidas[0]));
                livesTextP2?.SetText(GetHeartsString(InteractionManager.Instance.vidas[1]));
                
                if (livesTextP1 != null) livesTextP1.color = (InteractionManager.Instance.vidas[0] == 1) ? Color.red : Color.white;
                if (livesTextP2 != null) livesTextP2.color = (InteractionManager.Instance.vidas[1] == 1) ? Color.red : Color.white;
            }
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