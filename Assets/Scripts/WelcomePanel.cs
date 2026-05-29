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

    [Header("UI Recuperación de Cuenta")]
    [Tooltip("Contenedor visual del campo de Nombre (para ocultarlo en Login).")]
    public GameObject containerGuest;
    [Tooltip("Contenedor visual de los campos Email y Password (para ocultarlos en Invitado).")]
    public GameObject containerLogin;
    [Tooltip("Campo de texto para el Email de recuperación.")]
    public TMP_InputField inputEmail;
    [Tooltip("Campo de texto para la contraseña de recuperación.")]
    public TMP_InputField inputPassword;
    [Tooltip("Botón para alternar entre Crear Invitado y Recuperar Cuenta.")]
    public Button btnToggleMode;
    [Tooltip("Texto del botón toggle.")]
    public TMP_Text txtToggleMode;
    [Tooltip("Texto del botón de confirmación.")]
    public TMP_Text txtConfirmBtn;

    private bool isLoginMode = false;

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

        // Configurar Toggle Mode
        if (btnToggleMode != null)
        {
            btnToggleMode.onClick.RemoveAllListeners();
            btnToggleMode.onClick.AddListener(ToggleMode);
        }

        // Suscribirse a cambios en los inputs de login para validar el botón
        if (inputEmail != null) inputEmail.onValueChanged.AddListener((_) => ValidarFormulario());
        if (inputPassword != null) inputPassword.onValueChanged.AddListener((_) => ValidarFormulario());

        // Asegurarnos de que arrancamos en modo Invitado
        isLoginMode = false;
        ActualizarVistaModo();

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
    private void ValidarFormulario()
    {
        if (isLoginMode)
        {
            // Unity UGS usa Username, no Email, por lo que quitamos la obligación del '@'
            bool hasUsername = inputEmail != null && inputEmail.text.Trim().Length >= 3;
            bool hasPassword = inputPassword != null && inputPassword.text.Length >= 8;
            if (btnConfirm != null) btnConfirm.interactable = hasUsername && hasPassword;
            if (txtFeedback != null) txtFeedback.text = ""; // Limpiamos feedback en modo login
        }
        else
        {
            string text = inputNickname != null ? inputNickname.text : "";
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
    }

    private void OnInputChanged(string text)
    {
        if (!isLoginMode) ValidarFormulario();
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
        AudioManager.Instance?.PlayButtonAction();
        if (isProcessing) return;
        isProcessing = true;
        
        if (btnConfirm != null) btnConfirm.interactable = false;
        
        if (isLoginMode)
        {
            string email = inputEmail.text.Trim();
            string password = inputPassword.text;

            if (txtFeedback != null)
            {
                txtFeedback.text = "Recuperando cuenta...";
                txtFeedback.color = Color.yellow;
            }

            if (ProfileManager.Instance != null)
            {
                string error = await ProfileManager.Instance.LoginCuenta(email, password);
                if (error == null)
                {
                    Debug.Log($"[BIENVENIDA] Cuenta recuperada con éxito");
                    StartFade(0f, 0.3f, () => { if (panelRoot != null) panelRoot.SetActive(false); });
                }
                else
                {
                    if (txtFeedback != null) { txtFeedback.text = "Fallo: " + error; txtFeedback.color = Color.red; }
                }
            }
        }
        else
        {
            string nombre = inputNickname.text.Trim();

            // Validación final por seguridad
            string error = ValidarNombre(nombre);
            if (!string.IsNullOrEmpty(error))
            {
                if (txtFeedback != null) txtFeedback.text = error;
                isProcessing = false;
                if (btnConfirm != null) btnConfirm.interactable = true;
                return;
            }

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
        }

        isProcessing = false;
    }

    private void ToggleMode()
    {
        AudioManager.Instance?.PlayButtonGeneric();
        isLoginMode = !isLoginMode;
        ActualizarVistaModo();
    }

    private void ActualizarVistaModo()
    {
        if (containerGuest != null) containerGuest.SetActive(!isLoginMode);
        if (containerLogin != null) containerLogin.SetActive(isLoginMode);

        if (txtToggleMode != null)
            txtToggleMode.text = isLoginMode ? "Jugar como\nInvitado" : "Ya tengo\ncuenta";
            
        if (txtConfirmBtn != null)
            txtConfirmBtn.text = isLoginMode ? "Iniciar\nSesion" : "Comenzar";

        if (txtFeedback != null) txtFeedback.text = "";

        // Revalidar el formulario según el modo activo
        ValidarFormulario();
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
