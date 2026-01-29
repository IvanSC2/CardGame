using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro; // <--- 1. IMPORTANTE: Necesario para que Unity entienda qué es un Texto

public class TableZone : MonoBehaviour, IPointerClickHandler
{
    public static TableZone Instance;

    [Header("Marcadores UI")]
    public TMP_Text scoreTextP1; // Arrastra aquí el texto de "P1: 0"
    public TMP_Text scoreTextP2; // Arrastra aquí el texto de "P2: 0"

    [Header("Lógica Interna")]
    public int p1Wins = 0;
    public int p2Wins = 0;
    public int bazasJugadas = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void ResetStats()
    {
        p1Wins = 0;
        p2Wins = 0;
        bazasJugadas = 0;
        
        ClearTableNow();
        UpdateUI(); // <--- 2. Pone los marcadores a 0 al empezar
    }

    public void ClearTableNow()
    {
        foreach (Transform child in this.transform) Destroy(child.gameObject);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InteractionManager.Instance.HasCardSelected())
        {
            UICard cardToMove = InteractionManager.Instance.SelectedCard;

            // --- Lógica Visual y Movimiento ---
            CardResizer resizer = cardToMove.GetComponent<CardResizer>();
            Vector3 finalScale = Vector3.one;
            if (resizer != null)
            {
                finalScale = resizer.targetVisuals.localScale;
                resizer.enabled = false;
            }

            cardToMove.transform.SetParent(this.transform);
            cardToMove.transform.localPosition = Vector3.zero;
            cardToMove.transform.localScale = Vector3.one;
            if (resizer != null) resizer.targetVisuals.localScale = finalScale;
            cardToMove.GetComponent<UnityEngine.UI.Image>().color = Color.white;

            // Bloqueo
            CanvasGroup group = cardToMove.GetComponent<CanvasGroup>();
            if (group == null) group = cardToMove.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            InteractionManager.Instance.ClearSelection();
           

           if (this.transform.childCount == 2)
            {
                // 1. ¡PAUSA EL JUEGO! Nadie toca nada.
                InteractionManager.Instance.isPaused = true;
                InteractionManager.Instance.UpdateVisualStates(); // Pone todo gris

                // 2. Comprobamos quién ganó
                CheckWinner();

                // 3. Empezamos la limpieza con retraso
                StartCoroutine(CleanTableRoutine());
            }
            else
            {
                // Si solo hay 1 carta, cambiamos el turno normalmente
                InteractionManager.Instance.ChangeTurn();
            }
        }
    }

    private void CheckWinner()
    {
        UICard card1 = this.transform.GetChild(0).GetComponent<UICard>();
        UICard card2 = this.transform.GetChild(1).GetComponent<UICard>();

        int score1 = CalculateScore(card1.cardData);
        int score2 = CalculateScore(card2.cardData);
        
        bazasJugadas++;

        // Sumamos puntos
        if (score1 > score2)
        {
            p1Wins++;
            Debug.Log("Gana P1");
        }
        else if (score2 > score1)
        {
            p2Wins++;
            Debug.Log("Gana P2");
        }

        // <--- 3. ¡AQUÍ ESTÁ LA CLAVE! Actualizamos lo que ve el jugador
        UpdateUI(); 

        if (bazasJugadas >= 5) {Debug.Log("FIN DE PARTIDA");
        bazasJugadas=0;
        p1Wins=0;
        p2Wins=0;
        UpdateUI();
        
        }
    }

    // Método dedicado exclusivamente a pintar los textos
    private void UpdateUI()
    {
        // El '?' es un truco de seguridad: si se te olvida arrastrar el texto, no da error, solo lo ignora.
        scoreTextP1?.SetText($"P1: {p1Wins}");
        scoreTextP2?.SetText($"P2: {p2Wins}");
    }

    IEnumerator CleanTableRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        ClearTableNow();
        // --- DESBLOQUEO ---
        // 1. Quitamos la pausa
        InteractionManager.Instance.isPaused = false;
        
        // 2. Y AHORA cambiamos el turno para que juegue el siguiente
        // (Esto reactivará los colores de las manos correctamente)
        InteractionManager.Instance.ChangeTurn();
    }

    private int CalculateScore(Card card)
    {
        int suitValue = 0;
        switch (card.suit)
        {
            case "Tréboles": suitValue = 1; break;
            case "Picas": suitValue = 2; break;
            case "Corazones": suitValue = 3; break;
            case "Diamantes": suitValue = 4; break;
        }
        return (card.value * 10) + suitValue;
    }
}