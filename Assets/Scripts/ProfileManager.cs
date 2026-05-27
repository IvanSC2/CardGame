using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

// =============================================================================
// PROFILE MANAGER — Singleton que centraliza la persistencia del jugador
//
// Gestiona las 4 entidades del modelo de datos en Unity Cloud Save:
//   1. PlayerProfile  → "PlayerProfile"
//   2. Wallet         → "MisMonedas" + "NoAdsOwned" (gestionado por TopBarUI/ShopController)
//   3. PlayerStats    → "PlayerStats"
//   4. MatchHistory   → "MatchHistory"
//
// Se inicializa automáticamente tras el login en SessionNetworkManager.Awake().
// =============================================================================

public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    // ─── DATOS EN MEMORIA ──────────────────────────────────────────────────
    public PlayerProfile Profile { get; private set; } = new PlayerProfile();
    public PlayerStats Stats { get; private set; } = new PlayerStats();
    public MatchHistoryData History { get; private set; } = new MatchHistoryData();

    /// <summary>Indica si los datos del perfil ya se cargaron desde la nube.</summary>
    public bool IsLoaded { get; private set; } = false;

    // ─── CLAVES DE CLOUD SAVE ──────────────────────────────────────────────
    private const string KEY_PROFILE = "PlayerProfile";
    private const string KEY_STATS   = "PlayerStats";
    private const string KEY_HISTORY = "MatchHistory";

    // ─── SINGLETON ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =====================================================================
    // 1. CARGA COMPLETA (llamada tras el login)
    // =====================================================================
    /// <summary>
    /// Carga las 3 entidades propias desde CloudSave. La Wallet (MisMonedas)
    /// se sigue cargando desde TopBarUI.CargarEconomiaNube().
    /// </summary>
    public async Task CargarPerfilCompleto()
    {
        try
        {
            var keys = new HashSet<string> { KEY_PROFILE, KEY_STATS, KEY_HISTORY };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            // --- Perfil ---
            if (data.TryGetValue(KEY_PROFILE, out var profileItem))
            {
                Profile = JsonUtility.FromJson<PlayerProfile>(profileItem.Value.GetAsString());
            }
            else
            {
                // Primera vez: creamos un perfil vacío con fecha de registro
                Profile = new PlayerProfile
                {
                    registrationDate = System.DateTime.UtcNow.ToString("o")
                };
            }

            // --- Estadísticas ---
            if (data.TryGetValue(KEY_STATS, out var statsItem))
            {
                Stats = JsonUtility.FromJson<PlayerStats>(statsItem.Value.GetAsString());
            }
            else
            {
                Stats = new PlayerStats();
            }

            // --- Historial ---
            if (data.TryGetValue(KEY_HISTORY, out var historyItem))
            {
                History = JsonUtility.FromJson<MatchHistoryData>(historyItem.Value.GetAsString());
            }
            else
            {
                History = new MatchHistoryData();
            }

            // Sincronizar nickname con PlayerPrefs para la comprobación rápida del WelcomePanel
            if (!string.IsNullOrEmpty(Profile.nickname))
            {
                PlayerPrefs.SetString("Nickname", Profile.nickname);
                PlayerPrefs.Save();
            }

            IsLoaded = true;
            Debug.Log($"[PERFIL] Cargado: \"{Profile.nickname}\" | " +
                      $"Partidas: {Stats.practice.gamesPlayed + Stats.privateMatch.gamesPlayed + Stats.publicMatch.gamesPlayed} | " +
                      $"Historial: {History.matches.Count} registros");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al cargar perfil: {e.Message}");
            // Fallback: usamos datos vacíos para que el juego no crashee
            IsLoaded = true;
        }
    }

    // =====================================================================
    // 2. GUARDADO INDIVIDUAL DE CADA ENTIDAD
    // =====================================================================
    public async Task GuardarPerfil()
    {
        try
        {
            string json = JsonUtility.ToJson(Profile);
            var data = new Dictionary<string, object> { { KEY_PROFILE, json } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log("[PERFIL] Perfil guardado en la nube.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al guardar perfil: {e.Message}");
        }
    }

    public async Task GuardarEstadisticas()
    {
        try
        {
            string json = JsonUtility.ToJson(Stats);
            var data = new Dictionary<string, object> { { KEY_STATS, json } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log("[PERFIL] Estadísticas guardadas en la nube.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al guardar estadísticas: {e.Message}");
        }
    }

    public async Task GuardarHistorial()
    {
        try
        {
            string json = JsonUtility.ToJson(History);
            var data = new Dictionary<string, object> { { KEY_HISTORY, json } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log("[PERFIL] Historial guardado en la nube.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al guardar historial: {e.Message}");
        }
    }

    // =====================================================================
    // 3. REGISTRO DE RESULTADO DE PARTIDA (Stats + Historial en una llamada)
    // =====================================================================
    /// <summary>
    /// Registra el resultado de una partida terminada. Actualiza estadísticas 
    /// y añade un registro al historial. Persiste ambos en CloudSave.
    /// </summary>
    /// <param name="mode">"practice", "private", o "public"</param>
    /// <param name="position">Puesto final (1 = ganador)</param>
    /// <param name="totalPlayers">Jugadores en la partida</param>
    /// <param name="moneyChange">Dinero ganado (+) o perdido (-)</param>
    /// <param name="playerNames">Lista de nombres de los jugadores</param>
    /// <param name="difficulty">Dificultad de los bots (solo para Practice)</param>
    public async void RegistrarResultadoPartida(
        string mode, int position, int totalPlayers,
        int moneyChange, List<string> playerNames = null, int difficulty = 0, string status = "")
    {
        // --- Actualizar estadísticas ---
        ModeStats modeStats = Stats.GetByMode(mode);
        modeStats.gamesPlayed++;

        if (position == 1 && string.IsNullOrEmpty(status))
        {
            modeStats.gamesWon++;
            modeStats.currentWinRow++;
            if (modeStats.currentWinRow > modeStats.highestWinRow)
                modeStats.highestWinRow = modeStats.currentWinRow;

            // Practice: registrar la dificultad más alta con victoria
            if (mode == "practice" && difficulty > modeStats.hardestWinDifficulty)
                modeStats.hardestWinDifficulty = difficulty;
        }
        else
        {
            modeStats.currentWinRow = 0; // Se rompe la racha
        }

        if (moneyChange > 0)
            modeStats.totalMoneyEarned += moneyChange;

        // --- Añadir al historial ---
        MatchRecord record = new MatchRecord
        {
            date = System.DateTime.UtcNow.ToString("o"),
            mode = mode,
            position = position,
            totalPlayers = totalPlayers,
            moneyChange = moneyChange,
            playerNames = playerNames ?? new List<string>(),
            status = status
        };
        History.AddRecord(record);

        // --- Persistir en la nube (en paralelo) ---
        await Task.WhenAll(GuardarEstadisticas(), GuardarHistorial());
    }

    // =====================================================================
    // 4. IDENTIDAD Y VINCULACIÓN
    // =====================================================================
    /// <summary>¿El jugador ya eligió un nickname?</summary>
    public bool TieneNickname()
    {
        return !string.IsNullOrEmpty(Profile.nickname);
    }

    /// <summary>¿La cuenta sigue siendo anónima (sin vincular)?</summary>
    public bool EsInvitado()
    {
        return !Profile.isLinked;
    }

    /// <summary>
    /// Guarda el nickname elegido por el jugador.
    /// </summary>
    public async Task EstablecerNickname(string nuevoNombre)
    {
        Profile.nickname = nuevoNombre.Trim();
        PlayerPrefs.SetString("Nickname", Profile.nickname);
        PlayerPrefs.Save();
        await GuardarPerfil();
    }

    /// <summary>
    /// Vincula la cuenta anónima actual a un Email/Password de UGS Authentication.
    /// Tras vincular, el nombre queda bloqueado (inmutable).
    /// </summary>
    /// <returns>null si fue exitoso, o el string del error si falló</returns>
    public async Task<string> VincularCuenta(string email, string password)
    {
        try
        {
            await AuthenticationService.Instance.AddUsernamePasswordAsync(email, password);

            Profile.isLinked = true;
            await GuardarPerfil();

            Debug.Log($"[PERFIL] Cuenta vinculada exitosamente a: {email}");
            return null; // Éxito
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"[PERFIL] Error de autenticación al vincular: {e.Message}");
            return e.Message;
        }
        catch (RequestFailedException e)
        {
            Debug.LogError($"[PERFIL] Error de red al vincular: {e.Message}");
            return "Error de red: " + e.Message;
        }
    }

    /// <summary>
    /// Cierra la sesión anónima actual e inicia sesión con un correo y contraseña.
    /// Útil para recuperar cuentas tras desinstalar el juego.
    /// </summary>
    /// <returns>null si fue exitoso, o el string del error si falló</returns>
    public async Task<string> LoginCuenta(string email, string password)
    {
        try
        {
            // 1. Cerramos la sesión anónima basura que se nos asignó al arrancar
            AuthenticationService.Instance.SignOut();
            AuthenticationService.Instance.ClearSessionToken();

            // 2. Iniciamos sesión con nuestras credenciales reales
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);

            Debug.Log($"[PERFIL] Login exitoso. Nuevo PlayerID: {AuthenticationService.Instance.PlayerId}");

            // 3. Recargamos todos los datos del servidor para sobreescribir la basura local
            await CargarPerfilCompleto();
            
            // 4. Recargamos la economía para sincronizar monedas y trofeos
            if (TopBarUI.Instance != null)
            {
                await TopBarUI.Instance.CargarEconomiaNube();
            }

            return null; // Éxito
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"[PERFIL] Error de autenticación al logear: {e.Message}");
            return e.Message;
        }
        catch (RequestFailedException e)
        {
            Debug.LogError($"[PERFIL] Error de red al logear: {e.Message}");
            return "Error de red: " + e.Message;
        }
    }

    /// <summary>
    /// Actualiza el avatar del jugador con una imagen de la galería.
    /// </summary>
    /// <param name="localPath">Ruta local de la imagen seleccionada</param>
    public async Task CambiarAvatarPersonalizado(string localPath)
    {
        Profile.avatarId = -1; // -1 indica avatar personalizado
        Profile.customAvatarPath = localPath;
        await GuardarPerfil();
    }

    /// <summary>
    /// Devuelve el nickname del jugador, o "Invitado" si no tiene uno.
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(Profile.nickname))
            return Profile.nickname;
        return "Invitado";
    }
    /// <summary>
    /// Borra toda la cuenta del usuario de la nube (datos y autenticación)
    /// </summary>
    public async Task DeleteAccountAsync()
    {
        try
        {
            // 1. Borramos los datos de Cloud Save primero
            var keysToDelete = new List<string> { KEY_PROFILE, KEY_STATS, KEY_HISTORY, "MisMonedas", "NoAdsOwned" };
            await CloudSaveService.Instance.Data.Player.DeleteAllAsync();

            // 2. Borramos la cuenta de Authentication
            await AuthenticationService.Instance.DeleteAccountAsync();

            // 3. Limpiamos datos locales
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[PERFIL] Cuenta borrada por completo.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PERFIL] Error al borrar cuenta: {e.Message}");
            throw; // Re-lanzar para que la UI pueda mostrar el error
        }
    }
}
