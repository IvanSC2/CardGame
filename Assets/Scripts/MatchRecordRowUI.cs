using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class MatchRecordRowUI : MonoBehaviour
{
    [Header("Campos de la Fila")]
    public TMP_Text txtDate;
    public TMP_Text txtMode;
    public TMP_Text txtPosition;
    public TMP_Text txtPlayers;
    public TMP_Text txtMoney;
    public Image imgBackground;

    [Header("Colores de Fondo")]
    public Color colorVictoria = new Color(0.2f, 0.6f, 0.2f, 0.3f);
    public Color colorDerrota = new Color(0.6f, 0.2f, 0.2f, 0.3f);
    public Color colorNormal = new Color(0.3f, 0.3f, 0.3f, 0.2f);

    /// <summary>
    /// Puebla la fila con los datos de un MatchRecord.
    /// </summary>
    public void Configurar(MatchRecord record)
    {
        // --- Fecha (formato legible) ---
        if (txtDate != null)
        {
            if (System.DateTime.TryParse(record.date, out System.DateTime fecha))
            {
                txtDate.text = fecha.ToLocalTime().ToString("dd/MM/yy HH:mm");
            }
            else
            {
                txtDate.text = record.date;
            }
        }

        // --- Modo de juego ---
        if (txtMode != null)
        {
            txtMode.text = record.mode switch
            {
                "practice" => "Práctica",
                "private"  => "Privada",
                "public"   => "Pública",
                _          => record.mode
            };
        }

        // --- Puesto con iconos ---
        if (txtPosition != null)
        {
            if (record.status == "Interrumpida")
            {
                txtPosition.text = "<size=80%>(Interrumpida)</size>";
            }
            else
            {
                string icono = record.position switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"#{record.position}"
                };
                string textoBase = $"{icono} {record.position}º/{record.totalPlayers}";
                if (!string.IsNullOrEmpty(record.status))
                {
                    textoBase += $"\n<size=70%>({record.status})</size>";
                }
                txtPosition.text = textoBase;
            }
        }

        // --- Lista de jugadores ---
        if (txtPlayers != null)
        {
            if (record.playerNames != null && record.playerNames.Count > 0)
            {
                txtPlayers.text = string.Join(", ", record.playerNames);
            }
            else
            {
                txtPlayers.text = $"{record.totalPlayers} jugadores";
            }
        }

        // --- Cambio de dinero ---
        if (txtMoney != null)
        {
            if (record.moneyChange > 0)
            {
                txtMoney.text = $"+{record.moneyChange}";
                txtMoney.color = Color.green;
            }
            else if (record.moneyChange < 0)
            {
                txtMoney.text = $"{record.moneyChange}";
                txtMoney.color = Color.red;
            }
            else
            {
                txtMoney.text = "0";
                txtMoney.color = Color.gray;
            }
        }

        // --- Color de fondo ---
        if (imgBackground != null)
        {
            if (record.position == 1)
                imgBackground.color = colorVictoria;
            else if (record.position <= 3)
                imgBackground.color = colorNormal;
            else
                imgBackground.color = colorDerrota;
        }
    }
}
