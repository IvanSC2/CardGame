using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICard : MonoBehaviour
{
    [Header("Rank Texts")]
    public TMP_Text rankTop;
    public TMP_Text rankBottom;

    [Header("Suit Icons")]
    public Image suitTop;
    public Image suitBottom;

    [Header("Artwork")]
    public Image artwork;

    public void SetCard(Card card)
    {
        // --- Rank ---
        rankTop.text = card.rank;
        rankBottom.text = card.rank;

        // --- Suit Icons ---
        Sprite suitSprite = GetSuitSprite(card.suit);
        suitTop.sprite = suitSprite;
        suitBottom.sprite = suitSprite;
        /*
        // --- Artwork central ---
        if (card.artwork != null)
            artwork.sprite = card.artwork;
        else
            artwork.sprite = null;
*/
        // --- Color rojo/negro ---
        ApplySuitColor(card.suit);
        Debug.Log("Sprite loaded: " + (suitSprite != null));

    }

    private void ApplySuitColor(string suit)
{
    bool isRed = suit == "Corazones" || suit == "Diamantes";

    Color red = new Color(0.92f, 0.2f, 0.2f);
    Color black = Color.black;

    Color chosen = isRed ? red : black;

    rankTop.color = chosen;
    rankBottom.color = chosen;

    // 🔽 Añade esto para colorear los iconos de palo
    suitTop.color = chosen;
    suitBottom.color = chosen;
}

    private Sprite GetSuitSprite(string suit)
    {
        // IMPORTANTE → mete tus sprites aquí:
        // /Assets/Resources/Suits/Corazones.png
        // /Assets/Resources/Suits/Diamantes.png
        // /Assets/Resources/Suits/Tréboles.png
        // /Assets/Resources/Suits/Picas.png
        return Resources.Load<Sprite>($"Suits/{suit}");
    }
}
