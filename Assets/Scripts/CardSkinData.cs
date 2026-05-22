using UnityEngine;

// =============================================================================
// CARD SKIN DATA — ScriptableObject para definir skins de cartas
//
// ESTRUCTURA OPCIONAL: Este archivo compila y funciona de forma independiente.
// Si no hay ningún CardSkinData asignado en el juego, las cartas usan
// sus sprites por defecto sin que nada falle.
//
// USO FUTURO:
// 1. Click derecho en Project → Create → Card Game → Card Skin
// 2. Asignar los 4 sprites (frente, dorso, borde, fondo)
// 3. Registrar el nombre del skin en PlayerProfile.ownedSkins
// 4. Al instanciar cartas, consultar ProfileManager.Instance.Profile.activeSkin
//    y cargar el CardSkinData correspondiente desde Resources o Addressables
// =============================================================================

[CreateAssetMenu(fileName = "NewCardSkin", menuName = "Card Game/Card Skin")]
public class CardSkinData : ScriptableObject
{
    [Header("Identificación")]
    [Tooltip("Nombre único del skin. Debe coincidir con el string guardado en PlayerProfile.")]
    public string skinId = "default";

    [Tooltip("Nombre visible en la UI de selección de skins.")]
    public string displayName = "Baraja Clásica";

    [Header("Sprites del Skin (4 componentes)")]
    [Tooltip("Imagen del dorso de la carta (la que se ve boca abajo).")]
    public Sprite cardBack;

    [Tooltip("Marco/borde decorativo de la carta.")]
    public Sprite cardFrame;

    [Tooltip("Fondo de la carta (detrás del contenido).")]
    public Sprite cardBackground;

    [Tooltip("Icono decorativo que aparece en el centro del dorso.")]
    public Sprite cardEmblem;

    [Header("Colores (opcionales)")]
    [Tooltip("Color de acento para efectos visuales del skin.")]
    public Color accentColor = Color.white;

    [Tooltip("Color del texto sobre este skin.")]
    public Color textColor = Color.black;
}
