using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using Unity.Services.Authentication;

public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> UgsId = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (AuthenticationService.Instance.IsSignedIn)
                UgsId.Value = AuthenticationService.Instance.PlayerId;

            if (ProfileManager.Instance != null && ProfileManager.Instance.TieneNickname())
                PlayerName.Value = ProfileManager.Instance.GetDisplayName();
            else
                PlayerName.Value = "Invitado";
        }
    }
}