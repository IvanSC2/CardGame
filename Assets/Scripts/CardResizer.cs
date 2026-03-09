using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CardResizer : MonoBehaviour
{
    [Header("Referencias a Escalar")]
    public RectTransform targetVisuals; // Contenido
    public RectTransform backgroundVisuals; //Imagen de Fondo

    [Header("Configuración")]
    public Vector2 referenceSize = new Vector2(200, 280); 
    //Margen
    [Header("Ajuste Visual")]
    [Range(0.1f, 1f)] 
    public float scaleFactor = 0.85f; 

    private RectTransform myRect;

    void OnEnable()
    {
        myRect = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (myRect == null) return;

        // 1. Calculamos el ratio original basado en la altura
        float scaleRatio = myRect.rect.height / referenceSize.y;

        // 2. Aplicamos el factor de reducción (scaleFactor)
        float finalScale = scaleRatio * scaleFactor;
        Vector3 newScale = new Vector3(finalScale, finalScale, 1f);

        // 3. Aplicamos la escala al CONTENIDO
        if (targetVisuals != null)
        {
            // OJO: Si targetVisuals es hijo de backgroundVisuals, se escalará doble (evitar eso)
            targetVisuals.localScale = newScale;
        }

        // 4. Aplicamos la escala al FONDO (Nueva funcionalidad)
        if (backgroundVisuals != null)
        {
            backgroundVisuals.localScale = newScale;
        }
    }
}