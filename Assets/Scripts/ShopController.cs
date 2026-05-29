#pragma warning disable 0618
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections.Generic;

public class ShopController : MonoBehaviour, IStoreListener
{
    [Header("Panel de Confirmación")]
    public GameObject panelConfirmacion;
    private string idProductoPendiente;
    private static IStoreController m_StoreController;

    // --- IDs DE LOS PRODUCTOS ---
    public const string id_10k = "com.kheltan.coins_10k";
    public const string id_18k = "com.kheltan.coins_18k";
    public const string id_40k = "com.kheltan.coins_40k";
    public const string id_87k = "com.kheltan.coins_87k";
    public const string id_250k = "com.kheltan.coins_250k";
    public const string id_750k = "com.kheltan.coins_750k";
    public const string id_remove_ads = "com.kheltan.remove_ads";

    void Start()
    {
        if (m_StoreController == null)
        {
            var standardModule = StandardPurchasingModule.Instance();
           
            standardModule.useFakeStoreUIMode = FakeStoreUIMode.StandardUser; 

            var builder = ConfigurationBuilder.Instance(standardModule);

            builder.AddProduct(id_10k, ProductType.Consumable);
            builder.AddProduct(id_18k, ProductType.Consumable);
            builder.AddProduct(id_40k, ProductType.Consumable);
            builder.AddProduct(id_87k, ProductType.Consumable);
            builder.AddProduct(id_250k, ProductType.Consumable);
            builder.AddProduct(id_750k, ProductType.Consumable);
            builder.AddProduct(id_remove_ads, ProductType.NonConsumable);

            UnityPurchasing.Initialize(this, builder);
        }
    }

    //BOTONES DE LA TIENDA

    public void ComprarProducto(string idProducto)
    {
        AudioManager.Instance?.PlayButtonGeneric();
        
        if (idProducto == id_remove_ads && PlayerPrefs.GetInt("NoAds", 0) == 1)
        {
            if (MenuManager.Instance != null)
                MenuManager.Instance.MostrarPopupInfo("Ya eres usuario Premium");
            return;
        }

        idProductoPendiente = idProducto;
        panelConfirmacion.SetActive(true); 
    }

    public void ConfirmarCompra()
    {
        AudioManager.Instance?.PlayButtonGeneric();
        panelConfirmacion.SetActive(false);

        // --- MODO SIMULACIÓN PARA EVALUACIÓN ---
        Debug.Log($"Simulando compra en dispositivo: {idProductoPendiente}");
        
        if (idProductoPendiente == id_10k) EntregarPremio(10000);
        else if (idProductoPendiente == id_18k) EntregarPremio(18000);
        else if (idProductoPendiente == id_40k) EntregarPremio(40000);
        else if (idProductoPendiente == id_87k) EntregarPremio(87000);
        else if (idProductoPendiente == id_250k) EntregarPremio(250000);
        else if (idProductoPendiente == id_750k) EntregarPremio(750000);
        else if (idProductoPendiente == id_remove_ads) ActivarNoAds();
        else Debug.LogError($"[TIENDA] Error: El ID '{idProductoPendiente}' no existe. Revisa el texto escrito en el OnClick del botón en Unity.");
        
        // ANALÍTICAS: Evento purchase_completed
        if (AnalyticsManager.Instance != null && !string.IsNullOrEmpty(idProductoPendiente)) AnalyticsManager.Instance.EventoPurchaseCompleted(idProductoPendiente);

        idProductoPendiente = "";
    }

    public void CancelarCompra()
    {
        AudioManager.Instance?.PlayButtonGeneric();
        idProductoPendiente = "";
        panelConfirmacion.SetActive(false);
    }

    public void VerAnuncioGratis()
    {
        AudioManager.Instance?.PlayButtonGeneric();
        if (AdManager.Instance != null) {
            AdManager.Instance.MostrarAnuncioRecompensado();
        }
    }

    //IAP CALLBACKS MÓVIL
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string idComprado = args.purchasedProduct.definition.id;
        Debug.Log($"¡Compra exitosa del producto: {idComprado}!");

        if (idComprado == id_10k) EntregarPremio(10000);
        else if (idComprado == id_18k) EntregarPremio(18000);
        else if (idComprado == id_40k) EntregarPremio(40000);
        else if (idComprado == id_87k) EntregarPremio(87000);
        else if (idComprado == id_250k) EntregarPremio(250000);
        else if (idComprado == id_750k) EntregarPremio(750000);
        else if (idComprado == id_remove_ads) ActivarNoAds();

        // ANALÍTICAS: Evento purchase_completed (Funnel 2, paso 3)
        if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoPurchaseCompleted(idComprado);

        return PurchaseProcessingResult.Complete;
    }

    private void EntregarPremio(int cantidad)
    {
        AudioManager.Instance?.PlayShopSuccess();
        if (TopBarUI.Instance != null)
        {
            TopBarUI.Instance.ActualizarMonedas(cantidad);
        }
    }
    
    private async void ActivarNoAds()
{
    try 
    {
        AudioManager.Instance?.PlayShopSuccess();
        // 1. Guardamos localmente para que el efecto sea instantáneo
        PlayerPrefs.SetInt("NoAds", 1);
        
        // 2. GUARDAMOS EN LA NUBE (UGS) para que sea permanente por perfil
        var data = new Dictionary<string, object> { { "NoAdsOwned", true } };
        await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(data);
        
        Debug.Log("¡Compra de NoAds sincronizada con tu perfil en la nube!");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error al sincronizar NoAds: {e.Message}");
    }
}

    //MÉTODO OBLIGATORIOS IAP 
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions) 
    {
        m_StoreController = controller;
        Debug.Log("Tienda inicializada.");
    }
    
    public void OnInitializeFailed(InitializationFailureReason error) => Debug.LogError($"Fallo IAP: {error}");
    public void OnInitializeFailed(InitializationFailureReason error, string message) => Debug.LogError($"Fallo IAP: {error} - {message}");
    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
    {
        Debug.LogWarning($"Compra fallida o cancelada: {product.definition.id}. Razón: {reason}");
        idProductoPendiente = ""; 
    }
}