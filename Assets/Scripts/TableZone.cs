using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic; 
using System.Linq;
using TMPro; 
using Unity.Netcode; 

public class TableZone : NetworkBehaviour, IPointerClickHandler 
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

    // =======================================================================
    // 1. EL CLIC DEL JUGADOR
    // =======================================================================
    public void OnPointerClick(PointerEventData eventData)
    {
        // Si hay una carta seleccionada, solicitamos al servidor que la juegue
        if (InteractionManager.Instance.isPaused) return;
        if (InteractionManager.Instance.HasCardSelected())
        {
            UICard cardToMove = InteractionManager.Instance.SelectedCard;
            Transform manoPadre = cardToMove.transform.parent;
            
            int duenoIndex = -1;
            for (int i = 0; i < InteractionManager.Instance.playerHands.Count; i++)
            {
                if (manoPadre == InteractionManager.Instance.playerHands[i].transform)
                {
                    duenoIndex = i;
                    break;
                }
            }

            // Encontramos la posición exacta de la carta en la mano (para que el resto de jugadores sepan cuál es)
            int indexCartaEnMano = cardToMove.transform.GetSiblingIndex();

            // Si soy yo mismo el que juega (o soy el Host forzando jugada de Bot/AFK), solicito la jugada
            int localId = InteractionManager.Instance.MySeatIndex;
            if (duenoIndex == localId || IsServer)
            {   
                InteractionManager.Instance.ClearSelection();
                // Enviar la petición al Servidor
                SolicitarJugarCartaServerRpc(duenoIndex, indexCartaEnMano);
            }
        }
    }

    // =======================================================================
    // 2. LA AUTORIDAD DEL SERVIDOR (Recibe la petición y ejecuta)
    // =======================================================================
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarJugarCartaServerRpc(int playerIndex, int indexCartaEnMano, RpcParams rpcParams = default)
    {
        // 1. Validar que sea el turno del que la ha tirado
        if (InteractionManager.Instance.currentTurnIndex != playerIndex) return;

        // 2. Avisar a todos los clientes que muevan la carta a la mesa visualmente
        JugarCartaMesaClientRpc(playerIndex, indexCartaEnMano);

        // 3. Comprobar si la ronda se ha terminado en el servidor
        StartCoroutine(ValidarFinTurnoMesa());
    }

    private IEnumerator ValidarFinTurnoMesa()
    {
        yield return new WaitForSeconds(0.1f);
        // RE-CALCULAR vivos ahora, por si alguien se ha desconectado desde que tiró la carta
        int vivosAhora = 0;
        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
        {
            if (InteractionManager.Instance.vidas[i] > 0) vivosAhora++;
        }

        if (cartasEnMesa.Count >= vivosAhora && vivosAhora > 0)
        {
            // Pausar y chequear ganador
            PausarInteraccionClientRpc();
            CheckWinner(); 
        }
        else
        {
            // Siguiente turno
            InteractionManager.Instance.ChangeTurn();
        }
    }

    public void ForzarValidacionMesa()
    {
        if (!IsServer) return;
        int vivosAhora = 0;
        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            if (InteractionManager.Instance.vidas[i] > 0) vivosAhora++;

        if (vivosAhora > 0 && cartasEnMesa.Count >= vivosAhora)
        {
            PausarInteraccionClientRpc();
            CheckWinner();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PausarInteraccionClientRpc()
    {
        InteractionManager.Instance.isPaused = true;
        InteractionManager.Instance.UpdateVisualStates(); 
    }

    // =======================================================================
    // 3. SINCRONIZACIÓN VISUAL DE LA MESA (ClientRpc)
    // =======================================================================
    [Rpc(SendTo.Everyone)]
    private void JugarCartaMesaClientRpc(int playerIndex, int indexCartaEnMano)
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance?.cardDrop);
        if (playerIndex >= InteractionManager.Instance.playerHands.Count) return;
        Transform manoOriginal = InteractionManager.Instance.playerHands[playerIndex].transform;
        if (indexCartaEnMano >= manoOriginal.childCount) return;

        UICard cardToMove = manoOriginal.GetChild(indexCartaEnMano).GetComponent<UICard>();
        if (cardToMove == null) return;

        cardToMove.SetFaceUp(true);

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

        cartasEnMesa[playerIndex] = cardToMove;
        
        if (resizer != null) resizer.targetVisuals.localScale = finalScale;

        
        cardToMove.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        CanvasGroup group = cardToMove.GetComponent<CanvasGroup>();
        if (group == null) group = cardToMove.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 1f;
    }

    // =======================================================================
    // 4. LÓGICA DE GANADOR Y RONDAS (Ejecutado por el Servidor)
    // =======================================================================
    public void ForceEndRoundAnalysis()
    {
        UpdateUI(); 
        Debug.Log("--- FIN DE RONDA CIEGA ---");
        if (IsServer) StartCoroutine(WaitAndResolveRound());
    }

    private void CheckWinner()
    {
        if (!IsServer) return; 

        bazasJugadas++; 
        int maxScore = -1;
        int winnerIndex = -1;
        List<string> registroCartas = new List<string>();

        foreach (var kvp in cartasEnMesa)
        {
            int playerIndex = kvp.Key;
            UICard card = kvp.Value;
            
            int score = CalculateScore(card.cardData);
            string nombreJugador = InteractionManager.Instance.GetPlayerName(playerIndex);
            registroCartas.Add($"{nombreJugador} [{card.cardData.rank} de {card.cardData.suit} = {score} pts]");
            
            if (score > maxScore)
            {
                maxScore = score;
                winnerIndex = playerIndex;
            }
        }

        Debug.Log($"<color=cyan>--- RESULTADO BAZA --- {string.Join(", ", registroCartas)}</color>");

        if (winnerIndex != -1)
        {
            InteractionManager.Instance.bazasGanadas[winnerIndex]++;

            // Extraemos rank y suit de la carta ganadora para resaltarla en el TableTracker
            string winRank = cartasEnMesa[winnerIndex].cardData.rank;
            string winSuit = cartasEnMesa[winnerIndex].cardData.suit;
            
            AnunciarGanadorBazaClientRpc(winnerIndex, InteractionManager.Instance.bazasGanadas[winnerIndex], winRank, winSuit);
        }
        
        // Registrar las cartas en la memoria de la IA (para niveles Difficult+)
        if (AIController.Instance != null)
        {
            var cardDatas = cartasEnMesa.Values
                .Where(c => c != null)
                .Select(c => c.cardData)
                .ToList();
            AIController.Instance.RegisterPlayedCards(cardDatas);
        }

        UpdateUIClientRpc(); 

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

    [Rpc(SendTo.Everyone)]
    private void AnunciarGanadorBazaClientRpc(int winnerIndex, int totalBazasDelGanador, string winRank, string winSuit)
    {
        // Actualizamos el array local del Cliente con el dato real del Servidor
        InteractionManager.Instance.bazasGanadas[winnerIndex] = totalBazasDelGanador;

        int localId = InteractionManager.Instance.MySeatIndex;
        string nombreReal = InteractionManager.Instance.GetPlayerName(winnerIndex);
        if (winnerIndex == localId) 
            InteractionManager.Instance.SetInfoMessage("<color=#55FF55><b>¡GANASTE LA BAZA!</b></color>", 4f);
        else 
            InteractionManager.Instance.SetInfoMessage($"<color=#FF5555><b>{nombreReal}</b></color> se lleva la baza.", 4f);

        // Resaltar en verde la carta ganadora en el TableTracker
        TableTrackerUI.Instance?.HighlightWinner(winRank, winSuit);
    }

    IEnumerator WaitAndResolveRound()
    {
        yield return new WaitForSeconds(1.5f);
        ResolverApuestas(); 
    }

    private void ResolverApuestas()
    {
        if (!IsServer) return;

        string mensajeResultado = "<b>RESUMEN DE RONDA</b>\n";
        InteractionManager.Instance.rondasJugadasTotales++;
        int jugadoresVivos = 0;
        int totalPlayers = InteractionManager.Instance.totalPlayers;

        // Snapshot de vidas antes de decrementar
        int[] vidasAntes = new int[totalPlayers];
        for (int i = 0; i < totalPlayers; i++)
            vidasAntes[i] = InteractionManager.Instance.vidas[i];

        for (int i = 0; i < totalPlayers; i++)
        {
            if (InteractionManager.Instance.vidas[i] <= 0) continue;

            int bazas = InteractionManager.Instance.bazasGanadas[i];
            int apuesta = InteractionManager.Instance.apuestas[i];
            string nombre = InteractionManager.Instance.GetPlayerName(i);

            InteractionManager.Instance.bazasTotales[i] += bazas;

            if (bazas == apuesta)
            {
                InteractionManager.Instance.apuestasAcertadasTotales[i]++;
            }
            else
            {
                InteractionManager.Instance.vidas[i]--; 
                string nombreReal = InteractionManager.Instance.GetPlayerName(i);
                if (i == InteractionManager.Instance.MySeatIndex)
                {
                    AudioManager.Instance?.PlaySFX(AudioManager.Instance?.loseLife);
                    mensajeResultado += $"<color=#FF5555>Has perdido 1 vida 💔</color>\n";
                }
                else
                {
                    mensajeResultado += $"<color=#FF5555>{nombreReal} pierde 1 vida 💔</color>\n";
                }
            }

            if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
        }

        if (mensajeResultado == "<b>RESUMEN DE RONDA</b>\n")
        {
            mensajeResultado += "<color=#55FF55>¡Todos los jugadores salvan sus vidas!</color>";
        }

        // Enviamos la resolución al resto de jugadores
        SincronizarResolucionRondaClientRpc(mensajeResultado, vidasAntes, InteractionManager.Instance.vidas, InteractionManager.Instance.bazasTotales, InteractionManager.Instance.apuestasAcertadasTotales);

        if (jugadoresVivos > 1)
        {
            StartCoroutine(ResetRoundAfterDelay());
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarResolucionRondaClientRpc(string mensaje, int[] vidasAntes, int[] vidasServidor, int[] bazasTot, int[] apuestasAcertadasTot)
    {
        InteractionManager.Instance.SetInfoMessage(mensaje, 7f);
        
        int localId = InteractionManager.Instance.MySeatIndex;

        // Usamos vidasAntes (snapshot pre-decremento) para saber si estábamos vivos antes de esta ronda
        bool estabaVivo = vidasAntes[localId] > 0;

        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
        {
            InteractionManager.Instance.vidas[i] = vidasServidor[i];
            InteractionManager.Instance.bazasTotales[i] = bazasTot[i];
            InteractionManager.Instance.apuestasAcertadasTotales[i] = apuestasAcertadasTot[i];
        }
        
        UpdateUI();

        // Comprobamos nuestra vida DESPUÉS de actualizar
        bool sigoVivo = InteractionManager.Instance.vidas[localId] > 0;

        int jugadoresVivos = 0;
        foreach (int v in InteractionManager.Instance.vidas) if (v > 0) jugadoresVivos++;

        // Actualizar el castigo dinámico de ragequit según los que queden vivos
        if (PauseManager.Instance != null)
            PauseManager.Instance.UpdateAntiRageQuitPenalty();

        // --- CADA CLIENTE JUZGA SU PROPIO GAME OVER ---
        if (estabaVivo && !sigoVivo)
        {
            // Acabo de morir esta ronda
            int miPuesto = jugadoresVivos + 1;
            StartCoroutine(DelayedGameOver(miPuesto, 2f));
        }
        else if (jugadoresVivos <= 1 && sigoVivo)
        {
            // Quedo yo solo en la mesa, soy el ganador
            StartCoroutine(DelayedGameOver(1, 2f));
        }

        // Si la partida terminó y hay espectadores, mostrarles la pantalla final
        if (jugadoresVivos <= 1 && PauseManager.Instance != null && PauseManager.Instance.isSpectating)
            StartCoroutine(DelayedNotificarEspectador(2.5f));
    }

    private System.Collections.IEnumerator DelayedNotificarEspectador(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (PauseManager.Instance != null && PauseManager.Instance.isSpectating)
            PauseManager.Instance.NotificarFinPartidaDefinitivo();
    }

    private System.Collections.IEnumerator DelayedGameOver(int puesto, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Si somos espectador no disparamos GameOver de nuevo
        if (PauseManager.Instance != null && PauseManager.Instance.isSpectating) yield break;

        // TROFEOS: Si es una partida pública y el jugador acaba de morir (no es el ganador final),
        // contribuimos con los trofeos que pierde al bote, para que los supervivientes los recojan.
        if (GameConfig.currentMatchMode == "public" && puesto > 1 && !GameConfig.trophyAwarded)
        {
            int totalPlayers = InteractionManager.Instance != null ? InteractionManager.Instance.totalPlayers : GameConfig.nPlayers;
            int trofeosPerdidos = GameConfig.CalcularTrofeosPerdidos(puesto, totalPlayers);
            GameConfig.trophyBote += trofeosPerdidos;
            Debug.Log($"[TROFEOS SUMA-CERO] Jugador eliminado en puesto {puesto}/{totalPlayers}. Pierde y aporta {trofeosPerdidos} trofeos al bote. Bote total: {GameConfig.trophyBote}");
        }

        if (PauseManager.Instance != null)
            PauseManager.Instance.TriggerGameOver(puesto);
    }

    IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(3.0f); 
        LimpiarMesaParaSiguienteRondaClientRpc();
        
        InteractionManager.Instance.AdvanceRoundSequence();
        
        ReanudarPartidaClientRpc(InteractionManager.Instance.currentRoundCards);

        // Disparamos la siguiente ronda de cartas automáticamente
        if (IsServer)
        {
            StartCoroutine(InteractionManager.Instance.StartNewRoundServer());
        }
    }

    [Rpc(SendTo.Everyone)]
    private void LimpiarMesaParaSiguienteRondaClientRpc()
    {
        ClearTableNow();
        bazasJugadas = 0;
        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++) 
        {
            InteractionManager.Instance.bazasGanadas[i] = 0;
            InteractionManager.Instance.apuestas[i] = -1;
        }
        // Limpiar la memoria de cartas para la nueva ronda (IA niveles 3+)
        if (AIController.Instance != null) AIController.Instance.ResetRoundMemory();
        UpdateUI();
    }

    [Rpc(SendTo.Everyone)]
    private void ReanudarPartidaClientRpc(int curRoundCards)
    {
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetInfoMessage($"<b>¡NUEVA RONDA!</b> Repartiendo {curRoundCards} cartas...", 4f);
    }

    IEnumerator CleanTableRoutine(int nextTurn)
    {
        // El reloj de turno se ocultará/marcará PAUSA mientras esperamos
        InteractionManager.Instance.turnEndTime.Value = 0f;
        
        yield return new WaitForSeconds(3.0f);
        LimpiarMesaIntermediaClientRpc();
        
        InteractionManager.Instance.SetTurn(nextTurn);
    }

    [Rpc(SendTo.Everyone)]
    private void LimpiarMesaIntermediaClientRpc()
    {
        TableTrackerUI.Instance?.ClearHighlight();
        ClearTableNow();
        InteractionManager.Instance.isPaused = false;
    }

    // =======================================================================
    // UI Y CÁLCULOS MENORES
    // =======================================================================
    [Rpc(SendTo.Everyone)]
    private void UpdateUIClientRpc() { UpdateUI(); }

    private void UpdateUI()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ActualizarTodosLosPerfilesUI();

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