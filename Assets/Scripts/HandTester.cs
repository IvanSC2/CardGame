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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Suscribimos los botones a los métodos
        if (drawHandButton != null) drawHandButton.onClick.AddListener(DrawNewHand);
        if (showDeckButton != null) showDeckButton.onClick.AddListener(ShowFullDeck);
        
    }
// MÉTODO 1: Sacar 5 cartas (Modo Juego)
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
        int totalCartasNecesarias= cardsToDeal*2;
        if(CardDatabase.deck==null || CardDatabase.deck.Count < totalCartasNecesarias)
        {
            Debug.LogWarning("⚠️No quedan cartas suficientes en el mazo. Crea uno nuevo");
            return;
        }

        // 2. EXTRA DE SEGURIDAD: Destruimos cualquier carta basura que tuviera la IA
        Transform aiHandArea = InteractionManager.Instance.handGroupP2.transform;
        if (aiHandArea != null)
        {
            foreach (Transform child in aiHandArea) Destroy(child.gameObject);
        }

        // 3. REPARTIMOS AL JUGADOR 1 (Tú)
        for (int i = 0; i < cardsToDeal; i++) 
        {
            Card data = CardDatabase.DrawTopCard();
            if (data == null) break;
            InstanciarCarta(data, handArea);
        }

        // 4. REPARTIMOS AL JUGADOR 2 (La IA)
        for (int i = 0; i < cardsToDeal; i++) 
        {
            Card data = CardDatabase.DrawTopCard();
            if (data == null) break;
            
            // Instanciamos en el panel de la IA
            InstanciarCarta(data, aiHandArea);
        }

        // 5. Aplicamos las reglas visuales (para tapar cartas si es Ronda Ciega)
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshHandVisibility();
        }

        // 6. Iniciamos la fase de apuestas
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
            Destroy(child.gameObject);
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
