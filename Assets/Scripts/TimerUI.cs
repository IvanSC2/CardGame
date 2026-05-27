using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class TimerUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timerText;
    public Image timerFillBar;
    
    [Header("Settings")]
    public Color normalColor = Color.green;
    public Color warningColor = Color.red;
    public float warningThreshold = 5f;

    private void Update()
    {
        // Solo mostrar timer si el juego está activo y hay conexión
        if (InteractionManager.Instance == null || !NetworkManager.Singleton.IsConnectedClient || !GameConfig.gameStarted)
        {
            if (timerText != null) timerText.text = "";
            if (timerFillBar != null) timerFillBar.fillAmount = 0;
            return;
        }

        float endTime = InteractionManager.Instance.turnEndTime.Value;
        
        // Si el servidor pone el tiempo a 0, significa que el reloj está pausado
        if (endTime <= 0f)
        {
            if (timerText != null) timerText.text = "";
            if (timerFillBar != null) timerFillBar.fillAmount = 1f;
            return;
        }

        // Calculamos el tiempo restante localmente usando la hora sincronizada del servidor
        float timeRemaining = endTime - (float)NetworkManager.Singleton.ServerTime.Time;
        
        if (timeRemaining < 0) timeRemaining = 0;

        // Actualizar UI
        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
            timerText.color = timeRemaining <= warningThreshold ? warningColor : normalColor;
        }

        if (timerFillBar != null)
        {
            // Asumimos un turno de 15 segundos por defecto para el llenado de la barra, 
            // aunque idealmente leeríamos el GameConfig.turnTime
            float maxTime = GameConfig.turnTime > 0 ? GameConfig.turnTime : 15f;
            timerFillBar.fillAmount = timeRemaining / maxTime;
            timerFillBar.color = timeRemaining <= warningThreshold ? warningColor : normalColor;
        }
    }
}
