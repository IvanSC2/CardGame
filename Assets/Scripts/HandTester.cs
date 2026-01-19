using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Necesario para usar Listas

public class HandTester : MonoBehaviour
{

    [Header ("Configuración")]
    public GameObject cardPrefab;
    public Transform handContainer;
    public Button drawHandButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        drawHandButton.onClick.AddListener(DrawNewHand);
    }

       public void DrawNewHand()
    {
        //Borramos que hab ian por limpieza
        foreach (Transform child in handContainer)
        {
            Destroy(child.gameObject);

        }
        //Bucle de creacion de las 5 cartas
        for(int i=0; i<5; i++)
        {
            Card data= CardDatabase.DrawTopCard();
            GameObject newCardObj = Instantiate(cardPrefab, handContainer);

            //Meto los datos en la carta:
            UICard uiLogic = newCardObj.GetComponent<UICard>();
            uiLogic.SetCard(data);
        }

    }
}
