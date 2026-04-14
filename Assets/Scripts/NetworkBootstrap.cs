using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Threading.Tasks;

public class NetworkBootstrap : MonoBehaviour
{
    private string joinCodeInput = "";
    private string myJoinCode = "";

    private async void Start()
    {
        // PASO 0: Hacemos que este objeto (y el NetworkManager) sobrevivan al cambiar de escena
        DontDestroyOnLoad(gameObject);

        // Iniciamos los servicios de Unity en la nube
        await UnityServices.InitializeAsync();
        
        // Nos logueamos de forma anónima 
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Conectado a Unity Cloud con ID: {AuthenticationService.Instance.PlayerId}");
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            // BOTÓN HOST
            if (GUILayout.Button("Crear Partida (Host)"))
            {
                CrearPartidaRelay();
            }

            GUILayout.Space(20);

            // CAMPO DE TEXTO Y BOTÓN CLIENTE
            joinCodeInput = GUILayout.TextField(joinCodeInput, 10);
            if (GUILayout.Button("Unirse con Código"))
            {
                UnirsePartidaRelay(joinCodeInput);
            }
        }
        else
        {
            GUILayout.Label("Modo: " + (NetworkManager.Singleton.IsHost ? "Host" : "Cliente"));
            if (NetworkManager.Singleton.IsHost && !string.IsNullOrEmpty(myJoinCode))
            {
                GUILayout.Label($"TU CÓDIGO DE INVITACIÓN: {myJoinCode}");
            }
        }
        GUILayout.EndArea();
    }

    // --- LÓGICA DEL HOST ---
    private async void CrearPartidaRelay()
    {
        try
        {
            // Pedimos espacio para 5 clientes (6 jugadores en total)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(5);
            
            // Pedimos el código corto para compartir
            myJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"¡Partida creada! Código: {myJoinCode}");

            // Configuramos el transporte de Netcode para usar este Relay
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Arrancamos el Host
            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Error al crear la partida Relay: {e}");
        }
    }

    // --- LÓGICA DEL CLIENTE ---
    private async void UnirsePartidaRelay(string joinCode)
    {
        try
        {
            Debug.Log($"Intentando unirse con código: {joinCode}");
            
            // Pedimos a Unity que nos deje entrar con ese código
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Configuramos el transporte con los datos recibidos
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Arrancamos el Cliente
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Error al unirse a la partida Relay: {e}");
        }
    }
}