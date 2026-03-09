using UnityEngine;
using System.Collections.Generic;
//Clase estatica con la lista virtual de cartas
public class CardDatabase : MonoBehaviour
{
    //Baraja Unica Global
    public static List<Card> deck = new List<Card>();
    private static string[] suits = { "Corazones", "Diamantes", "Tréboles", "Picas" };
    private static string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    void Awake()
    {
        //GenerateOrderDeck();
    }
    //Genera una Deck y la baraja
    public static void GenerateDeck()
    {
        GetDeck(true);
    }

    public static void GenerateOrderDeck()
    {
        GetDeck(false);
    }
    //Genera una baraja recorriendo los ranks y suits
    public static void GetDeck(bool shuffle)
    {
        deck.Clear();
        int id = 0;

        foreach (string suit in suits)
        {
            for (int i = 0; i < ranks.Length; i++)
            {
                int value = i + 1; // A = 1, J=11, Q=12, K=13
                Card newCard = new Card(id, suit, ranks[i], value);
                deck.Add(newCard);
                id++;
            }
        }

        if(shuffle== true)
        {
            Shuffle();    
        }
        
        
    }

    

    //devuelve una carta alatoria del mazo
    public static Card GetRandomCard()
    {
        if (deck == null || deck.Count == 0)
        {
            Debug.LogWarning("¡Crea una Nueva baraja");
        }

        int index = Random.Range(0, deck.Count);
        return deck[index];
    }
    //Intercambia las posiciones de las cartas en la baraja de forma aletoria
    public static void Shuffle()
    {
    for (int i = 0; i < deck.Count; i++)
    {
        Card temp = deck[i];
        int randomIndex = Random.Range(i, deck.Count);
        deck[i] = deck[randomIndex];
        deck[randomIndex] = temp;
    }
    
    Debug.Log("La baraja ha sido mezclada.");
    }

    // Método para ROBAR una carta (Sacarla del mazo)
    public static Card DrawTopCard()
{
    //Si no hay cartas, devolvemos NULL inmediatamente
    if (deck == null || deck.Count <= 0)
    {
        Debug.LogWarning("¡El mazo se ha terminado! Saca Otra Baraja Nueva");
        return null; 
    }

    // Si hay cartas revolvemos la primera del mazo y eliminamos el indice
    Card cardToReturn = deck[0];
    deck.RemoveAt(0);
    return cardToReturn;
}
}
