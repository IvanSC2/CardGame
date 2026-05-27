using UnityEngine;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject pLoadingScreen;
    public TextMeshProUGUI txtLoadingMessage;
    public Transform spinnerTransform;

    [Header("Settings")]
    public float spinSpeed = -200f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Independizar el LoadingScreen para que no se destruya al cambiar de escena
            if (pLoadingScreen != null && pLoadingScreen.transform.parent != this.transform)
            {
                Canvas c = gameObject.GetComponent<Canvas>();
                if (c == null)
                {
                    c = gameObject.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = 999; // Por encima de todo
                    gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                    gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                
                pLoadingScreen.transform.SetParent(this.transform, false);
            }
        }
        else
        {
            // Si ya existe un manager, destruimos el panel de esta escena para evitar duplicados
            if (this.pLoadingScreen != null)
            {
                Destroy(this.pLoadingScreen.gameObject);
            }
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Aseguramos que empiece apagado salvo que alguien ya lo haya encendido
        if (pLoadingScreen != null && !pLoadingScreen.activeSelf)
        {
            OcultarCargando();
        }
    }

    private void Update()
    {
        if (pLoadingScreen != null && pLoadingScreen.activeSelf && spinnerTransform != null)
        {
            spinnerTransform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        }
    }

    public void MostrarCargando(string mensaje = "Cargando...")
    {
        if (pLoadingScreen == null) return;
        
        if (txtLoadingMessage != null)
            txtLoadingMessage.text = mensaje;
            
        pLoadingScreen.SetActive(true);
        Debug.Log($"[LOADING] Mostrando pantalla de carga: {mensaje}");
    }

    public void OcultarCargando()
    {
        if (pLoadingScreen == null) return;
        
        pLoadingScreen.SetActive(false);
        Debug.Log("[LOADING] Ocultando pantalla de carga.");
    }
}
