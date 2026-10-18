using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour, IPointerClickHandler
{
    public CardData Data { get; private set; }
    public event Action<CardView> Clicked;

    Image image;
    Vector3 baseScale;

    void Awake()
    {
        image = GetComponent<Image>();
        baseScale = transform.localScale;
    }

    public void ShowFront(CardData card, bool interactable)
    {
        Data = card;
        image.sprite = card.sprite;
        image.raycastTarget = interactable;
    }

    public void ShowBack(Sprite back)
    {
        Data = null;
        image.sprite = back;
        image.raycastTarget = false;
    }

    public void SetSelected(bool selected)
    {
        transform.localScale = selected ? baseScale * 1.15f : baseScale;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);
}
