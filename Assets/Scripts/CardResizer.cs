using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways] // ¡Truco! Funciona en el editor sin dar al Play
public class CardResizer : MonoBehaviour
{
    [Header("Configuración")]
    public RectTransform targetVisuals; // Arrastra aquí el objeto 'Visuals'
    public Vector2 referenceSize = new Vector2(200, 280); // El tamaño "ideal" de tu diseño

    private RectTransform myRect;

    void OnEnable()
    {
        myRect = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (myRect == null || targetVisuals == null) return;

        // 1. Calculamos cuánto nos ha estirado/encogido el padre (HandArea)
        // Usamos la altura (Height) como referencia principal
        float scaleRatio = myRect.rect.height / referenceSize.y;

        // 2. Aplicamos ese ratio como ESCALA (Zoom) al hijo visual
        // Así, si el hueco es la mitad, la carta se ve a la mitad (0.5)
        targetVisuals.localScale = new Vector3(scaleRatio, scaleRatio, 1f);
    }
}