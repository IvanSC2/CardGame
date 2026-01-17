using UnityEngine;
using UnityEngine.UI;

public class CardTester : MonoBehaviour
{
    public UICard uiCard;     // tu prefab de carta instanciado en la escena
    public Button testButton; // botón en la UI

    void Start()
    {
        testButton.onClick.AddListener(OnClickGenerateCard);
    }

    void OnClickGenerateCard()
    {
        Card randomCard = CardDatabase.GetRandomCard(); // usamos el método nuevo
        uiCard.SetCard(randomCard);
    }
}
