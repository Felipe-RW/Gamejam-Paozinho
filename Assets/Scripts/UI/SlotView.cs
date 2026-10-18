using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Um slot de carta na coluna do meio. Adicionado automaticamente pelo PlayerPanelView.
[RequireComponent(typeof(Image))]
public class SlotView : MonoBehaviour, IPointerClickHandler
{
    public int Index { get; set; }
    public event Action<int> Clicked;

    Image image;
    Sprite emptySprite;

    void Awake()
    {
        image = GetComponent<Image>();
        emptySprite = image.sprite;
    }

    public void Show(CardData card)
    {
        image.sprite = card != null ? card.sprite : emptySprite;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(Index);
}
