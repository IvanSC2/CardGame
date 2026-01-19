using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UICard : MonoBehaviour
{
    [Header("Rank Texts")]
    public TMP_Text rankTop;
    public TMP_Text rankBottom;

    [Header("Suit Icons")]
    public Image suitTop;
    public Image suitBottom;

    [Header("Pip Matrix")]
    public GameObject pipsContainer;
    public Image[] allPips;

    [Header("Artwork")]
    public GameObject artworkFace;

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

        if (card.value >= 11 || card.value == 1)
        {
            // A. TRUCO DE REUTILIZACIÓN:
            // Le decimos al generador de pips que dibuje un "1" (el centro),
            // independientemente de que la carta sea un 11, 12 o 13.
            GenerateDynamicPips(1, suitSprite); 

            // B. Encendemos el Overlay (La máscara de la figura)
            artworkFace.SetActive(true);

            // C. Cargamos la foto correcta (Solo si no es As, que ya es bonito solo)
            // Si quieres que el As también tenga marco, inclúyelo aquí.
            string letter = "";
            if (card.value == 11) letter = "J";
            else if (card.value == 12) letter = "Q";
            else if (card.value == 13) letter = "K";
            else if (card.value == 1) letter = "A";

            // Asignamos la imagen al componente Image del ArtworkFace
            // Asumo que ArtworkFace tiene un componente Image
            artworkFace.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Artworks/{letter}");
            
            // Aseguramos que el overlay sea blanco (o el color que quieras)
            artworkFace.GetComponent<Image>().color = Color.white;
        }
        else
        {
            //Activo en contenedor y genero el patron del artwork
            artworkFace.SetActive(false); // Apagamos la máscara
            pipsContainer.SetActive(true);
            GenerateDynamicPips(card.value, suitSprite);
        }

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

        // También coloreamos los pips del centro
        foreach (Image pip in allPips)
        {
            pip.color = chosen;
        }
    }

    private void GenerateDynamicPips(int number, Sprite suitIcon)
    {
        // 1. Definimos las coordenadas a encender (todo en minúsculas)
        System.Collections.Generic.List<string> activePips = new System.Collections.Generic.List<string>();

        switch (number)
        {
            case 1: activePips.AddRange(new[] { "b3" }); break;
            case 2: activePips.AddRange(new[] { "b1", "b5" }); break;
            case 3: activePips.AddRange(new[] { "b1", "b3", "b5" }); break;
            case 4: activePips.AddRange(new[] { "a1", "c1", "a5", "c5" }); break;
            case 5: activePips.AddRange(new[] { "a1", "c1", "b3", "a5", "c5" }); break;
            case 6: activePips.AddRange(new[] { "a1", "c1", "a3", "c3", "a5", "c5" }); break;
            case 7: activePips.AddRange(new[] { "a1", "c1", "a3", "c3", "a5", "c5", "b2" }); break;
            case 8: activePips.AddRange(new[] { "a1", "c1", "a3", "c3", "a5", "c5", "b2", "b4" }); break;
            case 9: activePips.AddRange(new[] { "a1", "c1", "a2", "c2", "a4", "c4", "a5", "c5", "b3" }); break;
            case 10: activePips.AddRange(new[] { "a1", "c1", "a2", "c2", "b2", "b4", "a4", "c4", "a5", "c5" }); break;
        }

        // 2. Bucle ÚNICO para aplicar estado
        foreach (Image pip in allPips)
        {
            // A. Aseguramos que el objeto FÍSICO está encendido para que el Grid reserve el hueco
            pip.gameObject.SetActive(true);

            // B. Ponemos el icono correcto
            pip.sprite = suitIcon;

            // C. Declaramos y limpiamos el nombre (ESTO ES LO QUE TE FALTABA)
            string nameInUnity = pip.name.ToLower().Trim();

            // D. El "Truco de la Transparencia":
            // Si está en la lista -> enabled = true (Se ve)
            // Si NO está en la lista -> enabled = false (Invisible, pero ocupa espacio)
            pip.enabled = activePips.Contains(nameInUnity);
        }
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
