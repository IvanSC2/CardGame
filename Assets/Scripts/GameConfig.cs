using UnityEngine;

public static class GameConfig
{
    //1 para jugar contra 1 bot, 2 para jugar contra 2 etc
    public static int nPlayers=1;
    public static int nHumanPlayers=1;

    // MONETIZACIÓN POST-PARTIDA
    public static int currentFee = 0;
    public static int currentPrize = 0;
    public static bool isPrivateMatch = false;
    public static bool isHostLobby = false;
    public static bool prizeAwarded = false;

    //0: Version ultra facil
    //1: Normal Mode por default
    //2: Hard Mode
    //3: Dificult Mode
    //4: Imposible Mode
    public static int difficulty=1; 

    // PERFIL: Modo de la partida actual (para registrar estadísticas correctamente)
    // Valores: "practice", "private", "public"
    public static string currentMatchMode = "practice";
    public static bool gameStarted = false;

    // ANALÍTICAS: Hora de inicio de la partida para medir duración
    public static float matchStartTime = 0f;
}
