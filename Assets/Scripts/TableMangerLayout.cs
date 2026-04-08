using UnityEngine;
using System.Collections.Generic;

public class TableManagerLayout : MonoBehaviour
{
    public static TableManagerLayout Instance;
    int numBots = GameConfig.nPlayers;

    [Header("Tu Molde (Prefab)")]
    public GameObject handAreaPrefab;

    [Header("Perfiles (UI)")]
    public GameObject playerProfilePrefab;
    
    [Tooltip("Separación extra desde el borde de la mesa (Solo se usa si no hay Override)")]
    public float radioExtraPerfil = 120f; 
    
    public List<PlayerProfileUI> perfilesActivos = new List<PlayerProfileUI>();

    [Header("Contenedor (Vacío dentro de CoTable)")]
    public Transform contenedorHandAreas;

    [Header("Geometría de tu Mesa")]
    public float radioX = 600f;
    public float radioY = 420f;
    
    [Tooltip("Usa este valor para rotar los perfiles de frente (-50f)")]
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
        //GenerarMesa(numBots+1);
    }

    public void GenerarMesa(int numJugadores)
    {
        // 1. Limpiamos solo los HandAreas y Perfiles
        foreach (Transform child in contenedorHandAreas) Destroy(child.gameObject);
        manosActivas.Clear();
        perfilesActivos.Clear(); 

        if (perspectiveManager != null)
            perspectiveManager.handAreas.Clear();

        // 2. MESA VIRTUAL: Calculamos asientos extra
        int asientosVirtuales = numJugadores;
        if (numJugadores == 5) asientosVirtuales = 7; 
        if (numJugadores == 6) asientosVirtuales = 8; 

        float anguloPaso = 360f / asientosVirtuales;
        
        GameObject manoP1 = null;
        GameObject[] manosGeneradas = new GameObject[numJugadores];

        for (int i = 0; i < numJugadores; i++)
        {
            int indexAsiento = i;
            
            if ((numJugadores >= 5) && i > 0) 
            {
                indexAsiento = i + 1; 
            }

            float anguloGrados = (indexAsiento * anguloPaso) - 90f;
            float anguloRadianes = anguloGrados * Mathf.Deg2Rad;
            float radioX_Usado = radioX;
            float radioY_Usado = radioY;

            if (i != 0) 
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
                handArea.name = $"HandArea_P{i + 1}";
                if (i == 0) manoP1 = handArea;

                RectTransform rtHand = handArea.GetComponent<RectTransform>();
                rtHand.anchoredPosition = new Vector2(posX, posY);

                Vector3 rotacionFinal = Vector3.zero;

                if (numJugadores == 2)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (i == 1) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                }
                else if (numJugadores == 3)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (i == 1) rotacionFinal = new Vector3(45f, -90f, -90f);    
                    if (i == 2) rotacionFinal = new Vector3(135f, -90f, -90f);   
                }
                else if (numJugadores == 4)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (i == 1) rotacionFinal = new Vector3(0f, -90f, -90f);     
                    if (i == 2) rotacionFinal = new Vector3(-45f, 0f, 0f);       
                    if (i == 3) rotacionFinal = new Vector3(0f, 90f, 90f);       
                }
                else if (numJugadores >= 5)
                {
                    if (i == 0) rotacionFinal = new Vector3(-45f, 0f, 0f); 
                    else rotacionFinal = new Vector3(anguloGrados, -90f, -90f); 
                }

                rtHand.localEulerAngles = rotacionFinal;
                manosGeneradas[i] = handArea;
             
                if (perspectiveManager != null) perspectiveManager.handAreas.Add(rtHand);

                CanvasGroup cg = handArea.GetComponentInChildren<CanvasGroup>();
                if (cg != null) manosActivas.Add(cg);
            }

            // --- B) INSTANCIAR PERFIL DEL JUGADOR ---
            if (playerProfilePrefab != null)
            {
                GameObject perfilObj = Instantiate(playerProfilePrefab, contenedorHandAreas);
                perfilObj.name = $"Profile_P{i + 1}";
                
                RectTransform rtPerfil = perfilObj.GetComponent<RectTransform>();
                
                // 1. MATEMÁTICA POR DEFECTO (Se usará solo como base por si acaso)
                float radioX_Perfil = radioX_Usado + radioExtraPerfil;
                float radioY_Perfil = radioY_Usado + radioExtraPerfil;
                float perfilPosX = radioX_Perfil * Mathf.Cos(anguloRadianes);
                float perfilPosY = radioY_Perfil * Mathf.Sin(anguloRadianes);

                // 2. OVERRIDE MANUAL 
                if (i == 0) 
                {
                    // TÚ (P1)
                    perfilPosX = -1000f; 
                    perfilPosY = -500f; 
                    rtPerfil.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                }
                else if (numJugadores == 2)
                {
                    if (i == 1) { perfilPosX = 0f; perfilPosY = 1500f; } // P2
                }
                else if (numJugadores == 3)
                {
                    if (i == 1) { perfilPosX = 1100f; perfilPosY = 1100f; } // P2 
                    else if (i == 2) { perfilPosX = -1100f; perfilPosY = 1100f; } // P3 
                }
                else if (numJugadores == 4)
                {
                    if (i == 1) { perfilPosX = 1300f; perfilPosY = 400f; } // P2
                    else if (i == 2) { perfilPosX = 0f; perfilPosY = 1700f; } // P3
                    else if (i == 3) { perfilPosX = -1300f; perfilPosY = 400f; } // P4
                }
                else if (numJugadores == 5)
                {
                    if (i == 1) { perfilPosX = 1300f; perfilPosY = 600f; } // P2
                    else if (i == 2) { perfilPosX = 550f; perfilPosY = 1600f; } // P3 
                    else if (i == 3) { perfilPosX = -550f; perfilPosY = 1600f; } // P4 
                    else if (i == 4) { perfilPosX = -1300f; perfilPosY = 600f; } // P5
                }
                else if (numJugadores == 6)
                {
                    if (i == 1) { perfilPosX = 1300f; perfilPosY = 500f; } // P2
                    else if (i == 2) { perfilPosX = 1000f; perfilPosY = 1300f; } // P3 
                    else if (i == 3) { perfilPosX = 0f; perfilPosY = 1750f; } // P4 
                    else if (i == 4) { perfilPosX = -1000f; perfilPosY = 1300f; } // P5 
                    else if (i == 5) { perfilPosX = -1300f; perfilPosY = 500f; } // P6
                }
                
                rtPerfil.anchoredPosition = new Vector2(perfilPosX, perfilPosY);
                rtPerfil.localEulerAngles = new Vector3(rotacionX_TodaLaMesa, 0f, 0f); 

                PlayerProfileUI profileUI = perfilObj.GetComponent<PlayerProfileUI>();
                if (profileUI != null)
                {
                    perfilesActivos.Add(profileUI);
                    string nombre = (i == 0) ? "TÚ" : $"BOT {i}";
                    profileUI.ActualizarPerfil(nombre, 3, 0, -1);
                }
            }
        }

        // --- ORDEN DE DIBUJADO---
        if (numJugadores == 5)
        {
            manosGeneradas[2].transform.SetSiblingIndex(0); 
            manosGeneradas[3].transform.SetSiblingIndex(1); 
            manosGeneradas[1].transform.SetSiblingIndex(2); 
            manosGeneradas[4].transform.SetSiblingIndex(3); 
            manosGeneradas[0].transform.SetSiblingIndex(4); 
        }
        else if (numJugadores == 6)
        {
            manosGeneradas[3].transform.SetSiblingIndex(0); 
            manosGeneradas[2].transform.SetSiblingIndex(1); 
            manosGeneradas[4].transform.SetSiblingIndex(2); 
            manosGeneradas[1].transform.SetSiblingIndex(3); 
            manosGeneradas[5].transform.SetSiblingIndex(4); 
            manosGeneradas[0].transform.SetSiblingIndex(5); 
        }
        else
        {
            if (manosGeneradas.Length > 0 && manosGeneradas[0] != null)
            {
                manosGeneradas[0].transform.SetAsLastSibling();
            }
        }
        
        // C) Los perfiles siempre deben dibujarse POR ENCIMA de todo
        foreach(PlayerProfileUI perfil in perfilesActivos)
        {
            perfil.transform.SetAsLastSibling();
        }
    }
}