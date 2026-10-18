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
        System.Array.Clear(GameSession.SeatClasses, 0, GameSession.SeatClasses.Length);
        int next = 0;
        foreach (var card in cards)
        {
            if (card.Confirmed == null || next >= GameSession.SeatClasses.Length) continue;
            GameSession.SeatClasses[next++] = card.Confirmed;
        }

        GameSession.HasSelection = true;
        SceneManager.LoadScene(battleScene);
    }
}
