using UnityEngine;
using TMPro;

[ExecuteAlways]
public class UIThemeManager : MonoBehaviour
{
    public static UIThemeManager Instance;

    [Header("Colores de la Paleta (Estilo Rummikub Verde)")]
    public Color cPrimario    = new Color(0.957f, 0.765f, 0.357f, 1f); // Dorado
    public Color cAlerta      = new Color(0.878f, 0.275f, 0.169f, 1f); // Rojo
    public Color cFondoOscuro = new Color(0.090f, 0.376f, 0.259f, 1f); // #176042 (Verde Rummikub)
    public Color cAcento      = new Color(0.847f, 0.478f, 0.161f, 1f); // Cobre/Naranja
    public Color cFondoTapete = new Color(0.060f, 0.250f, 0.170f, 1f); // Verde un poco más oscuro para contraste

    [Header("Tipografías (Opcional)")]
    public TMP_FontAsset fuenteTitulos;
    public TMP_FontAsset fuenteCuerpo;

    // Sprites generados procedimentalmente
    [HideInInspector] public Sprite spriteBotonPrimario;
    [HideInInspector] public Sprite spriteBotonSecundario;
    [HideInInspector] public Sprite spriteBotonGrande;
    [HideInInspector] public Sprite spritePanelMarcador; // Para monedas y trofeos

    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance = this;
        }
        GenerarSprites();
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        GenerarSprites();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Instance = this;
        GenerarSprites();
        
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            UIStyleApplier[] appliers = Resources.FindObjectsOfTypeAll<UIStyleApplier>();
            foreach (var app in appliers)
            {
                if (app != null && app.gameObject.scene.name != null) 
                {
                    app.ApplyStyle();
                }
            }
        };
    }
#endif

    public void GenerarSprites()
    {
        LimpiarSpritesAntiguos();
        // Generamos a diferentes tamaños para evitar que el 9-slice los achate si el RectTransform es muy pequeño
        spriteBotonPrimario = GenerarSpritePildora(cPrimario, cFondoOscuro, 2f, 96);
        spriteBotonSecundario = GenerarSpritePildora(cFondoOscuro, cPrimario, 3f, 96);
        spriteBotonGrande = GenerarSpriteBotonGrande(cFondoOscuro, cPrimario, 128);
        
        // Marcador muy pequeño (48px) para que no se deforme en las barras superiores finas
        spritePanelMarcador = GenerarSpritePildora(new Color(0f, 0f, 0f, 0.4f), cPrimario, 1.5f, 48);
    }

    private void LimpiarSpritesAntiguos()
    {
        if (spriteBotonPrimario != null)
        {
            if (spriteBotonPrimario.texture != null) DestroyImmediate(spriteBotonPrimario.texture, true);
            DestroyImmediate(spriteBotonPrimario, true);
        }
        if (spriteBotonSecundario != null)
        {
            if (spriteBotonSecundario.texture != null) DestroyImmediate(spriteBotonSecundario.texture, true);
            DestroyImmediate(spriteBotonSecundario, true);
        }
        if (spriteBotonGrande != null)
        {
            if (spriteBotonGrande.texture != null) DestroyImmediate(spriteBotonGrande.texture, true);
            DestroyImmediate(spriteBotonGrande, true);
        }
        if (spritePanelMarcador != null)
        {
            if (spritePanelMarcador.texture != null) DestroyImmediate(spritePanelMarcador.texture, true);
            DestroyImmediate(spritePanelMarcador, true);
        }
    }

    public Sprite GenerarSpriteBotonGrande(Color colorFondo, Color colorBrillo, int size)
    {
        float radius = size * (24f / 128f); // Escala el radio basado en el tamaño original de 128
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2[] corners = new Vector2[] {
            new Vector2(radius, radius),                 // BL
            new Vector2(size - radius, radius),          // BR
            new Vector2(radius, size - radius),          // TL
            new Vector2(size - radius, size - radius)    // TR
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 pos = new Vector2(x, y);
                float distToCorner = 0f;
                bool isCorner = false;

                if (x < radius && y < radius) { distToCorner = Vector2.Distance(pos, corners[0]); isCorner = true; }
                else if (x > size - radius && y < radius) { distToCorner = Vector2.Distance(pos, corners[1]); isCorner = true; }
                else if (x < radius && y > size - radius) { distToCorner = Vector2.Distance(pos, corners[2]); isCorner = true; }
                else if (x > size - radius && y > size - radius) { distToCorner = Vector2.Distance(pos, corners[3]); isCorner = true; }

                float alpha = 1f;
                if (isCorner)
                {
                    alpha = Mathf.Clamp01(radius - distToCorner + 0.5f);
                    if (alpha <= 0)
                    {
                        pixels[index] = Color.clear;
                        continue;
                    }
                }

                Color pixelColor = colorFondo;

                // Efecto Hundido (Inset)
                float distTop = size - y;
                float distBottom = y;
                float distLeft = x;
                float distRight = size - x;
                float ratio = size / 128f;

                if (distTop < 15f * ratio)
                    pixelColor = Color.Lerp(pixelColor, new Color(0,0,0,0.8f), (15f * ratio - distTop) / (15f * ratio));
                if (distLeft < 15f * ratio)
                    pixelColor = Color.Lerp(pixelColor, new Color(0,0,0,0.5f), (15f * ratio - distLeft) / (15f * ratio));

                if (distBottom < 10f * ratio)
                    pixelColor = Color.Lerp(pixelColor, colorBrillo, ((10f * ratio - distBottom) / (10f * ratio)) * 0.6f);
                if (distRight < 10f * ratio)
                    pixelColor = Color.Lerp(pixelColor, colorBrillo, ((10f * ratio - distRight) / (10f * ratio)) * 0.4f);

                if (x > size - radius * 1.5f && y < radius * 1.5f)
                {
                    float cornerDist = Vector2.Distance(pos, new Vector2(size, 0));
                    if (cornerDist < 35f * ratio)
                        pixelColor = Color.Lerp(pixelColor, new Color(1,1,1,0.6f), (35f * ratio - cornerDist) / (35f * ratio));
                }
                if (x < radius * 1.5f && y < radius * 1.5f)
                {
                    float cornerDist = Vector2.Distance(pos, new Vector2(0, 0));
                    if (cornerDist < 35f * ratio)
                        pixelColor = Color.Lerp(pixelColor, new Color(1,1,1,0.4f), (35f * ratio - cornerDist) / (35f * ratio));
                }

                pixelColor.a *= alpha;
                pixels[index] = pixelColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        int border = (int)radius + 2;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    public Sprite GenerarSpritePildora(Color colorFondo, Color colorBorde, float grosorBorde, int size)
    {
        float radius = size / 2f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);

                // Antialiasing para bordes curvos suaves (1 píxel de suavizado)
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                if (alpha <= 0)
                {
                    pixels[index] = Color.clear;
                    continue;
                }

                Color pixelColor;
                float innerRadius = radius - grosorBorde;

                if (dist > innerRadius)
                {
                    pixelColor = colorBorde;
                }
                else
                {
                    pixelColor = colorFondo;
                }

                if (colorFondo.a > 0f) 
                {
                    float normalizedY = y / (float)size; 
                    
                    if (normalizedY > 0.5f)
                    {
                        float brillo = (normalizedY - 0.5f) * 2f;
                        pixelColor = Color.Lerp(pixelColor, new Color(1, 1, 1, 0.5f), brillo * 0.5f);
                    }
                    else
                    {
                        float sombra = (0.5f - normalizedY) * 2f;
                        pixelColor = Color.Lerp(pixelColor, new Color(0, 0, 0, 0.8f), sombra * 0.6f);
                    }

                    float ratio = size / 128f;
                    if (dist < innerRadius && dist > innerRadius - (8f * ratio) && y > radius)
                    {
                        float intensidadReflejo = (dist - (innerRadius - (8f * ratio))) / (8f * ratio);
                        pixelColor = Color.Lerp(pixelColor, new Color(1, 1, 1, 0.8f), intensidadReflejo * 0.8f);
                    }
                }

                pixelColor.a *= alpha; 
                pixels[index] = pixelColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // Para el 9-slice de una píldora el corte debe ser el centro exacto menos 1 (ej: si es 48, corte en 23)
        int slicePos = (size / 2) - 1;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slicePos, slicePos, slicePos, slicePos));
    }
}
