using UnityEngine;
using System.Collections.Generic;

public class TableManagerLayout : MonoBehaviour
{
    public static TableManagerLayout Instance;
    int numBots = GameConfig.nPlayers;

    [Header("Tu Molde (Prefab)")]
    public GameObject handAreaPrefab;

    [Header("Contenedor (Vacío dentro de CoTable)")]
    public Transform contenedorHandAreas;

    [Header("Geometría de tu Mesa")]
    public float radioX = 600f;
    public float radioY = 420f;
    public float rotacionX_TodaLaMesa = -50f;

    [Header("Conexión con tu script de Escala")]
    public TablePerspectiveManager perspectiveManager;

    [Header("Datos Públicos")]
    public List<CanvasGroup> manosActivas = new List<CanvasGroup>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {

        GenerarMesa(numBots+1);
    }

    public void GenerarMesa(int numJugadores)
    {
        // 1. Limpiamos solo los HandAreas
        foreach (Transform child in contenedorHandAreas) Destroy(child.gameObject);
        manosActivas.Clear();

        if (perspectiveManager != null)
            perspectiveManager.handAreas.Clear();

        // 2. MESA VIRTUAL: Calculamos asientos extra para dejar huecos libres
        int asientosVirtuales = numJugadores;
        if (numJugadores == 5) asientosVirtuales = 7; // Mesa de 7 (P1 + 4 Bots + 2 Huecos abajo)
        if (numJugadores == 6) asientosVirtuales = 8; // Mesa de 8 (P1 + 5 Bots + 2 Huecos abajo)

        // Grados por asiento
        float anguloPaso = 360f / asientosVirtuales;
        
        GameObject manoP1 = null;
        GameObject[] manosGeneradas = new GameObject[numJugadores];

        //Bucle para sentarlos a todos
        for (int i = 0; i < numJugadores; i++)
        {
            // TRUCO: Decidimos en qué "silla" real los sentamos
            int indexAsiento = i;
            
            // Si hay 5 o 6 jugadores y no eres tú (P1), nos saltamos la silla 1
            // Esto empuja a los bots hacia arriba y deja vacíos los lados de tu mano
            if ((numJugadores >= 5) && i > 0) 
            {
                indexAsiento = i + 1; 
            }

            float anguloGrados = (indexAsiento * anguloPaso) - 90f;
            float anguloRadianes = anguloGrados * Mathf.Deg2Rad;
            float radioX_Usado = radioX;
            float radioY_Usado = radioY;

            if (i != 0) // Si NO eres tú (P1)
            {
                // Un multiplicador: 1.15f un 15% más lejos del centro
                float multiplicadorDistancia = 1f;

                // Definimos cuánto se acercan o se alejan del centro según lo llena que esté la mesa
                if (numJugadores == 2) multiplicadorDistancia = 0.9f;      // Más cerca de lo normal (-15%)
                else if (numJugadores == 3) multiplicadorDistancia = 1.0f;  // Distancia normal (100%)
                else if (numJugadores == 4) multiplicadorDistancia = 1.15f; // Un poco más lejos (+10%)
                else if (numJugadores == 5) multiplicadorDistancia = 1.20f; // Bastante lejos (+20%)
                else if (numJugadores >= 6) multiplicadorDistancia = 1.25f; // Pegados al borde de la pantalla (+30%)

                radioX_Usado *= multiplicadorDistancia;
                radioY_Usado *= multiplicadorDistancia;
            }

            float posX = radioX_Usado * Mathf.Cos(anguloRadianes);
            float posY = radioY_Usado * Mathf.Sin(anguloRadianes);

            // --- INSTANCIAR SOLO HAND AREA ---
            if (handAreaPrefab != null)
            {
                GameObject handArea = Instantiate(handAreaPrefab, contenedorHandAreas);
                handArea.name = $"HandArea_P{i + 1}";
                if (i == 0) manoP1 = handArea;

                RectTransform rtHand = handArea.GetComponent<RectTransform>();
                rtHand.anchoredPosition = new Vector2(posX, posY);

                // --- PERSPECTIVA VISUAL FORZADA DINÁMICA ---
                Vector3 rotacionFinal = Vector3.zero;

                // Situación: 2 Jugadores (Cara a cara)
                if (numJugadores == 2)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P1 (Abajo)
                    if (i == 1) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P2 (Enfrente)
                }
                // Situación: 3 Jugadores (Triángulo)
                else if (numJugadores == 3)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P1 (Abajo)
                    if (i == 1) rotacionFinal = new Vector3(45f, -90f, -90f);    // P2 (Derecha)
                    if (i == 2) rotacionFinal = new Vector3(135f, -90f, -90f);   // P3 (Izquierda)
                }
                // Situación: 4 Jugadores (Cruz)
                else if (numJugadores == 4)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P1 (Abajo)
                    if (i == 1) rotacionFinal = new Vector3(0f, -90f, -90f);     // P2 (Derecha)
                    if (i == 2) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P3 (Enfrente)
                    if (i == 3) rotacionFinal = new Vector3(0f, 90f, 90f);       // P4 (Izquierda)
                }
                // Situación: 5 o 6 Jugadores (Automático usando el asiento virtual)
                else if (numJugadores >= 5)
                {
                    if (i == 0) 
                        rotacionFinal = new Vector3(-45f, 0f, 0f); 
                    else 
                        rotacionFinal = new Vector3(anguloGrados, -90f, -90f); 
                }

                rtHand.localEulerAngles = rotacionFinal;
                manosGeneradas[i] = handArea;
                // -------------------------------------
             
                // Pasamos la mano al Manager para que la escale
                if (perspectiveManager != null)
                {
                    perspectiveManager.handAreas.Add(rtHand);
                }

                CanvasGroup cg = handArea.GetComponentInChildren<CanvasGroup>();
                if (cg != null) manosActivas.Add(cg);
            }
        }

        // --- ORDEN DE DIBUJADO---
        if (numJugadores == 5)
        {
            // Ordenamos del más lejano (0) al más cercano (4)
            manosGeneradas[2].transform.SetSiblingIndex(0); // P3 (Fondo derecha)
            manosGeneradas[3].transform.SetSiblingIndex(1); // P4 (Fondo izquierda)
            manosGeneradas[1].transform.SetSiblingIndex(2); // P2 (Frente derecha) -> Delante de P3
            manosGeneradas[4].transform.SetSiblingIndex(3); // P5 (Frente izquierda) -> Delante de P4
            manosGeneradas[0].transform.SetSiblingIndex(4); // P1 (Tu mano) -> Delante de todos
        }
        else if (numJugadores == 6)
        {
            // Ordenamos del más lejano (0) al más cercano (5)
            manosGeneradas[3].transform.SetSiblingIndex(0); // P4 (Arriba del todo)
            manosGeneradas[2].transform.SetSiblingIndex(1); // P3 (Arriba derecha) -> Delante de P4
            manosGeneradas[4].transform.SetSiblingIndex(2); // P5 (Arriba izquierda) -> Delante de P4
            manosGeneradas[1].transform.SetSiblingIndex(3); // P2 (Abajo derecha) -> Delante de P3
            manosGeneradas[5].transform.SetSiblingIndex(4); // P6 (Abajo izquierda) -> Delante de P5
            manosGeneradas[0].transform.SetSiblingIndex(5); // P1 (Tu mano) -> Delante de todos
        }
        else
        {
            // Para 2, 3 o 4 jugadores, solos nos aseguramos que este delante P1
            if (manosGeneradas.Length > 0 && manosGeneradas[0] != null)
            {
                manosGeneradas[0].transform.SetAsLastSibling();
            }
        }

        Debug.Log($"[MESA] Se han generado {numJugadores} jugadores en círculo (Asientos Virtuales: {asientosVirtuales}).");
    }
    
}