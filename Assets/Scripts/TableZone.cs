using UnityEngine;
using UnityEngine.EventSystems;

public class TableZone : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Solo hacemos algo si hay una carta seleccionada esperando moverse
        if (InteractionManager.Instance.HasCardSelected())
        {
            UICard cardToMove = InteractionManager.Instance.SelectedCard;

            // --- 1. CONGELAR TAMAÑO (Lógica Visual) ---
            CardResizer resizer = cardToMove.GetComponent<CardResizer>();
            Vector3 finalVisualScale = Vector3.one;

            if (resizer != null)
            {
                // Guardamos el tamaño actual de los dibujos internos
                finalVisualScale = resizer.targetVisuals.localScale;
                // Apagamos el redimensionador automático
                resizer.enabled = false; 
            }

            // --- 2. MOVER Y POSICIONAR (Lógica Física) ---
            cardToMove.transform.SetParent(this.transform);
            cardToMove.transform.localPosition = Vector3.zero; // Al centro de la pila
            cardToMove.transform.localScale = Vector3.one;     // Escala del contenedor a 1

            // Restauramos el tamaño de los dibujos internos
            if (resizer != null)
            {
                resizer.targetVisuals.localScale = finalVisualScale;
            }

            // (Opcional) Restaurar color por si se quedó amarillo
            cardToMove.GetComponent<UnityEngine.UI.Image>().color = Color.white;

            // --- 3. NUEVO: BLOQUEAR INTERACCIÓN (Lógica de Estado) ---
            // Buscamos o añadimos un CanvasGroup para controlar si recibe clics
            CanvasGroup group = cardToMove.GetComponent<CanvasGroup>();
            if (group == null)
            {
                // Si la carta no tenía este componente, se lo ponemos ahora mismo por código
                group = cardToMove.gameObject.AddComponent<CanvasGroup>();
            }
            
            // ¡AQUÍ ESTÁ LA CLAVE!
            // Al poner esto en false, el ratón ignorará esta carta para siempre.
            group.blocksRaycasts = false; 

            // --- 4. FINALIZAR ---
            InteractionManager.Instance.ClearSelection();
        }
    }
}