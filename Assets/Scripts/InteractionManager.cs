using UnityEngine;

public class InteractionManager : MonoBehaviour
{

    //Singleton
    public static InteractionManager Instance;
    //Carta que tenemos en la mano
    public UICard SelectedCard { get; private set; }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        // Si ya existe uno, destrúyete (solo puede haber un manager)
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    // Método llamado por la carta al ser clicada
    public void SelectCard(UICard card)
    {
        SelectedCard = card;
        Debug.Log($"Carta seleccionada: {card.name}. Esperando destino...");
    }

    // Método llamado por la mesa al recibir la carta
    public void ClearSelection()
    {
        SelectedCard = null;
        Debug.Log("Selección limpiada.");
    }

    // Utilidad para saber si hay algo seleccionado
    public bool HasCardSelected()
    {
        return SelectedCard != null;
    }
}
