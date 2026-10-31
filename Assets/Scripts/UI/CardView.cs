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
    bool selected, focused;

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

    public void SetSelected(bool value)
    {
        selected = value;
        ApplyScale();
    }

    // Foco (teclado/controle) = contorno amarelo. Selecao = carta maior. Visuais diferentes para nao confundir.
    public void SetFocused(bool value)
    {
        if (focused == value) return;
        focused = value;

        var outline = GetComponent<Outline>();
        if (outline == null && value)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = false;
        }
        if (outline != null) outline.enabled = value;
    }

    void ApplyScale()
    {
        transform.localScale = selected ? baseScale * 1.15f : baseScale;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);
}
