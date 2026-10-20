using UnityEngine;
using UnityEngine.EventSystems;

public sealed class V1HonorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private V1HUD hud; private string condition;
    public void Configure(V1HUD owner,string text){hud=owner;condition=text;}
    public void OnPointerEnter(PointerEventData e){if(hud!=null)hud.HonorTooltip(condition);}
    public void OnPointerExit(PointerEventData e){if(hud!=null)hud.HonorTooltip("将鼠标停在称号上，查看达成条件。");}
    public void OnSelect(BaseEventData e){if(hud!=null)hud.HonorTooltip(condition);}
    public void OnDeselect(BaseEventData e){OnPointerExit(null);}
}
