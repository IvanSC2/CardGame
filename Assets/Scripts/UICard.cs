using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class UICard : MonoBehaviour , IPointerClickHandler
{
    public Card cardData;
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

    [Header("Configuración Reverso")]
    public Sprite cardBackSprite; 
    private Sprite cardFrontSprite;
    private Image myBackground;
    private bool isFaceUp = true;
    
    private void Awake()
    {
        myBackground = GetComponent<Image>();
        // Guardamos el sprite blanco original como "Frente"
        if (myBackground != null) cardFrontSprite = myBackground.sprite;
    }
    public void SetCard(Card card)
    {
        //Guardo la carta
        this.cardData = card;
        // --- Rank ---
        rankTop.text = card.rank;
        rankBottom.text = card.rank;

        // --- Suit Icons ---
        Sprite suitSprite = GetSuitSprite(card.suit);
        suitTop.sprite = suitSprite;
        suitBottom.sprite = suitSprite;
        
        // --- Color rojo/negro ---
        ApplySuitColor(card.suit);
        Debug.Log("Sprite loaded: " + (suitSprite != null));

        if (card.value >= 11 || card.value == 1)
        {
            // A. REUTILIZACIÓN:
            // Le decimos al generador de pips que dibuje un "1" (el centro),
            // independientemente de que la carta sea un 11, 12 o 13.
            
            pipsContainer.SetActive(true);
            GenerateDynamicPips(1, suitSprite); 
            // B. Encendemos el Overlay (La máscara de la figura)
            artworkFace.SetActive(true);

            // C. Cargamos la foto correcta
            string letter = "";
            if (card.value == 11) letter = "J";
            else if (card.value == 12) letter = "Q";
            else if (card.value == 13) letter = "K";
            else if (card.value == 1) letter = "A";

            // Asigno la imagen al componente Image del ArtworkFace
            artworkFace.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Artworks/{letter}");
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
    //Control de Cara / Cruz de las cartas
    public void SetFaceUp(bool isUp)
    {
        isFaceUp = isUp;
        UpdateVisibility();
    }
    //Cambia imagen entre reverso y fenre y controla el contenido que aparece
    private void UpdateVisibility()
    {
        if (myBackground == null) return;

        // 1. Cambiar Fondo
        myBackground.sprite = isFaceUp ? cardFrontSprite : cardBackSprite;

        // 2. Ocultar/Mostrar contenido
        rankTop.gameObject.SetActive(isFaceUp);
        rankBottom.gameObject.SetActive(isFaceUp);
        suitTop.gameObject.SetActive(isFaceUp);
        suitBottom.gameObject.SetActive(isFaceUp);
        
        if (pipsContainer != null) 
            pipsContainer.SetActive(isFaceUp);
        
        if (artworkFace != null) 
            artworkFace.SetActive(isFaceUp && (cardData.value >= 11 || cardData.value == 1));
    }
    private void ApplySuitColor(string suit)
    {
        bool isRed = suit == "Corazones" || suit == "Diamantes";

        Color red = new Color(0.92f, 0.2f, 0.2f);
        Color black = Color.black;

        Color chosen = isRed ? red : black;

        rankTop.color = chosen;
        rankBottom.color = chosen;

        suitTop.color = chosen;
        suitBottom.color = chosen;

       //colorar pips del centro
        foreach (Image pip in allPips)
        {
            pip.color = chosen;
        }
    }

    private void GenerateDynamicPips(int number, Sprite suitIcon)
    {
        // 1. Definimos las coordenadas a encender 
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

            // C. Declaramos y limpiamos el nombre
            string nameInUnity = pip.name.ToLower().Trim();

            // D. Transparencia
            // Si está en la lista -> enabled = true (Se ve)
            // Si NO está en la lista -> enabled = false (Invisible, pero ocupa espacio)
            pip.enabled = activePips.Contains(nameInUnity);
        }
    }
    private Sprite GetSuitSprite(string suit)
    {
        // /Assets/Resources/Suits/Picas.png
        return Resources.Load<Sprite>($"Suits/{suit}");
    }

    // Este método se dispara automáticamente cuando haces clic en el objeto UI
    public void OnPointerClick(PointerEventData eventData)
    {
        // Llamamos al manager
        InteractionManager.Instance.SelectCard(this);
        
        // Aqui se puede oscurecer la carta al tocarla
       // GetComponent<Image>().color = Color.yellow; 
    }
}
