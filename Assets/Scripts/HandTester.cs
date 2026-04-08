using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HandTester : MonoBehaviour
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
        PrepararVista(esGrid: false);

        if (TableZone.Instance != null)
        {
            TableZone.Instance.ClearTableNow();
        }
        
        // 1. Extraemos cuántas cartas tocan en esta ronda
        int cardsToDeal = 5; 
        if (InteractionManager.Instance != null) 
        {
            cardsToDeal = InteractionManager.Instance.currentRoundCards;
        }

        // 2. Calculamos cartas necesarias SOLO para los VIVOS
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

        // 3. REPARTIMOS SOLO A LOS VIVOS
        if (TableManagerLayout.Instance != null)
        {
            for (int j = 0; j < TableManagerLayout.Instance.manosActivas.Count; j++)
            {
                CanvasGroup mano = TableManagerLayout.Instance.manosActivas[j];
                if (mano == null) continue;
                
                // Filtro para saltar a los muertos
                if (InteractionManager.Instance != null && InteractionManager.Instance.vidas[j] <= 0)
                {
                    continue; // Está eliminado, nos lo saltamos y no le damos cartas.
                }
                
                for (int i = 0; i < cardsToDeal; i++) 
                {
                    Card data = CardDatabase.DrawTopCard();
                    if (data == null) break;
                    
                    InstanciarCarta(data, mano.transform);
                }

                HandLayoutFanner fannerMesa = mano.GetComponent<HandLayoutFanner>();
                if (fannerMesa != null) 
                {
                    fannerMesa.ReorganizarCartas();
                }
            }
        }

        // 4. Aplicamos las reglas visuales
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshHandVisibility();
        }

        // 5. Iniciamos la fase de apuestas
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