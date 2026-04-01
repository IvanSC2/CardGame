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
        int totalJugadores = handAreas.Count;
        for (int i = 0; i < handAreas.Count; i++)
        {
            if (handAreas[i] != null)
            {
                // Ahora lee exactamente la casilla "Pos Y" de tu Inspector
                float posYActual = handAreas[i].anchoredPosition.y;

                // Muestro los números en el Inspector 
                if (i == 0) posY_Leida_P1 = posYActual;
                if (i == 1) posY_Leida_P2 = posYActual;

                // Calcula la regla de tres
                float porcentaje = Mathf.InverseLerp(yAbajo, yFondo, posYActual);
                float escalaCalculada = Mathf.Lerp(escalaFrente, escalaFondo, porcentaje);
                // 2. NUEVO: Calculamos el multiplicador por "aglomeración"
                float multOcupacion = 1f;
                
                // Si el nombre del objeto NO termina en "P1", es un BOT.
                if (!handAreas[i].gameObject.name.Contains("P1"))
                {
                    if (totalJugadores == 5) multOcupacion = 0.95f; // 25% más pequeñas
                    else if (totalJugadores >= 6) multOcupacion = 0.90f; // 35% más pequeñas
                }
                float escalaFinal = escalaCalculada * multOcupacion;

                // Aplica el tamaño
                handAreas[i].localScale = new Vector3(escalaFinal, escalaFinal, escalaFinal);
            }
        }
    }

    public float CalcularEscala(float posY)
    {
        float porcentaje = Mathf.InverseLerp(yAbajo, yFondo, posY);
        return Mathf.Lerp(escalaFrente, escalaFondo, porcentaje);
    }
}