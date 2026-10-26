using System;
using TMPro;
using UnityEngine;
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

    public ClassData Confirmed { get; private set; }
    public int Owner { get; private set; } = PlayerInputs.None;
    public bool Occupied => Owner != PlayerInputs.None;
    public event Action Changed;
    public event Action<PlayerSelectCard> MouseJoinRequested;

    ClassData[] classes;
    TMP_Text confirmLabel;
    string confirmText;
    int index;

    public void Setup(ClassData[] available)
    {
        classes = available;
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

    public void Join(int deviceId)
    {
        if (classes == null || classes.Length == 0 || Occupied) return;
        Owner = deviceId;
        Open();
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
        SetConfirmLabel(confirmText);
        selectButton.gameObject.SetActive(true);
        emptyText.gameObject.SetActive(true);
        pickerRoot.SetActive(false);
        Changed?.Invoke();
    }

    void Open()
    {
        index = 0;
        selectButton.gameObject.SetActive(false);
        emptyText.gameObject.SetActive(false);
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
