using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenuManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject optionsPanel;
    public OptionController selectorJugadores;
    public OptionController selectorDificultad;
    void Start()
    {
        if (selectorJugadores != null)
        {
          
            selectorJugadores.EstablecerIndice(GameConfig.nPlayers - 1);
        }
        
        if (selectorDificultad != null)
        {
            // El índice de la dificultad coincide exactamente con el valor (1 = Normal)
            selectorDificultad.EstablecerIndice(GameConfig.difficulty);
        }
    }


    public void Jugar()
{ 
    if (selectorJugadores != null)
    {
        GameConfig.nPlayers = selectorJugadores.ObtenerIndice() + 1; 
    }
    
    if (selectorDificultad != null)
    {
        GameConfig.difficulty = selectorDificultad.ObtenerIndice(); 
    }

    if (CardDatabase.deck != null)
    {
        CardDatabase.deck.Clear();
    }


    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
    {
        
        NetworkManager.Singleton.SceneManager.LoadScene("MainGame", LoadSceneMode.Single);
    }
    else
    {
        
        Debug.Log("Eres un cliente. Esperando a que el Host inicie la partida...");
    }
}

    public void AbrirOpciones()
    {
        if(optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }

    }

    public void CerrarOpciones()
    {
        if(optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }
    // Update is called once per frame
   public void Salir()
    {
        Debug.Log("Saliendo del Juego");
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif

    }
}
