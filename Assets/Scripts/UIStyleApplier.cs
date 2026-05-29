using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UIStyleApplier : MonoBehaviour
{
    public enum ElementType
    {
        Ignorar,
        PanelFondoPrincipal,
        PanelBordeSecundario,
        BotonAccionPrimaria,
        BotonAccionPeligro,
        BotonSecundarioOscuro,
        BotonGrandeHundido,
        PanelMarcadorPildora,
        FondoTapeteJuego,
        TextoTituloDorado,
        TextoCuerpoClaro,
        TextoPeligroNeon
    }

    [Header("Estilo Automático")]
    [Tooltip("Elige qué rol tiene este elemento en la UI. Se pintará automáticamente según la paleta del UIThemeManager.")]
    public ElementType tipoDeElemento = ElementType.Ignorar;

    [Header("Personalización Manual")]
    [Tooltip("Activa esto para reemplazar el color base (ej. verde) por uno propio en este botón específico, regenerando la textura perfecta.")]
    public bool usarColorPersonalizado = false;
    public Color colorPersonalizado = Color.red;

    [Tooltip("Activa esto si prefieres colorear el texto a mano. El script respetará tu color y no pondrá el dorado por defecto.")]
    public bool conservarColorTexto = false;

    // Cache local para el sprite único de este botón
    [HideInInspector] public Sprite spritePersonalizado;
    [HideInInspector] [SerializeField] private Color colorGeneradoAnterior = Color.clear;
    [HideInInspector] [SerializeField] private ElementType tipoGeneradoAnterior = ElementType.Ignorar;

    private void Start()
    {
        ApplyStyle();
    }

    private void OnEnable()
    {
        ApplyStyle();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyStyle();
    }
#endif

    public void ApplyStyle()
    {
        if (UIThemeManager.Instance == null) return;
        if (tipoDeElemento == ElementType.Ignorar) return;

        var theme = UIThemeManager.Instance;
        Image img = GetComponent<Image>();
        TMP_Text txt = GetComponent<TMP_Text>();

        // Lógica de color de fondo a usar
        Color fondoBase = theme.cFondoOscuro;
        if (tipoDeElemento == ElementType.BotonAccionPrimaria) fondoBase = theme.cPrimario;
        if (tipoDeElemento == ElementType.BotonAccionPeligro) fondoBase = theme.cAlerta;

        Color fondoReal = usarColorPersonalizado ? colorPersonalizado : fondoBase;

        // 1. Aplicar a Imagen de fondo
        if (img != null)
        {
            // Si usamos color personalizado en un botón con textura, generamos un sprite único para no ensuciar los colores
            bool necesitaGeneracionLocal = usarColorPersonalizado && 
                (tipoDeElemento == ElementType.BotonAccionPrimaria || 
                 tipoDeElemento == ElementType.BotonSecundarioOscuro || 
                 tipoDeElemento == ElementType.BotonGrandeHundido ||
                 tipoDeElemento == ElementType.PanelMarcadorPildora);

            if (necesitaGeneracionLocal)
            {
                if (spritePersonalizado == null || colorGeneradoAnterior != colorPersonalizado || tipoGeneradoAnterior != tipoDeElemento)
                {
                    if (spritePersonalizado != null && spritePersonalizado.texture != null)
                    {
                        DestroyImmediate(spritePersonalizado.texture, true);
                        DestroyImmediate(spritePersonalizado, true);
                    }

                    if (tipoDeElemento == ElementType.BotonGrandeHundido)
                        spritePersonalizado = theme.GenerarSpriteBotonGrande(colorPersonalizado, theme.cPrimario, 128);
                    else if (tipoDeElemento == ElementType.BotonAccionPrimaria)
                        spritePersonalizado = theme.GenerarSpritePildora(colorPersonalizado, theme.cFondoOscuro, 2f, 96);
                    else if (tipoDeElemento == ElementType.BotonSecundarioOscuro)
                        spritePersonalizado = theme.GenerarSpritePildora(colorPersonalizado, theme.cPrimario, 3f, 96);
                    else if (tipoDeElemento == ElementType.PanelMarcadorPildora)
                        spritePersonalizado = theme.GenerarSpritePildora(new Color(colorPersonalizado.r, colorPersonalizado.g, colorPersonalizado.b, 0.4f), theme.cPrimario, 1.5f, 48);

                    colorGeneradoAnterior = colorPersonalizado;
                    tipoGeneradoAnterior = tipoDeElemento;
                }
            }

            switch (tipoDeElemento)
            {
                case ElementType.PanelFondoPrincipal:
                    img.color = fondoReal;
                    img.sprite = null; 
                    img.type = Image.Type.Simple;
                    break;
                case ElementType.PanelBordeSecundario:
                    img.color = theme.cAcento;
                    break;
                case ElementType.BotonAccionPrimaria:
                    img.sprite = usarColorPersonalizado ? spritePersonalizado : theme.spriteBotonPrimario;
                    img.color = Color.white;
                    img.type = Image.Type.Sliced;
                    break;
                case ElementType.BotonSecundarioOscuro:
                    img.sprite = usarColorPersonalizado ? spritePersonalizado : theme.spriteBotonSecundario;
                    img.color = Color.white; 
                    img.type = Image.Type.Sliced;
                    break;
                case ElementType.BotonGrandeHundido:
                    img.sprite = usarColorPersonalizado ? spritePersonalizado : theme.spriteBotonGrande;
                    img.color = Color.white; 
                    img.type = Image.Type.Sliced;
                    break;
                case ElementType.PanelMarcadorPildora:
                    img.sprite = usarColorPersonalizado ? spritePersonalizado : theme.spritePanelMarcador;
                    img.color = Color.white; 
                    img.type = Image.Type.Sliced;
                    break;
                case ElementType.BotonAccionPeligro:
                    img.color = fondoReal;
                    break;
                case ElementType.FondoTapeteJuego:
                    img.color = fondoReal;
                    break;
            }
        }

        // 2. Aplicar a Texto (si el componente está directamente en este objeto)
        if (txt != null)
        {
            AplicarEstiloTexto(txt, theme);
        }

        // 3. Aplicar a Textos Hijos (si es un botón o marcador)
        if (tipoDeElemento == ElementType.BotonAccionPrimaria || tipoDeElemento == ElementType.BotonAccionPeligro || 
            tipoDeElemento == ElementType.BotonSecundarioOscuro || tipoDeElemento == ElementType.BotonGrandeHundido ||
            tipoDeElemento == ElementType.PanelMarcadorPildora)
        {
            TMP_Text[] textosHijos = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in textosHijos)
            {
                AplicarEstiloTextoBotones(t, theme, tipoDeElemento);
            }
        }
    }

    private void AplicarEstiloTexto(TMP_Text t, UIThemeManager theme)
    {
        // Solo aplica estilo de texto a componentes independientes que NO son hijos de botones
        if (conservarColorTexto) return;

        switch (tipoDeElemento)
        {
            case ElementType.TextoTituloDorado:
                t.color = theme.cPrimario;
                if (theme.fuenteTitulos != null) t.font = theme.fuenteTitulos;
                break;
            case ElementType.TextoCuerpoClaro:
                t.color = new Color(0.95f, 0.9f, 0.85f, 1f);
                if (theme.fuenteCuerpo != null) t.font = theme.fuenteCuerpo;
                break;
            case ElementType.TextoPeligroNeon:
                t.color = theme.cAlerta;
                if (theme.fuenteTitulos != null) t.font = theme.fuenteTitulos;
                break;
        }
    }

    private void AplicarEstiloTextoBotones(TMP_Text t, UIThemeManager theme, ElementType tipoBoton)
    {
        if (theme.fuenteTitulos != null) t.font = theme.fuenteTitulos;
        
        if (conservarColorTexto) return; // Si decide conservar el color a mano, no aplicamos el dorado

        if (tipoBoton == ElementType.BotonAccionPrimaria)
        {
            t.color = theme.cFondoOscuro; // Texto oscuro sobre dorado
        }
        else if (tipoBoton == ElementType.BotonAccionPeligro)
        {
            t.color = Color.white;
        }
        else if (tipoBoton == ElementType.BotonSecundarioOscuro || tipoBoton == ElementType.BotonGrandeHundido || tipoBoton == ElementType.PanelMarcadorPildora)
        {
            t.color = theme.cPrimario; // Texto dorado sobre oscuro
        }
    }
}
