using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Muestra en tiempo real las cartas que hay actualmente en el CoTable (zona de juego).
/// Actualiza su lista cada frame leyendo los hijos del TableZone.
/// Coloca hasta 6 entradas mostrando emoji de palo + valor con color rojo/oscuro.
/// El fondo de cada slot es blanco (como una carta), solo cambia el color del texto.
/// La carta ganadora de la baza se resalta brevemente en verde.
/// </summary>
public class TableTrackerUI : MonoBehaviour
{
    public static TableTrackerUI Instance;

    [Header("Referencias UI")]
    [Tooltip("Arrastra aquí los 6 GameObjects de las casillas (slots). Orden: 0 al 5.")]
    public List<GameObject> slots = new List<GameObject>(); // hasta 6 slots

    [Header("Colores de Texto")]
    public Color colorRojo   = new Color(0.85f, 0.10f, 0.10f, 1f); // Corazones / Diamantes
    public Color colorOscuro = new Color(0.10f, 0.10f, 0.15f, 1f); // Picas / Tréboles

    [Header("Color de Ganador")]
    public Color colorGanador = new Color(0.70f, 1.00f, 0.70f, 1f); // Verde suave

    // Caché para no actualizar si no ha cambiado nada
    private int _lastCardCount = -1;

    // Carta ganadora activa (vacío = sin highlight)
    private string _highlightRank = null;
    private string _highlightSuit = null;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (TableZone.Instance == null) return;

        // Recogemos todas las UICards que hay en el CoTable ahora mismo
        List<UICard> cardsOnTable = new List<UICard>();
        foreach (Transform t in TableZone.Instance.transform)
        {
            UICard c = t.GetComponent<UICard>();
            if (c != null) cardsOnTable.Add(c);
        }

        // Sólo actualizamos la UI si el número de cartas ha cambiado
        if (cardsOnTable.Count == _lastCardCount) return;
        _lastCardCount = cardsOnTable.Count;

        RefreshSlots(cardsOnTable);
    }

    private void RefreshSlots(List<UICard> cards)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i < cards.Count)
            {
                slots[i].SetActive(true);
                Card data = cards[i].cardData;

                bool isWinner = _highlightRank != null
                    && data.rank == _highlightRank
                    && data.suit == _highlightSuit;

                // Fondo: verde si es la ganadora, blanco si no
                Image bg = slots[i].GetComponent<Image>();
                if (bg != null)
                    bg.color = isWinner ? colorGanador : Color.white;

                // Texto: emoji + valor, color según palo
                TMP_Text label = slots[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"{GetSuitEmoji(data.suit)}\n{data.rank}";
                    label.color = IsRed(data.suit) ? colorRojo : colorOscuro;
                }
            }
            else
            {
                slots[i].SetActive(false);
            }
        }
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Resalta en verde el slot que coincida con la carta ganadora.</summary>
    public void HighlightWinner(string rank, string suit)
    {
        _highlightRank = rank;
        _highlightSuit = suit;
        _lastCardCount = -1; // Forzar refresco visual
    }

    /// <summary>Limpia el resaltado verde (llamar cuando se limpie la mesa).</summary>
    public void ClearHighlight()
    {
        _highlightRank = null;
        _highlightSuit = null;
        _lastCardCount = -1;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetSuitEmoji(string suit)
    {
        return suit switch
        {
            "Corazones" => "♥",
            "Diamantes" => "♦",
            "Picas"     => "♠",
            "Tréboles"  => "♣",
            _           => ""
        };
    }

    private static bool IsRed(string suit) =>
        suit == "Corazones" || suit == "Diamantes";
}
