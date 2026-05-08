using UnityEngine;
using TMPro;

public class OptionController : MonoBehaviour
{
    [Header("Referencias UI")]
    public TMP_Text textoValor;
    
    [Header("Configuración")]
    public string[] opciones;
    private int indiceActual = 0;

    void Start()
    {
        ActualizarPantalla();
    }

    public void AnteriorOpcion()
    {
        indiceActual--;
        if(indiceActual < 0)
        {
            indiceActual = opciones.Length - 1;        
        } 
        ActualizarPantalla();
    }
    
    public void SiguienteOpcion()
    {
         indiceActual++;
        if(indiceActual >= opciones.Length)
        {
            indiceActual = 0;        
        } 
        ActualizarPantalla();
    }

    public void ActualizarPantalla()
    {
        if (opciones.Length > 0 && textoValor != null)
        {
            textoValor.text = opciones[indiceActual];
        }
    }

    // --- GETTERS & SETTERS (Fundamentales para la lógica) ---

    public int ObtenerIndice()
    {
        return indiceActual;
    }

    public void EstablecerIndice(int nuevoIndice)
    {
        if (nuevoIndice >= 0 && nuevoIndice < opciones.Length)
        {
            indiceActual = nuevoIndice;
            ActualizarPantalla();
        }
    }

    public void ResetearComponente()
    {
        if (opciones == null || opciones.Length == 0) return;
        
        indiceActual = 0; 
        ActualizarPantalla(); 
    }
    
}