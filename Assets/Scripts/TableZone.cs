using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic; 
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

            // Si soy yo mismo el que juega (o soy el Host jugando por un Bot), solicito la jugada
            int localId = InteractionManager.Instance.MySeatIndex;
            if (duenoIndex == localId || (IsServer && !InteractionManager.Instance.IsPlayerConnectedAndHuman(duenoIndex)))
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
            string nombreJugador = (playerIndex == 0) ? "TÚ" : $"BOT {playerIndex}";
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
            
            AnunciarGanadorBazaClientRpc(winnerIndex, InteractionManager.Instance.bazasGanadas[winnerIndex]);
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
    private void AnunciarGanadorBazaClientRpc(int winnerIndex, int totalBazasDelGanador)
    {
        // Actualizamos el array local del Cliente con el dato real del Servidor
        InteractionManager.Instance.bazasGanadas[winnerIndex] = totalBazasDelGanador;

        int localId = InteractionManager.Instance.MySeatIndex;
        if (winnerIndex == localId) InteractionManager.Instance.SetInfoMessage("¡TÚ GANAS LA BAZA!\n");
        else InteractionManager.Instance.SetInfoMessage($"¡EL JUGADOR {winnerIndex} GANA LA BAZA!\n");
    }

    IEnumerator WaitAndResolveRound()
    {
        yield return new WaitForSeconds(1.5f);
        ResolverApuestas(); 
    }

    private void ResolverApuestas()
    {
        if (!IsServer) return;

        string mensajeResultado = "RESULTADOS:\n";
        InteractionManager.Instance.rondasJugadasTotales++;
        int jugadoresVivos = 0;
        int totalPlayers = InteractionManager.Instance.totalPlayers;

        for (int i = 0; i < totalPlayers; i++)
        {
            if (InteractionManager.Instance.vidas[i] <= 0) continue;

            int bazas = InteractionManager.Instance.bazasGanadas[i];
            int apuesta = InteractionManager.Instance.apuestas[i];
            string nombre = (i == 0) ? "TÚ" : $"BOT {i}"; // Esto lo mantengo para los logs del server

            InteractionManager.Instance.bazasTotales[i] += bazas;

            if (bazas == apuesta)
            {
                mensajeResultado += $"{nombre}: CUMPLE.\n";
                InteractionManager.Instance.apuestasAcertadasTotales[i]++;
            }
            else
            {
                InteractionManager.Instance.vidas[i]--; 
                mensajeResultado += $"{nombre}: FALLA (-1 Vida).\n";
            }

            if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
        }

        // Enviamos la resolución al resto de jugadores (para que actualicen vidas/texto)
        SincronizarResolucionRondaClientRpc(mensajeResultado, InteractionManager.Instance.vidas, InteractionManager.Instance.bazasTotales, InteractionManager.Instance.apuestasAcertadasTotales);

        if (jugadoresVivos > 1)
        {
            StartCoroutine(ResetRoundAfterDelay());
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarResolucionRondaClientRpc(string mensaje, int[] vidasServidor, int[] bazasTot, int[] apuestasAcertadasTot)
    {
        InteractionManager.Instance.SetInfoMessage(mensaje);
        
        int localId = InteractionManager.Instance.MySeatIndex;
        // Comprobamos nuestra vida ANTES de actualizar
        bool estabaVivo = InteractionManager.Instance.vidas[localId] > 0;

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
    }

    private System.Collections.IEnumerator DelayedGameOver(int puesto, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (PauseManager.Instance != null)
            PauseManager.Instance.TriggerGameOver(puesto);
    }

    IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(3.0f); 
        LimpiarMesaParaSiguienteRondaClientRpc();
        
        InteractionManager.Instance.AdvanceRoundSequence();
        
        ReanudarPartidaClientRpc(InteractionManager.Instance.currentRoundCards);
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
        UpdateUI();
    }

    [Rpc(SendTo.Everyone)]
    private void ReanudarPartidaClientRpc(int curRoundCards)
    {
        InteractionManager.Instance.isPaused = false;
        InteractionManager.Instance.SetInfoMessage($"Ronda terminada.\nSiguiente: {curRoundCards} cartas.");
    }

    IEnumerator CleanTableRoutine(int nextTurn)
    {
        yield return new WaitForSeconds(1.5f);
        LimpiarMesaIntermediaClientRpc();
        
        InteractionManager.Instance.SetTurn(nextTurn);
    }

    [Rpc(SendTo.Everyone)]
    private void LimpiarMesaIntermediaClientRpc()
    {
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