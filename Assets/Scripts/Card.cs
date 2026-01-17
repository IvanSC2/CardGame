using UnityEngine;

[System.Serializable]
public class Card
{
    public int id;
    public string cardName;
    public string suit;       // Corazones, Diamantes, Tréboles, Picas, tu puta madre
    public string rank;       // As, 2, 3, ..., Rey
    public int value;         // Valor numérico útil para la lógica del juego
    public Sprite artwork;    // Imagen de la carta
    public string description;

    public Card(int id, string suit, string rank, int value, Sprite artwork = null, string description = "")
    {
        this.id = id;
        this.suit = suit;
        this.rank = rank;
        this.value = value;
        this.cardName = $"{rank} de {suit}";
        this.artwork = artwork;
        this.description = description;
    }
}
