using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSelectMenu : MonoBehaviour
{
    public PlayerSelectCard[] cards;
    public ClassData[] classes;
    public Button startButton;
    public Button closeButton;
    public string battleScene;
    [Tooltip("Cena do prologo/tutorial, tocada sempre antes da batalha (com opcao de pular). Vazio = sem prologo.")]
    public string prologueScene = "Prologo";

    bool starting;

    void Start()
    {
        foreach (var card in cards)
        {
            card.Setup(classes);
            card.Changed += UpdateStartButton;
            card.MouseJoinRequested += OnMouseJoin;
        }
        startButton.onClick.AddListener(OnStartClicked);
        UpdateStartButton();
    }

    // Sem isso o controle "clica" no ultimo botao selecionado pelo mouse.
    void OnEnable()
    {
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
    }

    void OnDisable()
    {
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }

    void Update()
    {
        if (starting) return;

        bool backed = false;
        foreach (var card in cards)
        {
            if (!card.Occupied) continue;
            int dev = card.Owner;

            if (PlayerInputs.Left(dev)) card.Move(-1);
            if (PlayerInputs.Right(dev)) card.Move(1);
            if (PlayerInputs.Cancel(dev)) { card.Back(); backed = true; continue; }

            if (PlayerInputs.Submit(dev))
            {
                if (card.Confirmed == null) card.ToggleConfirm();
                else if (startButton.interactable) { StartBattle(); return; }
            }
            else if (PlayerInputs.Start(dev) && startButton.interactable)
            {
                StartBattle();
                return;
            }
        }

        // Ninguem na mesa: B volta ao menu principal.
        if (!backed && closeButton != null && cards.All(c => !c.Occupied))
        {
            foreach (int dev in PlayerInputs.Devices())
                if (PlayerInputs.Cancel(dev)) { closeButton.onClick.Invoke(); return; }
        }

        foreach (int dev in PlayerInputs.Devices())
        {
            if (cards.Any(c => c.Owner == dev)) continue;
            if (!PlayerInputs.Submit(dev) && !PlayerInputs.Start(dev)) continue;

            var free = cards.FirstOrDefault(c => !c.Occupied);
            if (free == null) break;
            free.Join(dev);
        }
    }

    void OnMouseJoin(PlayerSelectCard card)
    {
        if (card.Occupied || cards.Any(c => c.Owner == PlayerInputs.KeyboardMouse)) return;
        card.Join(PlayerInputs.KeyboardMouse);
    }

    void UpdateStartButton()
    {
        bool anyConfirmed = cards.Any(c => c.Confirmed != null);
        bool allReady = cards.All(c => !c.Occupied || c.Confirmed != null);
        startButton.interactable = anyConfirmed && allReady;
    }

    void OnStartClicked()
    {
        if (cards.Any(c => c.Owner == PlayerInputs.KeyboardMouse)) StartBattle();
    }

    void StartBattle()
    {
        if (starting) return;
        starting = true;

        System.Array.Clear(GameSession.SeatClasses, 0, GameSession.SeatClasses.Length);
        for (int i = 0; i < GameSession.SeatDevices.Length; i++) GameSession.SeatDevices[i] = PlayerInputs.None;

        int next = 0;
        foreach (var card in cards)
        {
            if (card.Confirmed == null || next >= GameSession.SeatClasses.Length) continue;
            GameSession.SeatClasses[next] = card.Confirmed;
            GameSession.SeatDevices[next] = card.Owner;
            next++;
        }

        GameSession.HasSelection = true;
        SceneManager.LoadScene(ShouldPlayPrologue() ? prologueScene : battleScene);
    }

    bool ShouldPlayPrologue()
    {
        if (string.IsNullOrEmpty(prologueScene)) return false;
        return Application.CanStreamedLevelBeLoaded(prologueScene); // false = cena fora do Build Settings
    }
}
