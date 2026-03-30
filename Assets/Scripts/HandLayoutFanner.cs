using UnityEngine;
using UnityEngine.UI; 

public class HandLayoutFanner : MonoBehaviour
{
    [Header("Separación Dinámica (Tus cálculos exactos)")]
    public float spacing5Cartas = -70f;   
    public float spacing4Cartas = -252f;  
    public float spacing3Cartas = -446f;  
    public float spacing2Cartas = -650f;  

    [Header("Curva 3D (Rotaciones y Profundidad)")]
    public float gradosYPorPaso = 16f; 
    public float zCentro = 30f;
    public float fuerzaCaidaZ = 30f;

    [Header("Ajustes Rápidos")]
    public bool invertirRotacionY = true;
    public float fuerzaCaidaY = 0f;    
    public float gradosZPorPaso = 0f;  

    private HorizontalLayoutGroup layoutGroup;

    void Start()
    {
        layoutGroup = GetComponent<HorizontalLayoutGroup>();
        Invoke("ReorganizarCartas", 0.1f);
    }

    public void ReorganizarCartas()
    {
        int numCards = transform.childCount;
        if (numCards == 0) return;

        if (layoutGroup == null) layoutGroup = GetComponent<HorizontalLayoutGroup>();
        
        // --- 1. APLICAMOS TUS NÚMEROS MÁGICOS ---
        if (layoutGroup != null)
        {
            if (numCards >= 5) layoutGroup.spacing = spacing5Cartas;
            else if (numCards == 4) layoutGroup.spacing = spacing4Cartas;
            else if (numCards == 3) layoutGroup.spacing = spacing3Cartas;
            else if (numCards <= 2) layoutGroup.spacing = spacing2Cartas;

            // --- 2. EL LATIGAZO A UNITY (Obliga a juntarlas al instante) ---
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        // --- 3. APLICAMOS EL ABANICO 3D ---
        float indiceCentro = (numCards - 1) / 2f;

        for (int i = 0; i < numCards; i++)
        {
            Transform card = transform.GetChild(i);
            float pasosDesdeCentro = indiceCentro - i; 
            float pasosAlCuadrado = pasosDesdeCentro * pasosDesdeCentro; 

            float direccionGiroY = invertirRotacionY ? -1f : 1f;
            float currentYRot = pasosDesdeCentro * gradosYPorPaso * direccionGiroY; 
            
            float currentZ = zCentro - (fuerzaCaidaZ * pasosAlCuadrado);
            float currentY = 0f - (fuerzaCaidaY * pasosAlCuadrado);
            float currentZRot = pasosDesdeCentro * gradosZPorPaso;

            // Mantenemos la X de Unity, solo tocamos la altura y profundidad
            card.localPosition = new Vector3(card.localPosition.x, card.localPosition.y + currentY, currentZ);
            card.localEulerAngles = new Vector3(card.localEulerAngles.x, currentYRot, currentZRot);
        }
    }
}