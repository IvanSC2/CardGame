using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class TablePerspectiveManager : MonoBehaviour
{
    [Header("Las áreas de los jugadores a controlar")]
    public List<RectTransform> handAreas;

    [Header("Límites de la Mesa (Pon números grandes para probar)")]
    public float yAbajo = -600f; 
    public float yFondo = 600f;

    [Header("Tamaños")]
    public float escalaFrente = 1f;
    public float escalaFondo = 0.65f;

    [Header("DEBUG: ¿Qué números está leyendo Unity?")]
    [Tooltip("Mira aquí para ver la Pos Y real que lee el código")]
    public float posY_Leida_P1;
    public float posY_Leida_P2;

    void Update()
    {
        if (handAreas == null || handAreas.Count == 0) return;

        for (int i = 0; i < handAreas.Count; i++)
        {
            if (handAreas[i] != null)
            {
                // Ahora lee exactamente la casilla "Pos Y" de tu Inspector
                float posYActual = handAreas[i].anchoredPosition.y;

                // Te muestro los números en el Inspector para que no vayas a ciegas
                if (i == 0) posY_Leida_P1 = posYActual;
                if (i == 1) posY_Leida_P2 = posYActual;

                // Calcula la regla de tres
                float porcentaje = Mathf.InverseLerp(yAbajo, yFondo, posYActual);
                float escalaCalculada = Mathf.Lerp(escalaFrente, escalaFondo, porcentaje);

                // Aplica el tamaño
                handAreas[i].localScale = new Vector3(escalaCalculada, escalaCalculada, escalaCalculada);
            }
        }
    }

    public float CalcularEscala(float posY)
    {
        float porcentaje = Mathf.InverseLerp(yAbajo, yFondo, posY);
        return Mathf.Lerp(escalaFrente, escalaFondo, porcentaje);
    }
}