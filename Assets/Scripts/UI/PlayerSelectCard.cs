using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerSelectCard : MonoBehaviour
{
    public Button selectButton;
    public TMP_Text emptyText;

    public GameObject pickerRoot;
    public Image portrait;
    public TMP_Text nameText;
    public Button arrowLeft;
    public Button arrowRight;
    public Button confirmButton;
    [Tooltip("Texto 'Select Player' da vaga vazia. Vazio = procura o filho 'txt_Player'.")]
    public GameObject titleText;

    public ClassData Confirmed { get; private set; }
    public int Owner { get; private set; } = PlayerInputs.None;
    public bool Occupied => Owner != PlayerInputs.None;
    public event Action Changed;
    public event Action<PlayerSelectCard> MouseJoinRequested;

    ClassData[] classes;
    GameObject[] joinHints; // icones "entre com PS/Xbox" da vaga vazia (btn_PS, btn_Xbox)
    Image confirmGlyph;
    TMP_Text confirmLabel;
    string confirmText;
    int index;

    public void Setup(ClassData[] available)
    {
        classes = available;
        if (titleText == null)
        {
            var t = transform.Find("txt_Player");
            if (t != null) titleText = t.gameObject;
        }
        var hints = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("btn_PS") || child.name.StartsWith("btn_Xbox")) hints.Add(child.gameObject);
        joinHints = hints.ToArray();
        AddConfirmGlyph();
        selectButton.onClick.AddListener(() => MouseJoinRequested?.Invoke(this));
        arrowLeft.onClick.AddListener(() => { if (Owner == PlayerInputs.KeyboardMouse) Move(-1); });
        arrowRight.onClick.AddListener(() => { if (Owner == PlayerInputs.KeyboardMouse) Move(1); });
        confirmLabel = confirmButton.GetComponentInChildren<TMP_Text>(true);
        confirmText = confirmLabel != null ? confirmLabel.text : "Confirmar";
        confirmButton.onClick.AddListener(() =>
        {
            if (Owner == PlayerInputs.KeyboardMouse) ToggleConfirm();
        });
        ShowEmpty();
    }

    // Icone do botao de confirmar do controle de quem ocupa a vaga (mouse/teclado: so o texto).
    void AddConfirmGlyph()
    {
        var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(confirmButton.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(64, 64);
        confirmGlyph = go.GetComponent<Image>();
        confirmGlyph.preserveAspect = true;
        confirmGlyph.raycastTarget = false;
        UpdateConfirmGlyph();
    }

    void UpdateConfirmGlyph()
    {
        Sprite sprite = null;
        foreach (var pad in Gamepad.all)
            if (Occupied && pad.deviceId == Owner) sprite = ControllerGlyphs.Get(GlyphRole.Confirm, pad);
        confirmGlyph.sprite = sprite;
        confirmGlyph.enabled = sprite != null;
        // Com controle, fica so o icone: some a moldura e o texto do botao.
        bool iconOnly = sprite != null;
        if (confirmButton.targetGraphic != null) confirmButton.targetGraphic.enabled = !iconOnly;
        if (confirmLabel != null) confirmLabel.enabled = !iconOnly;
    }

    void SetJoinHints(bool on)
    {
        foreach (var h in joinHints) h.SetActive(on);
    }

    public void Join(int deviceId)
    {
        if (classes == null || classes.Length == 0 || Occupied) return;
        Owner = deviceId;
        Open();
        UpdateConfirmGlyph();
    }

    public void ToggleConfirm()
    {
        if (Confirmed != null) Unconfirm();
        else Confirm();
    }

    // Cancelar: confirmado volta a escolher; escolhendo libera a vaga.
    public void Back()
    {
        if (Confirmed != null) Unconfirm();
        else ShowEmpty();
    }

    public void Move(int step)
    {
        if (!Occupied || Confirmed != null) return;
        index = (index + step + classes.Length) % classes.Length;
        ShowCurrent();
    }

    void ShowEmpty()
    {
        Confirmed = null;
        Owner = PlayerInputs.None;
        UpdateConfirmGlyph();
        SetConfirmLabel(confirmText);
        selectButton.gameObject.SetActive(true);
        SetJoinHints(true);
        emptyText.gameObject.SetActive(true);
        if (titleText != null) titleText.SetActive(true);
        pickerRoot.SetActive(false);
        Changed?.Invoke();
    }

    void Open()
    {
        index = 0;
        selectButton.gameObject.SetActive(false);
        SetJoinHints(false);
        emptyText.gameObject.SetActive(false);
        if (titleText != null) titleText.SetActive(false);
        pickerRoot.SetActive(true);
        arrowLeft.gameObject.SetActive(true);
        arrowRight.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(true);
        ShowCurrent();
        Changed?.Invoke();
    }

    void ShowCurrent()
    {
        portrait.sprite = classes[index].portrait;
        nameText.text = classes[index].displayName;
    }

    void SetConfirmLabel(string text)
    {
        if (confirmLabel != null) confirmLabel.text = text;
    }

    void Confirm()
    {
        if (!Occupied) return;
        Confirmed = classes[index];
        arrowLeft.gameObject.SetActive(false);
        arrowRight.gameObject.SetActive(false);
        SetConfirmLabel("Cancelar");
        Changed?.Invoke();
    }

    void Unconfirm()
    {
        Confirmed = null;
        arrowLeft.gameObject.SetActive(true);
        arrowRight.gameObject.SetActive(true);
        SetConfirmLabel(confirmText);
        Changed?.Invoke();
    }
}
