using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro; // <--- 1. IMPORTANTE: Necesario para que Unity entienda qué es un Texto

public class TableZone : MonoBehaviour, IPointerClickHandler
{
    public static TableZone Instance;

    [Header("Marcadores Bazas")]
    public TMP_Text scoreTextP1; // Arrastra aquí el texto de "P1: 0"
    public TMP_Text scoreTextP2; // Arrastra aquí el texto de "P2: 0"

    [Header("Marcadores Vidas")]
    public TMP_Text livesTextP1; // Arrastra un texto nuevo aquí (ej: "♥♥♥")
    public TMP_Text livesTextP2; // Arrastra un texto nuevo aquí

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
            //Mueve de handArea a CoTable
            cardToMove.transform.SetParent(this.transform);
            //Lo pone en el centro
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


    private string GetHeartsString(int lives)
{
    // Si tiene 0 o menos, devolvemos string vacío o una calavera ☠️
    if (lives <= 0) return "";
    
    // Truco de C# para repetir un carácter X veces
    return new string('♥', lives); 
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

        if (bazasJugadas >= 5) 
    {
        Debug.Log("--- 🏁 FIN DE LA MANO: RESULTADOS ---");

    int apuestaP1 = BettingManager.Instance.p1Bet;
    int apuestaP2 = BettingManager.Instance.p2Bet;

    // --- JUGADOR 1 ---
    if (p1Wins == apuestaP1) {
        Debug.Log($"<color=green>P1 CUMPLE:</color> Mantiene sus vidas.");
    } else {
        InteractionManager.Instance.p1Vidas--; // 🔥 RESTA VIDA
        Debug.Log($"<color=red>P1 FALLA:</color> Pierde un corazón.");
    }

    // --- JUGADOR 2 ---
    if (p2Wins == apuestaP2) {
        Debug.Log($"<color=green>P2 CUMPLE:</color> Mantiene sus vidas.");
    } else {
        InteractionManager.Instance.p2Vidas--; // 🔥 RESTA VIDA
        Debug.Log($"<color=red>P2 FALLA:</color> Pierde un corazón.");
    }

    // 1. Actualizamos visualmente INMEDIATAMENTE para que se vea el corazón desaparecer
    UpdateUI();

    // 2. Comprobamos Game Over (Opcional por ahora)
    if (InteractionManager.Instance.p1Vidas <= 0 || InteractionManager.Instance.p2Vidas <= 0)
    {
        Debug.Log("💀 GAME OVER PARA ALGUIEN 💀");
        // Aquí llamarías a tu pantalla de fin de partida
    }

    // 3. Limpieza normal
    StartCoroutine(CleanTableRoutine());
    
    // Reset contadores de la ronda
    bazasJugadas = 0;
    p1Wins = 0;
    p2Wins = 0;
    }
    }

    // Método dedicado exclusivamente a pintar los textos
    private void UpdateUI()
{
    // Actualiza Puntos
    scoreTextP1?.SetText($"P1: {p1Wins}");
    scoreTextP2?.SetText($"P2: {p2Wins}");

    // Actualiza Vidas (Usando el Singleton)
    if (InteractionManager.Instance != null)
    {
        string p1Corazones = GetHeartsString(InteractionManager.Instance.p1Vidas);
        string p2Corazones = GetHeartsString(InteractionManager.Instance.p2Vidas);

        livesTextP1?.SetText(p1Corazones);
        livesTextP2?.SetText(p2Corazones);
        
        // Opcional: Cambiar color si queda 1 vida (Feedback visual)
        if (livesTextP1 != null) 
            livesTextP1.color = (InteractionManager.Instance.p1Vidas == 1) ? Color.red : Color.white;
            
        if (livesTextP2 != null) 
            livesTextP2.color = (InteractionManager.Instance.p2Vidas == 1) ? Color.red : Color.white;
    }
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