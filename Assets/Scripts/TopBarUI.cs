using UnityEngine;
using TMPro;

public class TopBarUI : MonoBehaviour
{
    public static TopBarUI Instance;

    [Header("UI Elementos")]
    public TMP_Text textoMonedas;

    // La memoria interna de las monedas
    private int monedasActuales;

    private void Awake()
    {
        
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        //  Al arrancar el juego, CARGAMOS las monedas guardadas en la memoria del móvil.
        // Si es la primera vez que juega y no hay guardado, le damos 450 de regalo.
        monedasActuales = PlayerPrefs.GetInt("MisMonedas", 450);
        
        ActualizarPantalla();
    }

    
    public void ActualizarMonedas(int cantidadASumar)
    {
        // Sumamos (o restamos) la cantidad
        monedasActuales += cantidadASumar;

        // GUARDAMOS el nuevo total en la memoria del móvil
        PlayerPrefs.SetInt("MisMonedas", monedasActuales);
        PlayerPrefs.Save(); 

        // 4. Actualizamos el texto
        ActualizarPantalla();
    }

    // Añadir en TopBarUI.cs
    public bool TieneSuficientes(int coste)
    {
        return monedasActuales >= coste;
    }    
    public bool GastarMonedas(int coste)
    {
        if (monedasActuales >= coste)
        {
            ActualizarMonedas(-coste); 
            return true; // Compra exitosa
        }
        
        Debug.Log("No tienes suficientes monedas.");
        return false; // No puede pagar
    }

    private void ActualizarPantalla()
    {
        if (textoMonedas != null)
        {
            textoMonedas.text = monedasActuales.ToString();
        }
    }
}