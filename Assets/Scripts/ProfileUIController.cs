using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;



public class ProfileUIController : MonoBehaviour
{
    // ─── PANELES PRINCIPALES ───────────────────────────────────────────────
    [Header("Paneles Raíz")]
    [Tooltip("Panel de estadísticas (siempre visible al abrir perfil).")]
    public GameObject backStatsPanel;

    [Tooltip("Panel de edición de perfil (se alterna con bEditProfile).")]
    public GameObject backProfilePanel;

    // ─── HEADER DEL PERFIL ─────────────────────────────────────────────────
    [Header("Header del Perfil")]
    [Tooltip("Imagen del avatar del jugador.")]
    public Image imgAvatar;

    [Tooltip("Texto con el nombre del jugador.")]
    public TMP_Text txtPlayerName;

    [Tooltip("Texto con el estado de la cuenta (Invitado / Registrado).")]
    public TMP_Text txtAccountStatus;

    // ─── BOTONES DE NAVEGACIÓN ─────────────────────────────────────────────
    [Header("Botones de Navegación")]
    [Tooltip("Alterna la visibilidad del panel de edición.")]
    public Button bEditProfile;

    [Tooltip("Muestra el contenedor de estadísticas.")]
    public Button bStatistics;

    [Tooltip("Muestra el panel de historial de partidas.")]
    public Button bGameHistory;

    // ─── BOTONES DE EDICIÓN ────────────────────────────────────────────────
    [Header("Botones de Edición (dentro de backProfilePanel)")]
    public Button bChangeName;
    public Button bChangeAvatar;
    public Button bSyncAccount;
    public Button bDeleteAccount;

    // ─── EDICIÓN DE NOMBRE ─────────────────────────────────────────────────
    [Header("Sub-panel de Cambio de Nombre")]
    [Tooltip("Panel que contiene el InputField y botón confirmar nombre.")]
    public GameObject panelChangeName;
    public TMP_InputField inputNewName;
    public Button btnConfirmName;
    public Button btnCancelName;
    public TMP_Text txtNameFeedback;

    // ─── VINCULACIÓN DE CUENTA ─────────────────────────────────────────────
    [Header("Sub-panel de Vinculación de Cuenta")]
    [Tooltip("Panel que contiene los campos de Email y Password.")]
    public GameObject panelSyncAccount;
    public TMP_InputField inputEmail;
    public TMP_InputField inputPassword;
    public Button btnConfirmSync;
    public Button btnCancelSync;
    public TMP_Text txtSyncFeedback;

    // ─── BORRADO DE CUENTA ─────────────────────────────────────────────────
    [Header("Sub-panel de Borrado de Cuenta")]
    public GameObject panelDeleteAccount;
    public Button btnConfirmDelete;
    public Button btnCancelDelete;
    public TMP_Text txtDeleteFeedback;

    // ─── ESTADÍSTICAS ──────────────────────────────────────────────────────
    [Header("Contenedor de Estadísticas")]
    [Tooltip("Panel que contiene la lista vertical de KPIs.")]
    public GameObject statsContainer;

    [Tooltip("Texto donde se renderizan las estadísticas del filtro activo.")]
    public TMP_Text txtStatsContent;

    [Header("Filtros de Estadísticas (dentro de backStatsPanel)")]
    public Button btnFilterAll;
    public Button btnFilterPrivate;
    public Button btnFilterPublic;
    public Button btnFilterPractice;

    // ─── HISTORIAL ─────────────────────────────────────────────────────────
    [Header("Historial de Partidas")]
    [Tooltip("Panel que contiene el ScrollRect del historial.")]
    public GameObject gameHistoryPanel;

    [Tooltip("Transform Content del ScrollRect donde se instancian las filas.")]
    public Transform historyContentParent;

    [Tooltip("Prefab de la fila del historial (debe tener MatchRecordRowUI).")]
    public GameObject matchRecordRowPrefab;

    // ─── SPRITE POR DEFECTO ────────────────────────────────────────────────
    [Header("Recursos")]
    [Tooltip("Avatar por defecto cuando no se ha elegido foto.")]
    public Sprite defaultAvatarSprite;

    // ─── ESTADO INTERNO ────────────────────────────────────────────────────
    private string filtroActivo = "all";

    // =====================================================================
    // INICIALIZACIÓN
    // =====================================================================
    private void OnEnable()
    {
        // Al encender pProfile, mostramos el botón bEditProfile automáticamente
        if (bEditProfile != null) bEditProfile.gameObject.SetActive(true);

        ConfigurarBotones();
        CargarDatosDelPerfil();
        MostrarEstadisticas(); // Por defecto, mostrar stats
    }

    private void OnDisable()
    {
        // Al apagar pProfile, ocultamos los sub-paneles automáticamente
        if (bEditProfile != null) bEditProfile.gameObject.SetActive(false);
        if (backProfilePanel != null) backProfilePanel.SetActive(false);
        if (panelChangeName != null) panelChangeName.SetActive(false);
        if (panelSyncAccount != null) panelSyncAccount.SetActive(false);
    }

    private void ConfigurarBotones()
    {
        // Navegación principal
        if (bEditProfile != null)
        {
            bEditProfile.onClick.RemoveAllListeners();
            bEditProfile.onClick.AddListener(ToggleEditPanel);
        }
        if (bStatistics != null)
        {
            bStatistics.onClick.RemoveAllListeners();
            bStatistics.onClick.AddListener(MostrarEstadisticas);
        }
        if (bGameHistory != null)
        {
            bGameHistory.onClick.RemoveAllListeners();
            bGameHistory.onClick.AddListener(MostrarHistorial);
        }

        // Edición
        if (bChangeName != null)
        {
            bChangeName.onClick.RemoveAllListeners();
            bChangeName.onClick.AddListener(AbrirCambioNombre);
        }
        if (bChangeAvatar != null)
        {
            bChangeAvatar.onClick.RemoveAllListeners();
            bChangeAvatar.onClick.AddListener(AbrirSelectorAvatar);
        }
        if (bSyncAccount != null)
        {
            bSyncAccount.onClick.RemoveAllListeners();
            bSyncAccount.onClick.AddListener(AbrirVinculacion);
        }
        if (bDeleteAccount != null)
        {
            bDeleteAccount.onClick.RemoveAllListeners();
            bDeleteAccount.onClick.AddListener(AbrirBorrado);
        }

        // Confirmar nombre
        if (btnConfirmName != null)
        {
            btnConfirmName.onClick.RemoveAllListeners();
            btnConfirmName.onClick.AddListener(ConfirmarCambioNombre);
        }

        // Confirmar vinculación
        if (btnConfirmSync != null)
        {
            btnConfirmSync.onClick.RemoveAllListeners();
            btnConfirmSync.onClick.AddListener(ConfirmarVinculacion);
        }

        // Confirmar borrado
        if (btnConfirmDelete != null)
        {
            btnConfirmDelete.onClick.RemoveAllListeners();
            btnConfirmDelete.onClick.AddListener(ConfirmarBorradoReal);
        }

        // Cancelar edición o vinculación o borrado
        if (btnCancelName != null)
        {
            btnCancelName.onClick.RemoveAllListeners();
            btnCancelName.onClick.AddListener(() => { if (panelChangeName != null) panelChangeName.SetActive(false); });
        }
        if (btnCancelSync != null)
        {
            btnCancelSync.onClick.RemoveAllListeners();
            btnCancelSync.onClick.AddListener(() => { if (panelSyncAccount != null) panelSyncAccount.SetActive(false); });
        }
        if (btnCancelDelete != null)
        {
            btnCancelDelete.onClick.RemoveAllListeners();
            btnCancelDelete.onClick.AddListener(() => { if (panelDeleteAccount != null) panelDeleteAccount.SetActive(false); });
        }

        // Filtros de estadísticas
        if (btnFilterAll != null)
        {
            btnFilterAll.onClick.RemoveAllListeners();
            btnFilterAll.onClick.AddListener(() => AplicarFiltro("all"));
        }
        if (btnFilterPrivate != null)
        {
            btnFilterPrivate.onClick.RemoveAllListeners();
            btnFilterPrivate.onClick.AddListener(() => AplicarFiltro("private"));
        }
        if (btnFilterPublic != null)
        {
            btnFilterPublic.onClick.RemoveAllListeners();
            btnFilterPublic.onClick.AddListener(() => AplicarFiltro("public"));
        }
        if (btnFilterPractice != null)
        {
            btnFilterPractice.onClick.RemoveAllListeners();
            btnFilterPractice.onClick.AddListener(() => AplicarFiltro("practice"));
        }
    }

    // =====================================================================
    // CARGA DE DATOS DEL PERFIL
    // =====================================================================
    private void CargarDatosDelPerfil()
    {
        if (ProfileManager.Instance == null || !ProfileManager.Instance.IsLoaded) return;

        var profile = ProfileManager.Instance.Profile;

        // Nombre
        if (txtPlayerName != null)
            txtPlayerName.text = ProfileManager.Instance.GetDisplayName();

        // Estado de la cuenta
        if (txtAccountStatus != null)
        {
            txtAccountStatus.text = profile.isLinked
                ? "<color=green>✓ Cuenta Vinculada</color>"
                : "<color=yellow>⚠ Cuenta de Invitado</color>";
        }

        // Avatar
        CargarAvatar(profile);

        // Bloquear edición de nombre si la cuenta está vinculada
        if (bChangeName != null)
            bChangeName.interactable = !profile.isLinked;

        // Esconder "Vincular" si ya está vinculada
        if (bSyncAccount != null)
            bSyncAccount.gameObject.SetActive(!profile.isLinked);

        // Cerrar sub-paneles
        if (panelChangeName != null) panelChangeName.SetActive(false);
        if (panelSyncAccount != null) panelSyncAccount.SetActive(false);
        if (backProfilePanel != null) backProfilePanel.SetActive(false);
    }

    private void CargarAvatar(PlayerProfile profile)
    {
        if (imgAvatar == null) return;

        // Si tiene avatar personalizado (foto de galería)
        if (profile.avatarId == -1 && !string.IsNullOrEmpty(profile.customAvatarPath))
        {
            if (File.Exists(profile.customAvatarPath))
            {
                byte[] bytes = File.ReadAllBytes(profile.customAvatarPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    imgAvatar.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    return;
                }
            }
        }

        // Fallback: avatar por defecto
        if (defaultAvatarSprite != null)
            imgAvatar.sprite = defaultAvatarSprite;
    }

    // =====================================================================
    // NAVEGACIÓN ENTRE SECCIONES
    // =====================================================================
    private void ToggleEditPanel()
    {
        if (backProfilePanel != null)
            backProfilePanel.SetActive(!backProfilePanel.activeSelf);
    }

    private void MostrarEstadisticas()
    {
        if (statsContainer != null) statsContainer.SetActive(true);
        if (gameHistoryPanel != null) gameHistoryPanel.SetActive(false);
        AplicarFiltro(filtroActivo);
    }

    private void MostrarHistorial()
    {
        if (statsContainer != null) statsContainer.SetActive(false);
        if (gameHistoryPanel != null) gameHistoryPanel.SetActive(true);
        PoblarHistorial();
    }

    // =====================================================================
    // ESTADÍSTICAS CON FILTROS
    // =====================================================================
    private void AplicarFiltro(string modo)
    {
        filtroActivo = modo;
        if (ProfileManager.Instance == null || txtStatsContent == null) return;

        var stats = ProfileManager.Instance.Stats;
        string contenido = "";

        if (modo == "all")
        {
            int totalPlayed = stats.practice.gamesPlayed + stats.privateMatch.gamesPlayed + stats.publicMatch.gamesPlayed;
            int totalWon = stats.practice.gamesWon + stats.privateMatch.gamesWon + stats.publicMatch.gamesWon;
            float winRate = totalPlayed > 0 ? (totalWon * 100f / totalPlayed) : 0f;

            contenido = $"<b>── TODAS LAS PARTIDAS ──</b>\n\n" +
                        $"Partidas Jugadas:   <b>{totalPlayed}</b>\n" +
                        $"Partidas Ganadas:   <b>{totalWon}</b>\n" +
                        $"Porcentaje Victorias:   <b>{winRate:F1}%</b>";
        }
        else
        {
            ModeStats ms = stats.GetByMode(modo);
            string titulo = modo switch
            {
                "practice" => "PRÁCTICA",
                "private"  => "PRIVADAS",
                "public"   => "MATCHMAKING",
                _          => modo.ToUpper()
            };

            contenido = $"<b>── {titulo} ──</b>\n\n" +
                        $"Partidas Jugadas:   <b>{ms.gamesPlayed}</b>\n" +
                        $"Partidas Ganadas:   <b>{ms.gamesWon}</b>\n" +
                        $"Porcentaje Victorias:   <b>{ms.WinPercentage:F1}%</b>\n";

            // KPIs específicos por modo
            if (modo == "practice")
            {
                string diffLabel = ms.hardestWinDifficulty switch
                {
                    0 => "Ninguna",
                    1 => "Fácil",
                    2 => "Normal",
                    3 => "Difícil",
                    _ => $"Nivel {ms.hardestWinDifficulty}"
                };
                contenido += $"Victoria Más Difícil:   <b>{diffLabel}</b>";
            }
            else if (modo == "public")
            {
                contenido += $"Racha Máxima Victorias:   <b>{ms.highestWinRow}</b>";
            }
            else if (modo == "private")
            {
                contenido += $"Dinero Total Ganado:   <b>{ms.totalMoneyEarned} $</b>";
            }
        }

        txtStatsContent.text = contenido;
    }

    // =====================================================================
    // HISTORIAL DE PARTIDAS
    // =====================================================================
    private void PoblarHistorial()
    {
        if (historyContentParent == null || matchRecordRowPrefab == null)
        {
            Debug.LogWarning($"[HISTORIAL] No se puede poblar: historyContentParent={historyContentParent}, matchRecordRowPrefab={matchRecordRowPrefab}");
            return;
        }
        if (ProfileManager.Instance == null)
        {
            Debug.LogWarning("[HISTORIAL] ProfileManager.Instance es null");
            return;
        }
        if (!ProfileManager.Instance.IsLoaded)
        {
            Debug.LogWarning("[HISTORIAL] ProfileManager aún no ha terminado de cargar (IsLoaded=false)");
            return;
        }
        Debug.Log($"[HISTORIAL] Poblando historial: {ProfileManager.Instance.History.matches.Count} partidas encontradas");

        // Limpiar filas anteriores
        foreach (Transform child in historyContentParent)
            Destroy(child.gameObject);

        var matches = ProfileManager.Instance.History.matches;

        if (matches.Count == 0)
        {
            // Si no hay partidas, mostramos un mensaje
            GameObject emptyRow = Instantiate(matchRecordRowPrefab, historyContentParent);
            var row = emptyRow.GetComponent<MatchRecordRowUI>();
            if (row != null && row.txtDate != null)
                row.txtDate.text = "No hay partidas registradas";
            return;
        }

        foreach (var record in matches)
        {
            GameObject rowGO = Instantiate(matchRecordRowPrefab, historyContentParent);
            var rowUI = rowGO.GetComponent<MatchRecordRowUI>();
            if (rowUI != null)
                rowUI.Configurar(record);
        }
    }

    // =====================================================================
    // CAMBIO DE NOMBRE
    // =====================================================================
    private void AbrirCambioNombre()
    {
        if (panelChangeName == null) return;
        panelChangeName.SetActive(!panelChangeName.activeSelf);
        if (panelSyncAccount != null) panelSyncAccount.SetActive(false);

        if (inputNewName != null)
            inputNewName.text = ProfileManager.Instance?.GetDisplayName() ?? "";
        if (txtNameFeedback != null) txtNameFeedback.text = "";
    }

    private async void ConfirmarCambioNombre()
    {
        if (ProfileManager.Instance == null || inputNewName == null) return;

        string nuevoNombre = inputNewName.text.Trim();
        if (nuevoNombre.Length < 3)
        {
            if (txtNameFeedback != null)
            {
                txtNameFeedback.text = "Mínimo 3 caracteres";
                txtNameFeedback.color = Color.red;
            }
            return;
        }
        if (nuevoNombre.Length > 16)
        {
            if (txtNameFeedback != null)
            {
                txtNameFeedback.text = "Máximo 16 caracteres";
                txtNameFeedback.color = Color.red;
            }
            return;
        }

        if (txtNameFeedback != null)
        {
            txtNameFeedback.text = "Guardando...";
            txtNameFeedback.color = Color.yellow;
        }

        await ProfileManager.Instance.EstablecerNickname(nuevoNombre);

        if (txtPlayerName != null)
            txtPlayerName.text = nuevoNombre;
        if (txtNameFeedback != null)
        {
            txtNameFeedback.text = "✓ Nombre actualizado";
            txtNameFeedback.color = Color.green;
        }

        if (panelChangeName != null) panelChangeName.SetActive(false);
    }

    // =====================================================================
    // SELECTOR DE AVATAR (GALERÍA DEL DISPOSITIVO)
    // =====================================================================
    private void AbrirSelectorAvatar()
    {
        // En Editor: usamos un diálogo nativo de archivo
        // En Mobile: necesitarías NativeGallery (plugin externo) 
        // Por ahora usamos una implementación que funciona en el editor

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "Seleccionar Avatar", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
        {
            ProcesarImagenAvatar(path);
        }
#else
        // En builds reales, copiaríamos a persistentDataPath y cargaríamos
        // Para que funcione en móvil, integra NativeGallery:
        // NativeGallery.GetImageFromGallery((path) => { ProcesarImagenAvatar(path); });
        Debug.Log("[PERFIL] Selector de galería no disponible en esta plataforma. Integra NativeGallery.");
#endif
    }

    private async void ProcesarImagenAvatar(string rutaOriginal)
    {
        if (string.IsNullOrEmpty(rutaOriginal)) return;

        // Copiamos la imagen a persistentDataPath para que sobreviva
        string destino = Path.Combine(Application.persistentDataPath, "avatar.png");

        try
        {
            File.Copy(rutaOriginal, destino, true);

            // Cargar y mostrar
            byte[] bytes = File.ReadAllBytes(destino);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                if (imgAvatar != null)
                {
                    imgAvatar.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                }
            }

            // Guardar la ruta en CloudSave
            if (ProfileManager.Instance != null)
                await ProfileManager.Instance.CambiarAvatarPersonalizado(destino);

            Debug.Log($"[PERFIL] Avatar actualizado desde: {rutaOriginal}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al procesar avatar: {e.Message}");
        }
    }

    // =====================================================================
    // VINCULACIÓN DE CUENTA (Email/Password)
    // =====================================================================
    private void AbrirVinculacion()
    {
        if (panelSyncAccount == null) return;
        panelSyncAccount.SetActive(!panelSyncAccount.activeSelf);
        if (panelChangeName != null) panelChangeName.SetActive(false);
        if (panelDeleteAccount != null) panelDeleteAccount.SetActive(false);

        if (inputEmail != null) inputEmail.text = "";
        if (inputPassword != null) inputPassword.text = "";
        if (txtSyncFeedback != null) txtSyncFeedback.text = "";
    }

    private async void ConfirmarVinculacion()
    {
        if (ProfileManager.Instance == null) return;

        string username = inputEmail != null ? inputEmail.text.Trim() : "";
        string password = inputPassword != null ? inputPassword.text : "";

        // Validación básica
        if (username.Length < 3 || username.Length > 20)
        {
            if (txtSyncFeedback != null)
            {
                txtSyncFeedback.text = "El usuario debe tener entre 3 y 20 caracteres";
                txtSyncFeedback.color = Color.red;
            }
            return;
        }
        if (password.Length < 8)
        {
            if (txtSyncFeedback != null)
            {
                txtSyncFeedback.text = "La contraseña debe tener al menos 8 caracteres";
                txtSyncFeedback.color = Color.red;
            }
            return;
        }

        if (txtSyncFeedback != null)
        {
            txtSyncFeedback.text = "Vinculando cuenta...";
            txtSyncFeedback.color = Color.yellow;
        }
        if (btnConfirmSync != null) btnConfirmSync.interactable = false;

        string error = await ProfileManager.Instance.VincularCuenta(username, password);

        if (error == null) // null significa éxito
        {
            if (txtSyncFeedback != null)
            {
                txtSyncFeedback.text = "✓ ¡Cuenta vinculada exitosamente!";
                txtSyncFeedback.color = Color.green;
            }

            // Actualizar la UI
            if (txtAccountStatus != null)
                txtAccountStatus.text = "<color=green>✓ Cuenta Vinculada</color>";
            if (bChangeName != null)
                bChangeName.interactable = false; // Nombre queda bloqueado
            if (bSyncAccount != null)
                bSyncAccount.gameObject.SetActive(false);

            // TODO: Entregar recompensa de vinculación (monedas + skin)
            // TopBarUI.Instance.ActualizarMonedas(200);
        }
        else
        {
            if (txtSyncFeedback != null)
            {
                // Traducimos los errores más comunes de Unity
                if (error.Contains("Password") || error.Contains("password"))
                    txtSyncFeedback.text = "La contraseña debe tener mínimo 8 caracteres, 1 mayúscula, 1 minúscula y 1 número/símbolo.";
                else if (error.Contains("already in use") || error.Contains("conflict"))
                    txtSyncFeedback.text = "Ese usuario ya está vinculado a otra cuenta.";
                else if (error.Contains("validation") || error.Contains("Invalid") || error.Contains("INVALID_USERNAME"))
                    txtSyncFeedback.text = "El usuario contiene caracteres no permitidos.";
                else
                    txtSyncFeedback.text = "Error: " + error;
                    
                txtSyncFeedback.color = Color.red;
            }
            if (btnConfirmSync != null) btnConfirmSync.interactable = true;
        }
    }

    // =====================================================================
    // BORRADO DE CUENTA
    // =====================================================================
    private void AbrirBorrado()
    {
        if (panelDeleteAccount == null) return;
        panelDeleteAccount.SetActive(!panelDeleteAccount.activeSelf);
        if (panelChangeName != null) panelChangeName.SetActive(false);
        if (panelSyncAccount != null) panelSyncAccount.SetActive(false);

        if (txtDeleteFeedback != null)
        {
            txtDeleteFeedback.text = "¿Estás seguro? Se perderán TODOS tus datos, trofeos y monedas para siempre.";
            txtDeleteFeedback.color = Color.red;
        }
    }

    private async void ConfirmarBorradoReal()
    {
        if (btnConfirmDelete != null) btnConfirmDelete.interactable = false;
        if (txtDeleteFeedback != null)
        {
            txtDeleteFeedback.text = "Borrando cuenta en la nube...";
            txtDeleteFeedback.color = Color.yellow;
        }

        try
        {
            // Borrar de UGS
            await Unity.Services.Authentication.AuthenticationService.Instance.DeleteAccountAsync();

            // Limpiar variables locales
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Unity.Services.Authentication.AuthenticationService.Instance.SignOut();

            if (txtDeleteFeedback != null)
            {
                txtDeleteFeedback.text = "✓ Cuenta eliminada con éxito. Reinicia el juego.";
                txtDeleteFeedback.color = Color.green;
            }

            Debug.Log("[PERFIL] Cuenta borrada correctamente. Destruyendo Singletons para reinicio completo...");

            // Esperar un segundo y medio para que el usuario lea el mensaje
            await System.Threading.Tasks.Task.Delay(1500);

            // Destruir los Singletons que sobreviven entre escenas (DontDestroyOnLoad)
            // Esto obliga a que, al recargar la escena, vuelvan a ejecutar su Awake() y arranquen un nuevo SignInAnonymously
            if (SessionNetworkManager.Instance != null) Destroy(SessionNetworkManager.Instance.gameObject);
            if (ProfileManager.Instance != null) Destroy(ProfileManager.Instance.gameObject);

            // Recargar la escena actual
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al borrar cuenta: {e.Message}");
            if (txtDeleteFeedback != null)
            {
                txtDeleteFeedback.text = "Error al borrar: " + e.Message;
                txtDeleteFeedback.color = Color.red;
            }
            if (btnConfirmDelete != null) btnConfirmDelete.interactable = true;
        }
    }
}
