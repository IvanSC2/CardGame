using UnityEngine;
using UnityEngine.UI;
public class DeckManager : MonoBehaviour
{

    
    public Button bNewDeck;
    public Button bShuffleDeck; // Nuevo botón para la baraja
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Suscribimos los botones a los métodos
        if (bNewDeck != null) bNewDeck.onClick.AddListener(newDeck);
        if (bShuffleDeck != null) bShuffleDeck.onClick.AddListener(shuffleDeck);
    }


    public void newDeck()
    {
        CardDatabase.GenerateOrderDeck();
        if (TableZone.Instance != null)
        {
            TableZone.Instance.ClearTableNow();
        }
    }

    public void shuffleDeck()
    {
        CardDatabase.Shuffle();

    }
   
}
