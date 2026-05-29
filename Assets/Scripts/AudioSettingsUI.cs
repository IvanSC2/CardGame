using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Referencias a la UI")]
    public Toggle toggleMusic;
    public Toggle toggleSFX;
    public Button btnClose;

    [Header("Panel Principal")]
    public GameObject panelRoot; // El propio panel de settings para poder cerrarlo

    private void OnEnable()
    {
        // Al abrir el panel, sincronizamos los toggles con el estado real del AudioManager
        if (AudioManager.Instance != null)
        {
            if (toggleMusic != null) 
            {
                // Removemos listeners temporales para que no haga ruido al asignar el valor por código
                toggleMusic.onValueChanged.RemoveAllListeners();
                toggleMusic.isOn = !AudioManager.Instance.IsMusicMuted;
                toggleMusic.onValueChanged.AddListener(OnMusicToggleChanged);
            }

            if (toggleSFX != null)
            {
                toggleSFX.onValueChanged.RemoveAllListeners();
                toggleSFX.isOn = !AudioManager.Instance.IsSfxMuted;
                toggleSFX.onValueChanged.AddListener(OnSfxToggleChanged);
            }
        }

        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(ClosePanel);
        }
    }

    private void OnMusicToggleChanged(bool isOn)
    {
        if (AudioManager.Instance != null)
        {
            // isOn = true significa Música Activada. AudioManager necesita saber si está "Muted" (silenciado)
            AudioManager.Instance.ToggleMusic(!isOn);
            AudioManager.Instance.PlayButtonGeneric(); // Feedback
        }
    }

    private void OnSfxToggleChanged(bool isOn)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ToggleSFX(!isOn);
            AudioManager.Instance.PlayButtonGeneric(); // Feedback
        }
    }

    public void OpenPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void TogglePanelVisibility()
    {
        if (panelRoot != null) 
        {
            if (panelRoot.activeSelf)
                ClosePanel();
            else
                OpenPanel();
        }
    }

    public void ClosePanel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonGeneric();
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
