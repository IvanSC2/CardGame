using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerProfileUI : MonoBehaviour
{
    [Header("Conexiones de Textos")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI statsText;

    [Header("Avatar")]
    public Image imgAvatar;

   
    public void ActualizarPerfil(string nombre, int vidas, int bazasGanadas, int apuestaActual, Sprite avatar = null)
    {
        // 1. Actualiza el nombre
        if (nameText != null) nameText.text = nombre;

        // 2. Dibuja los corazones (o pone ELIMINADO si muere)
        if (livesText != null)
        {
            if (vidas <= 0) 
                livesText.text = "<color=grey><size=30%>ELIMINADO</size></color>";
            else 
                livesText.text = "<color=red>" + new string('♥', vidas) + "</color>";
        }

        // 3. Actualiza los stats (Si la apuesta es -1, significa que aún no ha apostado)
        if (statsText != null)
        {
            string textoApuesta = (apuestaActual >= 0) ? apuestaActual.ToString() : "-";
            statsText.text = $"Bazas: <b>{bazasGanadas}</b> / Apuesta: <b>{textoApuesta}</b>";
        }

        // 4. Avatar (solo se actualiza si se pasa uno nuevo)
        if (avatar != null && imgAvatar != null)
        {
            imgAvatar.sprite = avatar;
        }
    }
}