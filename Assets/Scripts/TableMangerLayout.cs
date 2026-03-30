using UnityEngine;
using System.Collections.Generic;

public class TableManagerLayout : MonoBehaviour
{
    public static TableManagerLayout Instance;

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
        
        GenerarMesa(4); 
    }

    public void GenerarMesa(int numJugadores)
    {
        // 1. Limpiamos solo los HandAreas
        foreach (Transform child in contenedorHandAreas) Destroy(child.gameObject);
        manosActivas.Clear();
        
        if(perspectiveManager != null) 
            perspectiveManager.handAreas.Clear();

        // 2. Calculamos los grados
        float anguloPaso = 360f / numJugadores;

        // 3. Bucle para sentarlos a todos
        for (int i = 0; i < numJugadores; i++)
        {
            float anguloGrados = (i * anguloPaso) - 90f; 
            float anguloRadianes = anguloGrados * Mathf.Deg2Rad;

            float posX = radioX * Mathf.Cos(anguloRadianes);
            float posY = radioY * Mathf.Sin(anguloRadianes);

            // --- INSTANCIAR SOLO HAND AREA ---
            if (handAreaPrefab != null)
            {
                GameObject handArea = Instantiate(handAreaPrefab, contenedorHandAreas);
                handArea.name = $"HandArea_P{i + 1}";
                
                RectTransform rtHand = handArea.GetComponent<RectTransform>();
                rtHand.anchoredPosition = new Vector2(posX, posY);

                

                //PERSPECTIVA VISUAL FORZADA ---
               Vector3 rotacionFinal = Vector3.zero;

                if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P1 (Abajo)
                if (i == 1) rotacionFinal = new Vector3(0f, -90f, -90f);     // P2 (Derecha)
                if (i == 2) rotacionFinal = new Vector3(-45f, 0f, 0f);       // P3 (Enfrente)
                if (i == 3) rotacionFinal = new Vector3(0f, 90f, 90f);       // P4 (Izquierda)

                rtHand.localEulerAngles = rotacionFinal;
                // -------------------------------------

                // --- NUEVO: CONFIGURACIÓN ABANICO CLÁSICO PARA PLAYER 1 ---
               /* if (i == 0) 
                {
                    HandLayoutFanner fannerP1 = handArea.GetComponent<HandLayoutFanner>();
                    if (fannerP1 != null)
                    {
                        // 1. Apagamos la curva 3D (para que no se deforme raro hacia atrás)
                        fannerP1.gradosYPorPaso = 0f;
                        fannerP1.fuerzaCaidaZ = 0f;
                        fannerP1.zCentro = 0f; // Puedes subir esto si quieres que las cartas se acerquen a la cámara

                        // 2. Encendemos la curva 2D (¡Tu idea!)
                        fannerP1.fuerzaCaidaY = 15f;    // Los extremos bajan 15 píxeles (ajusta a ojo)
                        fannerP1.gradosZPorPaso = 5f;   // Los extremos se inclinan 5 grados como un volante
                    }
                }*/
                // Pasamos la mano al Manager para que la escale
                if(perspectiveManager != null)
                {
                    perspectiveManager.handAreas.Add(rtHand);
                }

                CanvasGroup cg = handArea.GetComponentInChildren<CanvasGroup>();
                if (cg != null) manosActivas.Add(cg);
            }
        }

        Debug.Log($"[MESA] Se han generado {numJugadores} jugadores en círculo.");
    }
}