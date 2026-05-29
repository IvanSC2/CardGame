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
    
    // TIEMPOS DE TURNO
    public static float turnTime = 15f;

    // ANALÍTICAS: Hora de inicio de la partida para medir duración
    public static float matchStartTime = 0f;
    // TROFEOS (Solo para partidas de MatchMaking público)
    public static bool trophyAwarded = false;
    // Bote de trofeos acumulados de los jugadores que han muerto/abandonado
    // Se repartirá entre los supervivientes si el Host abandona
    public static int trophyBote = 0;

    // Trofeos que se ganan/pierden por posición en Matchmaking [1º, 2º, 3º, 4º/últimos]
    // Si hay menos jugadores, se usan los índices desde el final del array
    // Ej. en partida de 2: 1º -> +30, 2º -> -20
    public static readonly int[] trophyDeltaByRank = { 30, 10, -10, -20, -25, -28 };

    /// <summary>
    /// Calcula dinámicamente cuántos trofeos pierde un jugador eliminado según el tamaño de la sala.
    /// Diseñado para garantizar una economía de suma cero perfecta.
    /// </summary>
    public static int CalcularTrofeosPerdidos(int puesto, int totalPlayers)
    {
        bool esLoser = puesto > totalPlayers / 2;
        if (!esLoser) return 0; // La mitad superior no pierde trofeos

        int distanciaUltimo = totalPlayers - puesto;
        if (distanciaUltimo == 0) return 20;     // Último puesto
        if (distanciaUltimo == 1) return 15;     // Penúltimo puesto
        return 10;                              // Antepenúltimo puesto (3º por la cola o superior si es de 6)
    }
}
