using System.Collections.Generic;

// =============================================================================
// MODELOS DE DATOS DEL JUGADOR (4 Entidades NoSQL para CloudSave)
//
// Estas clases se serializan a JSON con JsonUtility y se persisten en
// Unity Cloud Save como claves individuales:
//   - "PlayerProfile"  → PlayerProfile
//   - "MisMonedas"      → int (ya existente, gestionado por TopBarUI)
//   - "NoAdsOwned"      → bool (ya existente, gestionado por ShopController)
//   - "PlayerStats"     → PlayerStats
//   - "MatchHistory"    → MatchHistoryData
//
// La entidad "Wallet" no necesita clase propia porque ya existe como dos
// claves separadas (MisMonedas + NoAdsOwned) que se gestionan desde
// TopBarUI.cs y ShopController.cs respectivamente.
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// ENTIDAD 1: Perfil del Jugador
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class PlayerProfile
{
    public string nickname = "";
    public int avatarId = -1;               // -1 = avatar personalizado (galería), 0+ = predefinido
    public string customAvatarPath = "";    // Ruta local si eligió foto de galería
    public string registrationDate = "";    // ISO 8601 (se fija al crear el perfil)
    public bool isLinked = false;           // true tras vincular Email/Password

    // SKINS (estructura preparada, no obligatoria)
    public string activeSkin = "default";
    public List<string> ownedSkins = new List<string> { "default" };
}

// ─────────────────────────────────────────────────────────────────────────────
// ENTIDAD 3: Estadísticas por Modo de Juego
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class PlayerStats
{
    public ModeStats practice = new ModeStats();
    public ModeStats privateMatch = new ModeStats();
    public ModeStats publicMatch = new ModeStats();

    /// <summary>
    /// Devuelve las estadísticas del modo indicado ("practice", "private", "public").
    /// </summary>
    public ModeStats GetByMode(string mode)
    {
        switch (mode)
        {
            case "practice": return practice;
            case "private":  return privateMatch;
            case "public":   return publicMatch;
            default:         return practice;
        }
    }
}

[System.Serializable]
public class ModeStats
{
    public int gamesPlayed = 0;
    public int gamesWon = 0;
    public int highestWinRow = 0;       // Racha máxima de victorias consecutivas
    public int currentWinRow = 0;       // Racha actual (se resetea al perder)
    public int totalMoneyEarned = 0;    // Solo relevante para Private
    public int hardestWinDifficulty = 0;// Solo relevante para Practice (nivel de bots)

    public float WinPercentage => gamesPlayed > 0 ? (gamesWon * 100f / gamesPlayed) : 0f;
}

// ─────────────────────────────────────────────────────────────────────────────
// ENTIDAD 4: Historial de Partidas
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class MatchHistoryData
{
    public const int MAX_RECORDS = 50;
    public List<MatchRecord> matches = new List<MatchRecord>();

    /// <summary>
    /// Añade un registro al inicio de la lista y elimina el más antiguo si se supera el límite.
    /// </summary>
    public void AddRecord(MatchRecord record)
    {
        matches.Insert(0, record); // Más reciente primero
        while (matches.Count > MAX_RECORDS)
        {
            matches.RemoveAt(matches.Count - 1);
        }
    }
}

[System.Serializable]
public class MatchRecord
{
    public string date = "";                // ISO 8601 con hora
    public string mode = "";                // "practice", "private", "public"
    public int position = 0;                // Puesto final (1 = ganador)
    public int totalPlayers = 0;            // Jugadores en la partida
    public List<string> playerNames = new List<string>(); // Nombres de todos los participantes
    public int moneyChange = 0;             // +ganado o -perdido
    public string status = "";              // Ej: "", "Abandonada", "Interrumpida"
}
