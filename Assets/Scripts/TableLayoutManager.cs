using UnityEngine;
using System.Collections.Generic;

public class TableGenerator : MonoBehaviour
{
    public static TableGenerator Instance;

    [Header("Los Prefabs Base")]
    public GameObject tapetePrefab;   // Tu dibujo de Tapete1 convertido en Prefab
    public GameObject handAreaPrefab; // Tu HandArea1 convertido en Prefab

    [Header("Los Contenedores (Jerarquía)")]
    public Transform contenedorTapetes; // Un objeto vacío DENTRO del molde
    public Transform contenedorHandAreas; // Un objeto vacío FUERA del molde (para las cartas)

    [Header("Ajustes de la Mesa")]
    public float radioX = 400f; // Ancho del óvalo
    public float radioY = 200f; // Alto del óvalo (más pequeño por la perspectiva 3D)
    public float inclinacionCartasX = 40f; // Los grados que se levantan las cartas para compensar el 3D

    [Header("Listas Generadas")]
    public List<CanvasGroup> manosActivas = new List<CanvasGroup>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        GenerarMesa(GameConfig.nPlayers); // Lee de tu menú
    }

    public void GenerarMesa(int numJugadores)
    {
        // 1. Limpiar la mesa por si acaso
        foreach (Transform child in contenedorTapetes) Destroy(child.gameObject);
        foreach (Transform child in contenedorHandAreas) Destroy(child.gameObject);
        manosActivas.Clear();

        // 2. Calcular la separación entre jugadores
        float anguloPaso = 360f / numJugadores;
        
        // 3. Ajustar escala general si son muchos jugadores
        float escalaGeneral = 1f;
        if (numJugadores >= 4) escalaGeneral = 0.8f;
        if (numJugadores >= 6) escalaGeneral = 0.65f;

        // 4. Bucle generador
        for (int i = 0; i < numJugadores; i++)
        {
            // Restamos 90 grados para que el Jugador 0 empiece siempre ABAJO del todo (tu posición)
            float anguloGrados = (i * anguloPaso) - 90f; 
            float anguloRadianes = anguloGrados * Mathf.Deg2Rad;

            // Calcular posición X e Y en la elipse
            float posX = radioX * Mathf.Cos(anguloRadianes);
            float posY = radioY * Mathf.Sin(anguloRadianes);
            Vector3 posicionLocal = new Vector3(posX, posY, 0);

            // Calcular la rotación para que miren al centro
            // Sumamos 90 porque la "cabeza" de tu carta apunta hacia arriba por defecto
            float rotacionZ = anguloGrados + 90f; 

            // --- INSTANCIAR TAPETE (Visual, DENTRO de la mesa) ---
            GameObject nuevoTapete = Instantiate(tapetePrefab, contenedorTapetes);
            nuevoTapete.name = $"Tapete_P{i + 1}";
            nuevoTapete.transform.localPosition = posicionLocal;
            nuevoTapete.transform.localEulerAngles = new Vector3(0, 0, rotacionZ);
            nuevoTapete.transform.localScale = Vector3.one * escalaGeneral;

            // --- INSTANCIAR HAND AREA (Lógica, FUERA de la mesa) ---
            GameObject nuevaHandArea = Instantiate(handAreaPrefab, contenedorHandAreas);
            nuevaHandArea.name = $"HandArea_P{i + 1}";
            nuevaHandArea.transform.localPosition = posicionLocal;
            
            // Aquí aplicamos la rotación en Z para que mire al centro, 
            // Y la rotación en X para que se levante del tapete (perspectiva 3D)
            // Al P1 (i==0) le ponemos X=0 para que te mire plano a ti. Al resto, la inclinación.
            float rotX = (i == 0) ? 0f : inclinacionCartasX;
            nuevaHandArea.transform.localEulerAngles = new Vector3(rotX, 0, rotacionZ);
            nuevaHandArea.transform.localScale = Vector3.one * escalaGeneral;

            // Guardar en la lista para el InteractionManager
            CanvasGroup cg = nuevaHandArea.GetComponentInChildren<CanvasGroup>();
            if (cg != null) manosActivas.Add(cg);
        }

        Debug.Log($"[SISTEMA] Mesa procedural generada para {numJugadores} jugadores.");
    }
}