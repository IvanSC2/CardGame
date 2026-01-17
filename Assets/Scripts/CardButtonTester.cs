using UnityEngine;
using UnityEngine.UI;

public class CardButtonTester : MonoBehaviour
{
    [Header("Referencias")]
    public UICard uiCard;          // Script de la carta en CardPrefab
    public Button generateButton;  // Botón de la UI

    void Start()
    {
        Debug.Log("[CardButtonTester] Start() llamado");

        if (uiCard == null)
            Debug.LogError("[CardButtonTester] uiCard es NULL en el inspector");
        if (generateButton == null)
            Debug.LogError("[CardButtonTester] generateButton es NULL en el inspector");

        generateButton.onClick.AddListener(OnGenerateClicked);
        Debug.Log("[CardButtonTester] Listener del botón registrado");
    }

    public void OnGenerateClicked()
    {
        Debug.Log("[CardButtonTester] Botón pulsado");

        Card randomCard = CardDatabase.GetRandomCard();
        if (randomCard == null)
        {
            Debug.LogError("[CardButtonTester] GetRandomCard devolvió NULL");
            return;
        }

        Debug.Log($"[CardButtonTester] Carta aleatoria: {randomCard.cardName}");
        uiCard.SetCard(randomCard);
    }
}
