using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Necesario para usar Listas

public class HandTester : MonoBehaviour
{

    [Header("Configuración")]
    public GameObject cardPrefab;
    public Button drawHandButton;
    public Button showDeckButton; // Nuevo botón para la baraja


    [Header("Contenedores")]
    public Transform handArea;     // Tu HandArea de 5 cartas (Horizontal)
    public Transform fullHandArea; // Tu nuevo FullHandArea (Grid)

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
    
    for (int i = 0; i < 5; i++)
    {
        Card data = CardDatabase.DrawTopCard(); 

        // Si data es null, significa que el mazo se ha agotado
        if (data == null) 
        {
            Debug.Log("No se pueden sacar más cartas, mazo vacío.");
            break; // Rompemos el bucle for para que no intente sacar las siguientes
        }

        InstanciarCarta(data, handArea);
    }
}

    // MÉTODO 2: Mostrar las 52 cartas (Modo Colección)
    public void ShowFullDeck()
    {
        PrepararVista(esGrid: true);

        // Como tu deck es static, accedemos directamente a la lista
        // Hacemos una copia para no vaciar el mazo real al mostrarlo
       // GenerateOrderDeck();
        List<Card> todasLasCartas = new List<Card>(CardDatabase.deck);
        
        foreach (Card card in todasLasCartas)
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
