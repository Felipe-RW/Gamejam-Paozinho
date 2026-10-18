using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSelectMenu : MonoBehaviour
{
    public PlayerSelectCard[] cards;
    public ClassData[] classes;
    public Button startButton;
    public string battleScene;

    void Start()
    {
        foreach (var card in cards)
        {
            card.Setup(classes);
            card.Changed += UpdateStartButton;
        }
        startButton.onClick.AddListener(StartBattle);
        UpdateStartButton();
    }

    void UpdateStartButton()
    {
        startButton.interactable = cards.Count(c => c.Confirmed != null) >= 1;
    }

    void StartBattle()
    {
        for (int i = 0; i < cards.Length && i < GameSession.SeatClasses.Length; i++)
            GameSession.SeatClasses[i] = cards[i].Confirmed;

        GameSession.HasSelection = true;
        SceneManager.LoadScene(battleScene);
    }
}
