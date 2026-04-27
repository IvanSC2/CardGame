#pragma warning disable 0618
using UnityEngine;
using UnityEngine.Purchasing;

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

    // Botones de pago 
    public void ComprarProducto(string idProducto)
    {
        idProductoPendiente = idProducto;
        panelConfirmacion.SetActive(true); 
    }

    // Botón Aceptar de tu panel
    public void ConfirmarCompra()
    {
        panelConfirmacion.SetActive(false);

#if UNITY_EDITOR
        // --- MODO ORDENADOR: Bypass directo ---
        Debug.Log($"Simulando compra en PC: {idProductoPendiente}");
        
        if (idProductoPendiente == id_10k) EntregarPremio(10000);
        else if (idProductoPendiente == id_18k) EntregarPremio(18000);
        else if (idProductoPendiente == id_40k) EntregarPremio(40000);
        else if (idProductoPendiente == id_87k) EntregarPremio(87000);
        else if (idProductoPendiente == id_250k) EntregarPremio(250000);
        else if (idProductoPendiente == id_750k) EntregarPremio(750000);
        else if (idProductoPendiente == id_remove_ads) ActivarNoAds();
        
        idProductoPendiente = "";
#else
        // MODO MÓVIL
        if (m_StoreController != null)
        {
            m_StoreController.InitiatePurchase(idProductoPendiente);
        }
#endif
    }

    // Botón Cancelar de tu panel
    public void CancelarCompra()
    {
        idProductoPendiente = "";
        panelConfirmacion.SetActive(false);
    }

    public void VerAnuncioGratis()
    {
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

        return PurchaseProcessingResult.Complete;
    }

    private void EntregarPremio(int cantidad)
    {
        if (TopBarUI.Instance != null)
        {
            TopBarUI.Instance.ActualizarMonedas(cantidad);
        }
    }
    
    private void ActivarNoAds()
    {
        PlayerPrefs.SetInt("NoAds", 1);
        PlayerPrefs.Save();
        Debug.Log("¡Publicidad eliminada de forma permanente!");
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