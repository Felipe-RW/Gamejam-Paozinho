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
    public event Action Changed;

    ClassData[] classes;
    int index;

    public void Setup(ClassData[] available)
    {
        classes = available;
        selectButton.onClick.AddListener(Open);
        arrowLeft.onClick.AddListener(() => Move(-1));
        arrowRight.onClick.AddListener(() => Move(1));
        confirmButton.onClick.AddListener(Confirm);
        ShowEmpty();
    }

    void ShowEmpty()
    {
        Confirmed = null;
        selectButton.gameObject.SetActive(true);
        emptyText.gameObject.SetActive(true);
        pickerRoot.SetActive(false);
        Changed?.Invoke();
    }

    void Open()
    {
        if (classes == null || classes.Length == 0) return;
        index = 0;
        selectButton.gameObject.SetActive(false);
        emptyText.gameObject.SetActive(false);
        pickerRoot.SetActive(true);
        arrowLeft.gameObject.SetActive(true);
        arrowRight.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(true);
        ShowCurrent();
    }

    void Move(int step)
    {
        index = (index + step + classes.Length) % classes.Length;
        ShowCurrent();
    }

    void ShowCurrent()
    {
        portrait.sprite = classes[index].portrait;
        nameText.text = classes[index].displayName;
    }

    void Confirm()
    {
        Confirmed = classes[index];
        arrowLeft.gameObject.SetActive(false);
        arrowRight.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        Changed?.Invoke();
    }
}
