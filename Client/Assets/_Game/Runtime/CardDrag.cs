using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Emberwake.Client
{
    public sealed class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        public Action select, cancel;
        public Action<Vector2> deploy, preview;
        RectTransform ghost;
        Canvas canvas;
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=true; }
        public void OnBeginDrag(PointerEventData e)
        {
            select?.Invoke();
            canvas=GetComponentInParent<Canvas>().rootCanvas;
            var go=new GameObject("Dragged card",typeof(RectTransform),typeof(CanvasGroup),typeof(Image));
            ghost=go.GetComponent<RectTransform>();ghost.SetParent(canvas.transform,false);
            ghost.sizeDelta=new Vector2(110,130);
            var pictures=GetComponentsInChildren<Image>();
            var image=go.GetComponent<Image>();image.sprite=pictures.Length>1?pictures[1].sprite:null;image.preserveAspect=true;image.raycastTarget=false;
            go.GetComponent<CanvasGroup>().blocksRaycasts=false;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(!ghost)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local);
            ghost.anchoredPosition=local+new Vector2(65,75);
            preview?.Invoke(e.position);
        }
        public void OnEndDrag(PointerEventData e) { bool wasDragging=ghost;Cleanup();if(wasDragging)deploy?.Invoke(e.position); }
        void Cleanup(){if(ghost)Destroy(ghost.gameObject);ghost=null;}
        void OnApplicationFocus(bool focused){if(!focused){Cleanup();cancel?.Invoke();}}
        void OnDisable(){Cleanup();cancel?.Invoke();}
    }
}

