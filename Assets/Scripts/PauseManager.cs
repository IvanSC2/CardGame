using UnityEngine;
using UnityEngine.InputSystem; // <--- NUEVO: Importamos el nuevo Input System
using UnityEngine.SceneManagement;
using TMPro; // Necesario para cambiar el texto del título
using UnityEngine.UI; // Necesario para desactivar botones

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("UI References")]
    public GameObject pausePanel;
    public TMP_Text titleText;       // NUEVO: El texto que dice "PAUSA"
    public GameObject resumeButton;  // NUEVO: El botón de "Reanudar
    [Header("Control de Estados")]
    public bool isGameOver = false;  
    public TMP_Text statsText;       // NUEVO: Arrastra aquí el texto de en medio

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {

        // Anulamos la tecla de esc si Se ejecuta el panel de GameOver
        if (isGameOver) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {   
        if (isGameOver) return;
        bool currentState = InteractionManager.Instance.isPaused;
        SetPauseState(!currentState);
    }

    public void ResumeGame()
    {
        if (isGameOver) return;
        SetPauseState(false);
    }

    public void TriggerGameOver(string ganador)
    {
        isGameOver = true; // Echamos el candado al sistema
        SetPauseState(true);
        // Mutamos la Interfaz
        if (titleText != null) 
            titleText.text = $"GAME OVER";
            
        if (resumeButton != null) 
            resumeButton.SetActive(false); // Escondemos el botón de seguir jugando

        // =======================================================
        // CONSTRUCCIÓN DE COLUMNAS DE ESTADÍSTICAS
        // =======================================================
        if (statsText != null)
        {
            statsText.gameObject.SetActive(true); // Lo encendemos

            var im = InteractionManager.Instance;
            int rondas = im.rondasJugadasTotales;

            
            string stats = 
                "<b><color=#5A9BD5>JUGADOR 1</color></b> <pos=50%><b><color=#ED7D31>JUGADOR 2 (IA)</color></b>\n\n" +
                $"Precisión: {im.p1ApuestasAcertadas} / {rondas} <pos=50%>Precisión: {im.p2ApuestasAcertadas} / {rondas}\n" +
                $"Bazas Totales: {im.p1BazasTotales} <pos=50%>Bazas Totales: {im.p2BazasTotales}\n" +
                $"Vidas Restantes: {im.p1Vidas} <pos=50%>Vidas Restantes: {im.p2Vidas}";

            statsText.text = stats;
        }    
    }
    public void RestartGame()
    {
        Time.timeScale = 1f; 
        isGameOver = false;
        SetPauseState(false);
        if (CardDatabase.deck != null)
        {
            CardDatabase.deck.Clear();
        }
        InteractionManager.Instance.StartNewGame();

        if (TableZone.Instance != null)
        {
            TableZone.Instance.ResetStats();
        }
    }

    public void QuitGame()
    {   
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void SetPauseState(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        InteractionManager.Instance.isPaused = isPaused;
        InteractionManager.Instance.UpdateVisualStates();
        Time.timeScale = isPaused ? 0f : 1f;
    }
}