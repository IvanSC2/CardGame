using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;

public class HandTester : NetworkBehaviour
{
    [Header("Configuración")]
    public GameObject cardPrefab;
    public Button drawHandButton;
    public Button showDeckButton; 

    [Header("Contenedores")]
    public Transform handArea;    
    public Transform fullHandArea; 

    void Start()
    {
        if (drawHandButton != null) drawHandButton.onClick.AddListener(DrawNewHand);
        if (showDeckButton != null) showDeckButton.onClick.AddListener(ShowFullDeck);
    }

  public void DrawNewHand()
    {
        // --- 1. SEGURIDAD RED: Solo el Host decide el reparto ---
        if (!IsServer) return;

        // --- 2. Limpiar mesas en las pantallas de todos ---
        LimpiarMesaClientRpc();

        // Extraemos cuántas cartas tocan en esta ronda
        int cardsToDeal = 5; 
        if (InteractionManager.Instance != null) 
        {
            cardsToDeal = InteractionManager.Instance.currentRoundCards;
        }

        // Calculamos cartas necesarias SOLO para los VIVOS
        int totalJugadores = 2; 
        if (TableManagerLayout.Instance != null && TableManagerLayout.Instance.manosActivas.Count > 0)
        {
            totalJugadores = TableManagerLayout.Instance.manosActivas.Count;
        }

        int jugadoresVivos = 0;
        if (InteractionManager.Instance != null)
        {
            for (int i = 0; i < totalJugadores; i++)
            {
                if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
            }
        }
        else
        {
            jugadoresVivos = totalJugadores; // Fallback por si no hay manager
        }

        int totalCartasNecesarias = cardsToDeal * jugadoresVivos; 

        if(CardDatabase.deck == null || CardDatabase.deck.Count < totalCartasNecesarias)
        {
            Debug.LogWarning($"⚠️ No quedan cartas. Necesitas {totalCartasNecesarias} pero hay {CardDatabase.deck?.Count}");
            return;
        }

        // --- 3. REPARTIMOS SOLO A LOS VIVOS ---
        if (TableManagerLayout.Instance != null)
        {
            for (int j = 0; j < TableManagerLayout.Instance.manosActivas.Count; j++)
            {
                // Filtro para saltar a los muertos
                if (InteractionManager.Instance != null && InteractionManager.Instance.vidas[j] <= 0)
                {
                    continue; // Está eliminado, nos lo saltamos y no le damos cartas.
                }
                
                for (int i = 0; i < cardsToDeal; i++) 
                {
                    // El Host roba la carta real de la base de datos
                    Card data = CardDatabase.DrawTopCard();
                    if (data == null) break;
                    
                    // En lugar de instanciarla solo en su pantalla, le grita a todos por la red:
                    RepartirCartaClientRpc(data.id, data.suit, data.rank, data.value, j);
                }
            }
        }

        // --- 4. El Host avisa de que el reparto ha terminado ---
        TerminarRepartoClientRpc(cardsToDeal);
    }

    // =========================================================================
    // MÉTODOS RPC (Órdenes que el Host envía y se ejecutan en TODOS los clientes)
    // =========================================================================

    [ClientRpc]
    private void LimpiarMesaClientRpc()
    {
        PrepararVista(esGrid: false);
        if (TableZone.Instance != null)
        {
            TableZone.Instance.ClearTableNow();
        }
    }

    [ClientRpc]
    private void RepartirCartaClientRpc(int id, string suit, string rank, int value, int manoIndex)
    {
        // 1. Reconstruimos la carta en la memoria del cliente con los datos recibidos
        Card cartaSincronizada = new Card(id, suit, rank, value);
        
        // 2. Buscamos la mano correcta y la instanciamos físicamente
        if (TableManagerLayout.Instance != null && TableManagerLayout.Instance.manosActivas.Count > manoIndex)
        {
            CanvasGroup mano = TableManagerLayout.Instance.manosActivas[manoIndex];
            InstanciarCarta(cartaSincronizada, mano.transform);

            HandLayoutFanner fannerMesa = mano.GetComponent<HandLayoutFanner>();
            if (fannerMesa != null) 
            {
                fannerMesa.ReorganizarCartas();
            }
        }
    }

    [ClientRpc]
    private void TerminarRepartoClientRpc(int cardsToDeal)
    {
        // Aplicamos las reglas visuales (caras arriba/abajo) en todos los PCs
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshHandVisibility();
        }

        // Iniciamos la fase de apuestas para todos
        if (BettingManager.Instance != null)
        {
            BettingManager.Instance.StartBettingPhase(cardsToDeal);
        }
    }
   
    public void ShowFullDeck()
    {
        if (fullHandArea.gameObject.activeSelf)
        {
            fullHandArea.gameObject.SetActive(false); 
            foreach (Transform child in fullHandArea) Destroy(child.gameObject); 
            return; 
        }

        fullHandArea.gameObject.SetActive(true);
        foreach (Transform child in fullHandArea) Destroy(child.gameObject);

        if (CardDatabase.deck == null || CardDatabase.deck.Count == 0)
        {
            Debug.LogWarning("El mazo está vacío. No hay cartas que mostrar.");
            return; 
        }

        List<Card> cartasRestantes = new List<Card>(CardDatabase.deck);
        foreach (Card card in cartasRestantes)
        {
            InstanciarCarta(card, fullHandArea);
        }
    }

    private void PrepararVista(bool esGrid)
    {
        fullHandArea.gameObject.SetActive(esGrid);

        if (esGrid)
        {
            foreach (Transform child in fullHandArea)
            {
                if(child.gameObject.name.Contains("CardPrefab")) Destroy(child.gameObject);
            }
        }
        else
        {
            if (TableManagerLayout.Instance != null)
            {
                foreach (CanvasGroup mano in TableManagerLayout.Instance.manosActivas)
                {
                    if (mano == null) continue;
                    foreach (Transform child in mano.transform)
                    {
                        if(child.GetComponent<UICard>() != null) Destroy(child.gameObject);
                    }
                }
            }
        }
    }

    private void InstanciarCarta(Card data, Transform padre)
    {
        if (data == null) return;

        GameObject newCardObj = Instantiate(cardPrefab, padre);
        UICard uiLogic = newCardObj.GetComponent<UICard>();
        if (uiLogic != null)
        {
            uiLogic.SetCard(data);
        }
    }
}