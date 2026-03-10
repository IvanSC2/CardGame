using UnityEngine;
using TMPro;
public class OptionController : MonoBehaviour
{
    [Header ("Referecias UI")]
    public TMP_Text textoValor;
    
    [Header ("Configuración")]
    public string[] opciones;
    private int indiceActual =0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        if (opciones.Length> 0 && textoValor != null)
        {
            textoValor.text = opciones[indiceActual];
        }

    }
    public int ObtenerIndice()
    {
        return indiceActual;
    }

    // Añade esta función al final de ArcadeSelector.cs
    public void EstablecerIndice(int nuevoIndice)
    {
        // Comprobamos por seguridad que el índice sea válido
        if (nuevoIndice >= 0 && nuevoIndice < opciones.Length)
        {
            indiceActual = nuevoIndice;
            ActualizarPantalla();
        }
    }
}
