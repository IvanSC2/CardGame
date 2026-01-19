using UnityEngine;
using System.Collections.Generic;

public class CardDatabase : MonoBehaviour
{
    public static List<Card> deck = new List<Card>();
    private static string[] suits = { "Corazones", "Diamantes", "Tréboles", "Picas" };
    private static string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    void Awake()
    {
        GenerateDeck();
    }
    //Genera una Deck y la baraja
    public static void GenerateDeck()
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
        //Cada vez que se crea una deck se baraja
        Shuffle(deck);
    }


    //devuelve una carta alatoria del mazo
    public static Card GetRandomCard()
    {
        // Si por lo que sea el mazo está vacío, lo regeneramos
        if (deck == null || deck.Count == 0)
        {
            GenerateDeck();
        }

        int index = Random.Range(0, deck.Count);
        return deck[index];
    }
    //Baraja la deck y devuelve una lista de cartas (new Deck)
    public static void Shuffle(List<Card> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Card temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
