using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    // Esto se ejecuta cuando el objeto aparece en la red
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Solo el dueño del objeto envia el saludo
            EnviarSaludoServerRpc($"¡Hola! Soy el jugador {OwnerClientId}");
        }
    }

    [ServerRpc] // Este código lo envía el Cliente pero se ejecuta SOLO en el Host
    private void EnviarSaludoServerRpc(string mensaje)
    {
        Debug.Log($"[SERVIDOR] Recibido del cliente: {mensaje}");
        // Ahora el Host le responde a todos
        ResponderTodosClientRpc(mensaje + " (Confirmado por el Servidor)");
    }

    [ClientRpc] // Este código lo envía el Host pero se ejecuta en TODOS los Clientes
    private void ResponderTodosClientRpc(string mensaje)
    {
        Debug.Log($"[CLIENTE] El servidor dice: {mensaje}");
    }
}