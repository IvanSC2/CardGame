using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BettingManager : MonoBehaviour
{
    public static BettingManager Instance;
    
    [Header("UI References")]
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public Button[] betButtons; // Arrastra los 6 botones aquí (índice 0 = apuesta 0)
    [Header("Referencias de Escena")]
public GameObject tableObject; // Arrastra el CoTable aquí
    [Header("Game State")]
    public int cardsInRound = 5;
    public int p1Bet;
    public int p2Bet;

    private bool isP1Choosing = true;

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);

        // Configurar clicks de botones
        for (int i = 0; i < betButtons.Length; i++)
        {
            int val = i; 
            betButtons[i].onClick.AddListener(() => OnBetClicked(val));
        }
    }

    public void StartBettingPhase(int numCards)
    {

        cardsInRound = numCards;
        isP1Choosing = true;
        tableObject.SetActive(false);
        panelRoot.SetActive(true);
        SetupUIForP1();
    }

    private void SetupUIForP1()
    {
        titleText.text = "JUGADOR 1: ¿Cuántas vas a ganar?";
        // P1 siempre puede elegir todo (0 a cardsInRound)
        for (int i = 0; i < betButtons.Length; i++)
        {
            betButtons[i].interactable = (i <= cardsInRound);
        }
    }

    private void SetupUIForP2()
    {
        titleText.text = "JUGADOR 2: ¿Cuántas vas a ganar?";
        int forbiddenBet = cardsInRound - p1Bet;

        for (int i = 0; i < betButtons.Length; i++)
        {
            // Regla de Oro: Suma no puede ser igual a total cartas
            bool isAllowed = (i <= cardsInRound) && (i != forbiddenBet);
            betButtons[i].interactable = isAllowed;
        }
    }

    private void OnBetClicked(int amount)
    {
        if (isP1Choosing)
        {
            p1Bet = amount;
            // Mensaje informativo
            InteractionManager.Instance.SetInfoMessage($"P1 APUESTA QUE GANARA {p1Bet} BAZAS");
            
            isP1Choosing = false;
            SetupUIForP2();
        }
        else
        {
            p2Bet = amount;
            // Mensaje informativo
            InteractionManager.Instance.SetInfoMessage($"P2 APUESTA QUE GANARA {p2Bet} BAZAS.\n¡A JUGAR!");
            
            panelRoot.SetActive(false);
            tableObject.SetActive(true);
            
            InteractionManager.Instance.InitializeGame();
        }
    }
}