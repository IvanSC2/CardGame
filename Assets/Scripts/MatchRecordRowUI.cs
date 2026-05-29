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
    public Color colorDerrota  = new Color(0.6f, 0.2f, 0.2f, 0.3f);
    public Color colorNormal   = new Color(0.3f, 0.3f, 0.3f, 0.2f);

    // ─────────────────────────────────────────────────────────────────────
    // Aplicamos en Awake el comportamiento de ancho fijo a todos los TMP
    // de esta fila: sin word wrap y con overflow Truncate para que NUNCA
    // se expandan horizontalmente, solo en vertical si el texto ocupa más.
    // ─────────────────────────────────────────────────────────────────────
    // Anchos preferidos de cada columna (en unidades UI).
    // Ajústalos libremente desde el Inspector o directamente aquí.
    [Header("Anchos de Columna (unidades UI)")]
    public float anchoFecha     = 130f;
    public float anchoModo      = 90f;
    public float anchoPosicion  = 90f;
    public float anchoJugadores = 200f;
    public float anchoDinero    = 120f;

    private void Awake()
    {
        // Fijar el ancho del root: horizontal no crece, vertical sí
        var rootFitter = GetComponent<ContentSizeFitter>();
        if (rootFitter != null)
        {
            rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rootFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        }

        // Asignar LayoutElement con ancho fijo a cada columna de texto
        AplicarAncho(txtDate,      anchoFecha);
        AplicarAncho(txtMode,      anchoModo);
        AplicarAncho(txtPosition,  anchoPosicion);
        AplicarAncho(txtPlayers,   anchoJugadores);
        AplicarAncho(txtMoney,     anchoDinero);
    }

    /// <summary>
    /// Añade (o reutiliza) un LayoutElement con ancho fijo y configura el TMP
    /// para que el texto se parta en líneas (word wrap) sin expandirse hacia los lados.
    /// </summary>
    private void AplicarAncho(TMP_Text label, float ancho)
    {
        if (label == null) return;

        // Word wrap: el texto se parte dentro del ancho dado
        label.enableWordWrapping = true;
        // Overflow vertical: crece hacia abajo, nunca hacia los lados
        label.overflowMode = TextOverflowModes.Overflow;

        // LayoutElement: le dice al HorizontalLayoutGroup el ancho exacto de esta celda
        var le = label.GetComponent<LayoutElement>();
        if (le == null) le = label.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = ancho;
        le.minWidth       = ancho;
        le.flexibleWidth  = 0f; // No estirarse más allá del ancho preferido
    }

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
                txtDate.text = fecha.ToLocalTime().ToString("dd/MM/yy\nHH:mm");
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
                string textoBase = $"{record.position}º/{record.totalPlayers}";
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
                // Un nombre por línea para que crezca en vertical, no en horizontal
                string formatedNames = "";
                for (int i = 0; i < record.playerNames.Count; i++)
                {
                    formatedNames += record.playerNames[i];
                    if (i < record.playerNames.Count - 1)
                        formatedNames += "\n";
                }
                txtPlayers.text = formatedNames;
            }
            else
            {
                txtPlayers.text = $"{record.totalPlayers}\njugadores";
            }
        }

        // --- Cambio de dinero (y trofeos en públicas, en la misma columna) ---
        if (txtMoney != null)
        {
            string lineaDinero;
            if (record.moneyChange > 0)
            {
                lineaDinero = $"<color=green>+{record.moneyChange} mn</color>";
            }
            else if (record.moneyChange < 0)
            {
                lineaDinero = $"<color=red>{record.moneyChange} mn</color>";
            }
            else
            {
                lineaDinero = "<color=grey>0 mn</color>";
            }

            // Solo partidas públicas muestran trofeos (en el renglón de debajo del dinero)
            if (record.mode == "public")
            {
                string lineaTrofeos;
                if (record.trophyChange > 0)
                    lineaTrofeos = $"<color=green>+{record.trophyChange} cop</color>";
                else if (record.trophyChange < 0)
                    lineaTrofeos = $"<color=red>{record.trophyChange} cop</color>";
                else
                    lineaTrofeos = "<color=grey>0 cop</color>";

                txtMoney.text = $"{lineaDinero}\n{lineaTrofeos}";
            }
            else
            {
                txtMoney.text = lineaDinero;
            }

            // Resetear el color del texto al blanco (ahora usamos rich text para los colores)
            txtMoney.color = Color.white;
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
