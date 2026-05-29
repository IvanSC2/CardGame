using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode; 

public class TableManagerLayout : MonoBehaviour
{
    public static TableManagerLayout Instance;
    
    // Dejamos esto como base, pero en multijugador real esto nos lo dirá el Host
    int numBots = GameConfig.nPlayers; 

    [Header("Tu Molde (Prefab)")]
    public GameObject handAreaPrefab;

    [Header("Perfiles (UI)")]
    public GameObject playerProfilePrefab;
    
    [Tooltip("Separación extra desde el borde de la mesa")]
    public float radioExtraPerfil = 120f; 
    
    public List<PlayerProfileUI> perfilesActivos = new List<PlayerProfileUI>();

    [Header("Contenedor")]
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

    public void GenerarMesa(int numJugadores, int localSeatIndex)
    {   
        
        if (CardDatabase.deck != null) CardDatabase.deck.Clear(); 
        manosActivas = new List<CanvasGroup>(); 
        // 1. Limpieza inicial
        foreach (Transform child in contenedorHandAreas) Destroy(child.gameObject);
        manosActivas.Clear();
        perfilesActivos.Clear(); 

        if (perspectiveManager != null)
            perspectiveManager.handAreas.Clear();

        // El localSeatIndex viene asignado por el servidor (0..N-1)
        int localId = localSeatIndex;

        // 2. MESA VIRTUAL: Calculamos asientos extra
        int asientosVirtuales = numJugadores;
        if (numJugadores == 5) asientosVirtuales = 7; 
        if (numJugadores == 6) asientosVirtuales = 8; 

        float anguloPaso = 360f / asientosVirtuales;
        
        GameObject[] manosGeneradas = new GameObject[numJugadores];

        for (int i = 0; i < numJugadores; i++)
        {
            //  Desplazamos el índice visual basándonos en tu ID local
            int visualIndex = (i - localId + numJugadores) % numJugadores;
            
            // A partir de aquí, para dibujar sillas y posiciones usamos "visualIndex"
            int indexAsiento = visualIndex;
            
            if ((numJugadores >= 5) && visualIndex > 0) 
            {
                indexAsiento = visualIndex + 1; 
            }

            float anguloGrados = (indexAsiento * anguloPaso) - 90f;
            float anguloRadianes = anguloGrados * Mathf.Deg2Rad;
            float radioX_Usado = radioX;
            float radioY_Usado = radioY;

            if (visualIndex != 0) 
            {
                float multiplicadorDistancia = 1f;
                if (numJugadores == 2) multiplicadorDistancia = 0.9f;      
                else if (numJugadores == 3) multiplicadorDistancia = 1.0f;  
                else if (numJugadores == 4) multiplicadorDistancia = 1.15f; 
                else if (numJugadores == 5) multiplicadorDistancia = 1.20f; 
                else if (numJugadores >= 6) multiplicadorDistancia = 1.25f; 

                radioX_Usado *= multiplicadorDistancia;
                radioY_Usado *= multiplicadorDistancia;
            }

            float posX = radioX_Usado * Mathf.Cos(anguloRadianes);
            float posY = radioY_Usado * Mathf.Sin(anguloRadianes);

            // --- A) INSTANCIAR HAND AREA (Las cartas) ---
            if (handAreaPrefab != null)
            {
                GameObject handArea = Instantiate(handAreaPrefab, contenedorHandAreas);
                
                handArea.name = $"HandArea_JUG{i}";

                RectTransform rtHand = handArea.GetComponent<RectTransform>();
                rtHand.anchoredPosition = new Vector2(posX, posY);

                Vector3 rotacionFinal = Vector3.zero;

                // Las rotaciones visuales también usan visualIndex
                if (numJugadores == 2)
                {
                    if (visualIndex == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (visualIndex == 1) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                }
                else if (numJugadores == 3)
                {
                    if (visualIndex == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (visualIndex == 1) rotacionFinal = new Vector3(45f, -90f, -90f);    
                    if (visualIndex == 2) rotacionFinal = new Vector3(135f, -90f, -90f);   
                }
                else if (numJugadores == 4)
                {
                    if (visualIndex == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (visualIndex == 1) rotacionFinal = new Vector3(0f, -90f, -90f);     
                    if (visualIndex == 2) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (visualIndex == 3) rotacionFinal = new Vector3(0f, 90f, 90f);       
                }
                else if (numJugadores >= 5)
                {
                    if (visualIndex == 0) rotacionFinal = new Vector3(-45f, 0f, 0f); 
                    else rotacionFinal = new Vector3(anguloGrados, -90f, -90f); 
                }

                rtHand.localEulerAngles = rotacionFinal;
                // Guardamos en el array real usando 'i'
                manosGeneradas[i] = handArea;
             
                if (perspectiveManager != null) perspectiveManager.handAreas.Add(rtHand);

                CanvasGroup cg = handArea.GetComponentInChildren<CanvasGroup>();
                if (cg != null) manosActivas.Add(cg);
            }

            // --- B) INSTANCIAR PERFIL DEL JUGADOR ---
            if (playerProfilePrefab != null)
            {
                GameObject perfilObj = Instantiate(playerProfilePrefab, contenedorHandAreas);
                perfilObj.name = $"Profile_JUG{i}";
                
                RectTransform rtPerfil = perfilObj.GetComponent<RectTransform>();
                
                float radioX_Perfil = radioX_Usado + radioExtraPerfil;
                float radioY_Perfil = radioY_Usado + radioExtraPerfil;
                float perfilPosX = radioX_Perfil * Mathf.Cos(anguloRadianes);
                float perfilPosY = radioY_Perfil * Mathf.Sin(anguloRadianes);

                // 2. OVERRIDE MANUAL usando visualIndex
                if (visualIndex == 0) 
                {
                    // TÚ (Siempre serás el índice visual 0, sin importar tu ID de red)
                    perfilPosX = -1000f; 
                    perfilPosY = -500f; 
                    rtPerfil.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                }
                else if (numJugadores == 2)
                {
                    if (visualIndex == 1) { perfilPosX = 0f; perfilPosY = 1500f; } 
                }
                else if (numJugadores == 3)
                {
                    if (visualIndex == 1) { perfilPosX = 1100f; perfilPosY = 1100f; }  
                    else if (visualIndex == 2) { perfilPosX = -1100f; perfilPosY = 1100f; } 
                }
                else if (numJugadores == 4)
                {
                    if (visualIndex == 1) { perfilPosX = 1300f; perfilPosY = 400f; } 
                    else if (visualIndex == 2) { perfilPosX = 0f; perfilPosY = 1700f; } 
                    else if (visualIndex == 3) { perfilPosX = -1300f; perfilPosY = 400f; } 
                }
                else if (numJugadores == 5)
                {
                    if (visualIndex == 1) { perfilPosX = 1400f; perfilPosY = 300f; } 
                    else if (visualIndex == 2) { perfilPosX = 700f; perfilPosY = 1600f; } 
                    else if (visualIndex == 3) { perfilPosX = -700f; perfilPosY = 1600f; } 
                    else if (visualIndex == 4) { perfilPosX = -1400f; perfilPosY = 300f; } 
                }
                else if (numJugadores >= 6)
                {
                    if (visualIndex == 1) { perfilPosX = 1400f; perfilPosY = 200f; } 
                    else if (visualIndex == 2) { perfilPosX = 1000f; perfilPosY = 1400f; } 
                    else if (visualIndex == 3) { perfilPosX = 0f; perfilPosY = 1750f; } 
                    else if (visualIndex == 4) { perfilPosX = -1000f; perfilPosY = 1400f; } 
                    else if (visualIndex == 5) { perfilPosX = -1400f; perfilPosY = 200f; } 
                }
                
                rtPerfil.anchoredPosition = new Vector2(perfilPosX, perfilPosY);
                rtPerfil.localEulerAngles = new Vector3(rotacionX_TodaLaMesa, 0f, 0f); 

                PlayerProfileUI profileUI = perfilObj.GetComponent<PlayerProfileUI>();
                if (profileUI != null)
                {
                    perfilesActivos.Add(profileUI);
                    string nombre = $"JUGADOR {i}";
                    profileUI.ActualizarPerfil(nombre, 3, 0, -1);
                }
            }
        }

        // --- ORDEN DE DIBUJADO (Aseguramos que tu mano se dibuje la última para tapar al resto) ---
        
        if (manosGeneradas.Length > localId && manosGeneradas[localId] != null)
        {
            manosGeneradas[localId].transform.SetAsLastSibling();
        }
        
        // Ponemos los perfiles primero para que se dibujen detrás de las barajas
        foreach(PlayerProfileUI perfil in perfilesActivos)
        {
            perfil.transform.SetAsFirstSibling();
        }
    }
}