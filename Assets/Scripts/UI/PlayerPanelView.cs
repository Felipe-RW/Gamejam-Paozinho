using System;
using UnityEngine;
using UnityEngine.UI;

// Liga os pedacos de UI de um jogador (retrato, vida, slots, mao) ao PlayerState.
public class PlayerPanelView : MonoBehaviour
{
    [Header("Referencias (arraste do Canvas)")]
    public Image portrait;
    public Slider healthBar;
    public Image[] slotImages;           // Slot0..2
    public Transform handRoot;

    [Header("Cartas")]
    public CardView cardPrefab;
    public Sprite cardBack;              // verso mostrado nas maos dos outros jogadores

    public event Action<CardView> HandCardClicked;
    public event Action<int> SlotClicked;   // indice do slot

    readonly System.Collections.Generic.List<CardView> handViews = new();
    SlotView[] slots;

    void Awake()
    {
        slots = new SlotView[slotImages.Length];
        for (int i = 0; i < slotImages.Length; i++)
        {
            var slot = slotImages[i].GetComponent<SlotView>();
            if (slot == null) slot = slotImages[i].gameObject.AddComponent<SlotView>();
            slot.Index = i;
            slot.Clicked += idx => SlotClicked?.Invoke(idx);
            slots[i] = slot;
        }
    }

    // isCurrent: mao aberta e clicavel so para o jogador da vez.
    public void Refresh(PlayerState player, bool isCurrent, CardData selected)
    {
        if (portrait != null)
        {
            portrait.sprite = player.classData.portrait;
            portrait.color = !player.IsAlive ? new Color(1, 1, 1, 0.35f)
                           : isCurrent ? Color.white
                           : new Color(0.65f, 0.65f, 0.65f, 1f);
            portrait.transform.localScale = isCurrent ? Vector3.one * 1.15f : Vector3.one;
        }

        healthBar.maxValue = player.classData.maxHealth;
        healthBar.value = player.health;

        for (int i = 0; i < slots.Length; i++)
            slots[i].Show(i < player.slots.Length ? player.slots[i] : null);

        RebuildHand(player, isCurrent, selected);
    }

    void RebuildHand(PlayerState player, bool isCurrent, CardData selected)
    {
        foreach (var v in handViews) if (v != null) Destroy(v.gameObject);
        handViews.Clear();

        foreach (var card in player.hand.Cards)
        {
            var view = Instantiate(cardPrefab, handRoot);
            if (isCurrent)
            {
                view.ShowFront(card, true);
                view.Clicked += v => HandCardClicked?.Invoke(v);
                view.SetSelected(card == selected);
            }
            else
            {
                view.ShowBack(cardBack);
            }
            handViews.Add(view);
        }
    }
}
