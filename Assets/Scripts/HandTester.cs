using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Necesario para usar Listas

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
        // Suscribimos los botones a los métodos
        if (drawHandButton != null) drawHandButton.onClick.AddListener(DrawNewHand);
        if (showDeckButton != null) showDeckButton.onClick.AddListener(ShowFullDeck);
    }

    // MÉTODO 1: Sacar 5 cartas (Modo Juego + Multijugador)
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

        // Calculamos cartas para Ti + la IA + los jugadores generados nuevos
        int jugadoresExtra = (TableManagerLayout.Instance != null) ? TableManagerLayout.Instance.manosActivas.Count : 0;
        int totalCartasNecesarias = cardsToDeal * (2 + jugadoresExtra); 

        if(CardDatabase.deck == null || CardDatabase.deck.Count < totalCartasNecesarias)
        {
            Debug.LogWarning("⚠️No quedan cartas suficientes en el mazo. Crea uno nuevo");
            return;
        }

        // 2. EXTRA DE SEGURIDAD: Destruimos cualquier carta basura que tuviera la IA
        Transform aiHandArea = null;
        if (InteractionManager.Instance != null && InteractionManager.Instance.handGroupP2 != null)
        {
             aiHandArea = InteractionManager.Instance.handGroupP2.transform;
             // foreach (Transform child in aiHandArea) Destroy(child.gameObject);
        }

        // 3. REPARTIMOS AL JUGADOR 1 (Tú)
        for (int i = 0; i < cardsToDeal; i++) 
        {
            Card data = CardDatabase.DrawTopCard();
            if (data == null) break;
            InstanciarCarta(data, handArea);
        }
        // ---> LLAMAMOS AL CRUPIER DEL JUGADOR 1 <---
        if (handArea != null)
        {
            HandLayoutFanner fannerP1 = handArea.GetComponent<HandLayoutFanner>();
            if (fannerP1 != null) fannerP1.ReorganizarCartas();
        }

        // 4. REPARTIMOS AL JUGADOR 2 (La IA)
        if (aiHandArea != null)
        {
            for (int i = 0; i < cardsToDeal; i++) 
            {
                Card data = CardDatabase.DrawTopCard();
                if (data == null) break;
                InstanciarCarta(data, aiHandArea);
            }
            // ---> LLAMAMOS AL CRUPIER DE LA IA <---
            HandLayoutFanner fannerIA = aiHandArea.GetComponent<HandLayoutFanner>();
            if (fannerIA != null) fannerIA.ReorganizarCartas();
        }

        // 5. REPARTIMOS A LOS JUGADORES DEL CONTENEDOR HA
        if (TableManagerLayout.Instance != null)
        {
            foreach (CanvasGroup mano in TableManagerLayout.Instance.manosActivas)
            {
                if (mano == null) continue; // Por si acaso la mano no existe
                
                for (int i = 0; i < cardsToDeal; i++) 
                {
                    Card data = CardDatabase.DrawTopCard();
                    if (data == null) break;
                    
                    // Instanciamos la carta dentro del HandArea generado
                    InstanciarCarta(data, mano.transform);
                }

                // ---> LLAMAMOS AL CRUPIER DE ESTA MANO DINÁMICA <---
                HandLayoutFanner fannerMesa = mano.GetComponentInParent<HandLayoutFanner>();
                if (fannerMesa != null) 
                {
                    fannerMesa.ReorganizarCartas();
                }
            }
        }

        // 6. Aplicamos las reglas visuales (para tapar cartas si es Ronda Ciega)
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshHandVisibility();
        }

        // 7. Iniciamos la fase de apuestas
        if (BettingManager.Instance != null)
        {
            BettingManager.Instance.StartBettingPhase(cardsToDeal);
        }
    }
   
    public void ShowFullDeck()
    {
        // 1. LÓGICA DE TOGGLE: Si el panel ya está encendido, lo apagamos y salimos.
        if (fullHandArea.gameObject.activeSelf)
        {
            fullHandArea.gameObject.SetActive(false); 
            foreach (Transform child in fullHandArea) Destroy(child.gameObject); 
            return; 
        }

        fullHandArea.gameObject.SetActive(true);
        
        foreach (Transform child in fullHandArea) Destroy(child.gameObject);

        // 3. Comprobación de seguridad
        if (CardDatabase.deck == null || CardDatabase.deck.Count == 0)
        {
            Debug.LogWarning("El mazo está vacío. No hay cartas que mostrar.");
            return; 
        }

        // 4. Instanciamos SOLO las cartas restantes
        List<Card> cartasRestantes = new List<Card>(CardDatabase.deck);
        
        foreach (Card card in cartasRestantes)
        {
            InstanciarCarta(card, fullHandArea);
        }
    }

    // --- MÉTODOS DE APOYO PARA NO REPETIR ---

    private void PrepararVista(bool esGrid)
    {
        // Activamos un área y desactivamos la otra
        handArea.gameObject.SetActive(!esGrid);
        fullHandArea.gameObject.SetActive(esGrid);

        // Limpiamos el área que vamos a usar
        Transform areaActiva = esGrid ? fullHandArea : handArea;
        foreach (Transform child in areaActiva)
        {
            if(child.gameObject.name.Contains("CardPrefab")){Destroy(child.gameObject);}
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