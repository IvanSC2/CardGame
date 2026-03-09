using UnityEngine;
using UnityEngine.InputSystem; // <--- NUEVO: Importamos el nuevo Input System

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("UI References")]
    public GameObject pausePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void Update()
    {
        // <--- NUEVO: Detectar la tecla Escape usando el Input System moderno
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        bool currentState = InteractionManager.Instance.isPaused;
        SetPauseState(!currentState);
    }

    public void ResumeGame()
    {
        SetPauseState(false);
    }

    public void RestartGame()
    {
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
        Debug.Log("Saliendo del juego...");
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private void SetPauseState(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        InteractionManager.Instance.isPaused = isPaused;
        InteractionManager.Instance.UpdateVisualStates();
    }
}