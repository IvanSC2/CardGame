using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// WELCOME PANEL — Panel modal que aparece UNA SOLA VEZ para registrar el nombre
//
// CONFIGURACIÓN EN UNITY:
// 1. Crear un Panel (UI) como hijo del Canvas principal del menú
// 2. Ponerlo con un fondo semitransparente negro (bloqueante)
// 3. Asignar las referencias del Inspector: inputNickname, btnConfirm, etc.
// 4. Dejar el GameObject DESACTIVADO en la escena
// 5. MenuManager lo activará si PlayerPrefs no tiene "Nickname"
// =============================================================================

public class WelcomePanel : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Campo de texto donde el jugador escribe su nombre.")]
    public TMP_InputField inputNickname;

    [Tooltip("Botón para confirmar el nombre.")]
    public Button btnConfirm;

    [Tooltip("Texto de error/validación debajo del input.")]
    public TMP_Text txtFeedback;

    [Tooltip("Panel raíz del overlay (se desactiva al confirmar).")]
    public GameObject panelRoot;

    [Header("Configuración")]
    [Tooltip("Mínimo de caracteres para el nombre.")]
    public int minLength = 3;

    [Tooltip("Máximo de caracteres para el nombre.")]
    public int maxLength = 16;

    [Header("Animación")]
    [Tooltip("CanvasGroup del panel para hacer fade-in/out.")]
    public CanvasGroup canvasGroup;

    private bool isProcessing = false;
    private Coroutine fadeCoroutine;

    private void OnEnable()
    {
        // Configurar el InputField
        if (inputNickname != null)
        {
            inputNickname.characterLimit = maxLength;
            inputNickname.text = "";
            inputNickname.onValueChanged.AddListener(OnInputChanged);
        }

        // Configurar el botón
        if (btnConfirm != null)
        {
            btnConfirm.onClick.RemoveAllListeners();
            btnConfirm.onClick.AddListener(OnConfirmClicked);
            btnConfirm.interactable = false;
        }

        if (txtFeedback != null) txtFeedback.text = "";

        // Fade-in suave
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            StartFade(1f, 0.4f);
        }
    }

    private void OnDisable()
    {
        if (inputNickname != null)
            inputNickname.onValueChanged.RemoveListener(OnInputChanged);
    }

    // =====================================================================
    // VALIDACIÓN EN TIEMPO REAL
    // =====================================================================
    private void OnInputChanged(string text)
    {
        string trimmed = text.Trim();
        string error = ValidarNombre(trimmed);

        if (txtFeedback != null)
        {
            txtFeedback.text = error;
            txtFeedback.color = string.IsNullOrEmpty(error) ? Color.green : Color.red;
            if (string.IsNullOrEmpty(error) && trimmed.Length >= minLength)
                txtFeedback.text = "✓ Nombre válido";
        }

        if (btnConfirm != null)
            btnConfirm.interactable = string.IsNullOrEmpty(error) && trimmed.Length >= minLength;
    }

    private string ValidarNombre(string nombre)
    {
        if (nombre.Length < minLength)
            return $"Mínimo {minLength} caracteres";
        if (nombre.Length > maxLength)
            return $"Máximo {maxLength} caracteres";

        // Solo letras, números, guiones y guiones bajos
        foreach (char c in nombre)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ' ')
                return "Solo letras, números, espacios, - y _";
        }

        // Sin espacios al inicio/final (el trim ya lo maneja, pero avisamos)
        if (nombre != nombre.Trim())
            return "Sin espacios al inicio o final";

        return null; // null = válido
    }

    // =====================================================================
    // CONFIRMACIÓN
    // =====================================================================
    private async void OnConfirmClicked()
    {
        if (isProcessing) return;
        isProcessing = true;

        string nombre = inputNickname.text.Trim();

        // Validación final por seguridad
        string error = ValidarNombre(nombre);
        if (!string.IsNullOrEmpty(error))
        {
            if (txtFeedback != null) txtFeedback.text = error;
            isProcessing = false;
            return;
        }

        // Feedback visual mientras guardamos
        if (btnConfirm != null) btnConfirm.interactable = false;
        if (txtFeedback != null)
        {
            txtFeedback.text = "Guardando...";
            txtFeedback.color = Color.yellow;
        }

        // Guardar en ProfileManager → CloudSave + PlayerPrefs
        if (ProfileManager.Instance != null)
        {
            await ProfileManager.Instance.EstablecerNickname(nombre);
        }
        else
        {
            // Fallback por si ProfileManager no existe (no debería pasar)
            PlayerPrefs.SetString("Nickname", nombre);
            PlayerPrefs.Save();
        }

        Debug.Log($"[BIENVENIDA] Nombre registrado: \"{nombre}\"");

        // Fade-out y cerrar
        StartFade(0f, 0.3f, () =>
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        });

        isProcessing = false;
    }

    private void StartFade(float targetAlpha, float duration, System.Action onComplete = null)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        if (canvasGroup != null)
        {
            fadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(targetAlpha, duration, onComplete));
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    private System.Collections.IEnumerator FadeCanvasGroupRoutine(float targetAlpha, float duration, System.Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
        onComplete?.Invoke();
    }
}
