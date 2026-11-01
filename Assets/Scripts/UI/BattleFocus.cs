using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Navegacao por foco (d-pad/setas) para quem joga com controle ou teclado.
// So o dispositivo dono do jogador da vez controla a batalha.
// O foco so aparece depois que uma tecla/botao de navegacao e usado e some quando o mouse e usado.
public class BattleFocus : MonoBehaviour
{
    enum Kind { Hand, Slots, EndTurn, Enemies }

    struct Row
    {
        public Kind kind;
        public int player;
        public int count;
    }

    BattleUI ui;
    GameManager gm;
    RectTransform cursor;
    PlayerPanelView handPanel;
    Selectable highlighted;
    Vector3 highlightedScale;
    PlayerState lastPlayer;
    readonly List<Row> rows = new();
    readonly List<int> aliveEnemies = new();
    readonly Vector3[] corners = new Vector3[4];
    int row, col;
    bool wasChoosing;
    bool navActive;

    public void Init(BattleUI battleUI, GameManager manager)
    {
        ui = battleUI;
        gm = manager;
        BuildCursor();
    }

    void BuildCursor()
    {
        var canvas = ui.EndTurnButton.GetComponentInParent<Canvas>().rootCanvas;
        var go = new GameObject("FocusCursor", typeof(RectTransform), typeof(Image), typeof(Outline));
        cursor = (RectTransform)go.transform;
        cursor.SetParent(canvas.transform, false);
        cursor.anchorMin = cursor.anchorMax = cursor.pivot = new Vector2(0.5f, 0.5f);

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 0.9f, 0.2f, 0.25f);
        image.raycastTarget = false;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
        outline.effectDistance = new Vector2(4f, 4f);

        go.SetActive(false);
    }

    IEnumerable<int> Joined()
    {
        foreach (int dev in ui.PlayerDevices)
        {
            if (dev == PlayerInputs.None)
            {
                foreach (int any in PlayerInputs.Devices()) yield return any;
                yield break;
            }
            yield return dev;
        }
    }

    bool Pressed(Func<int, bool> check)
    {
        int dev = ui.CurrentDevice;
        if (dev != PlayerInputs.None) return check(dev);
        foreach (int any in PlayerInputs.Devices())
            if (check(any)) return true;
        return false;
    }

    void Update()
    {
        if (gm.Players.Count == 0) { Hide(); return; }

        bool menuOpen = gm.menuPanel != null && gm.menuPanel.activeSelf;
        bool defeatOpen = gm.defeatPanel != null && gm.defeatPanel.activeSelf;
        UpdatePanelNavigation(menuOpen ? gm.menuPanel : defeatOpen ? gm.defeatPanel : null);

        foreach (int dev in Joined())
        {
            if (PlayerInputs.Start(dev)) { gm.ToggleMenu(); return; }
            if (menuOpen && (PlayerInputs.Select(dev) || PlayerInputs.Cancel(dev))) { gm.ToggleMenu(); return; }
        }

        if (gm.Phase != GamePhase.PlayerTurn || menuOpen) { HideAll(); return; }

        var current = gm.CurrentPlayer;
        if (current != lastPlayer)
        {
            lastPlayer = current;
            row = col = 0;
        }

        bool choosing = ui.IsChoosingTarget;
        if (choosing != wasChoosing)
        {
            wasChoosing = choosing;
            if (choosing) row = col = 0;
        }

        BuildRows(current);
        row = Mathf.Clamp(row, 0, rows.Count - 1);
        col = Mathf.Clamp(col, 0, rows[row].count - 1);

        if (MouseUsed()) navActive = false;
        if (!navActive)
        {
            if (!NavPressed()) { HideAll(); return; }
            navActive = true;
            // Seta/confirmar so mostram o foco onde ele esta (sem mover nem confirmar algo invisivel).
            // Cancelar age direto, para desfazer uma selecao feita com o mouse.
            if (!Pressed(PlayerInputs.Cancel)) { Place(current); return; }
        }

        if (ReadInput(current)) return;
        Place(current);
    }

    bool NavPressed() =>
        Pressed(PlayerInputs.Up) || Pressed(PlayerInputs.Down) || Pressed(PlayerInputs.Left) ||
        Pressed(PlayerInputs.Right) || Pressed(PlayerInputs.Submit) || Pressed(PlayerInputs.Cancel);

    // Mouse mexido ou clicado durante a vez de quem joga com teclado/mouse.
    bool MouseUsed()
    {
        int dev = ui.CurrentDevice;
        if (dev != PlayerInputs.None && dev != PlayerInputs.KeyboardMouse) return false;

        var mouse = Mouse.current;
        if (mouse == null) return false;
        return mouse.delta.ReadValue().sqrMagnitude > 4f
            || mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.scroll.ReadValue().sqrMagnitude > 0f;
    }

    void HideAll()
    {
        Hide();
        SetHandFocus(null, -1);
        SetEnemyFocus(-1);
    }

    // Paineis de pausa/derrota: liga a navegacao do EventSystem so enquanto estao abertos.
    void UpdatePanelNavigation(GameObject panel)
    {
        var es = EventSystem.current;
        if (es == null) return;

        es.sendNavigationEvents = panel != null;
        if (panel == null) { ClearHighlight(); return; }

        var selected = es.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(panel.transform))
        {
            foreach (var s in panel.GetComponentsInChildren<Selectable>())
            {
                if (!s.IsInteractable() || !s.gameObject.activeInHierarchy) continue;
                es.SetSelectedGameObject(s.gameObject);
                break;
            }
        }

        var next = es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
        if (next == highlighted) return;
        ClearHighlight();
        if (next == null) return;
        highlighted = next;
        highlightedScale = next.transform.localScale;
        next.transform.localScale = highlightedScale * 1.1f;
    }

    void ClearHighlight()
    {
        if (highlighted != null) highlighted.transform.localScale = highlightedScale;
        highlighted = null;
    }

    void BuildRows(PlayerState current)
    {
        rows.Clear();

        // Escolhendo o alvo de uma carta de Espadas: so os inimigos vivos ficam navegaveis.
        if (ui.IsChoosingTarget)
        {
            aliveEnemies.Clear();
            for (int i = 0; i < gm.Enemies.Count; i++)
                if (gm.Enemies[i].IsAlive && ui.EnemyTargetRect(i) != null) aliveEnemies.Add(i);
            if (aliveEnemies.Count > 0)
            {
                rows.Add(new Row { kind = Kind.Enemies, player = -1, count = aliveEnemies.Count });
                return;
            }
        }

        if (current.hand.Cards.Count > 0)
            rows.Add(new Row { kind = Kind.Hand, player = -1, count = current.hand.Cards.Count });

        for (int p = 0; p < gm.Players.Count; p++)
        {
            if (!gm.Players[p].IsAlive) continue;
            rows.Add(new Row { kind = Kind.Slots, player = p, count = gm.Players[p].slots.Length });
        }

        rows.Add(new Row { kind = Kind.EndTurn, player = -1, count = 1 });
    }

    // true quando uma acao foi executada (a tela sera reconstruida).
    bool ReadInput(PlayerState current)
    {
        int n = rows.Count;
        if (Pressed(PlayerInputs.Up)) { row = (row - 1 + n) % n; col = Mathf.Clamp(col, 0, rows[row].count - 1); }
        if (Pressed(PlayerInputs.Down)) { row = (row + 1) % n; col = Mathf.Clamp(col, 0, rows[row].count - 1); }
        if (Pressed(PlayerInputs.Left)) col = (col - 1 + rows[row].count) % rows[row].count;
        if (Pressed(PlayerInputs.Right)) col = (col + 1) % rows[row].count;

        if (Pressed(PlayerInputs.Cancel) && ui.IsChoosingTarget)
        {
            ui.CancelTarget();
            wasChoosing = false;
            BuildRows(current);
            FocusOwnSlots(current);
            return true;
        }

        if (Pressed(PlayerInputs.Cancel) && ui.Selected != null)
        {
            ui.ClearSelection();
            row = 0;
            return true;
        }

        if (!Pressed(PlayerInputs.Submit)) return false;

        var r = rows[row];
        switch (r.kind)
        {
            case Kind.Hand:
                ui.ActivateHandCard(current.hand.Cards[col]);
                if (ui.Selected != null) FocusOwnSlots(current);
                break;
            case Kind.Slots:
                ui.ActivateSlot(r.player, col);
                if (ui.Selected == null) row = 0;
                break;
            case Kind.EndTurn:
                if (ui.EndTurnButton.interactable) ui.EndTurnPressed();
                break;
            case Kind.Enemies:
                ui.ActivateEnemy(aliveEnemies[col]);
                row = 0;
                break;
        }
        return true;
    }

    void SetEnemyFocus(int index)
    {
        if (ui.FocusedEnemy == index) return;
        ui.FocusedEnemy = index;
        ui.RefreshEnemyFocus();
    }

    void FocusOwnSlots(PlayerState current)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].kind != Kind.Slots || gm.Players[rows[i].player] != current) continue;
            row = i;
            col = Mathf.Max(0, current.FreeSlotIndex());
            return;
        }
    }

    void Place(PlayerState current)
    {
        RectTransform target = null;
        var r = rows[row];
        SetEnemyFocus(r.kind == Kind.Enemies ? aliveEnemies[col] : -1);
        switch (r.kind)
        {
            case Kind.Enemies:
                target = ui.EnemyTargetRect(aliveEnemies[col]);
                break;
            case Kind.Hand:
                // Carta em foco fica maior (sem highlight), como no hover do mouse.
                SetHandFocus(ui.GetPanel(current), col);
                Hide();
                return;
            case Kind.Slots:
                var owner = ui.GetPanel(gm.Players[r.player]);
                if (owner != null) target = owner.GetSlotRect(col);
                break;
            case Kind.EndTurn:
                target = (RectTransform)ui.EndTurnButton.transform;
                break;
        }

        if (target == null) { Hide(); return; }

        SetHandFocus(null, -1);
        target.GetWorldCorners(corners);
        var scale = cursor.parent.lossyScale;
        cursor.gameObject.SetActive(true);
        cursor.SetAsLastSibling();
        cursor.position = (corners[0] + corners[2]) * 0.5f;
        cursor.sizeDelta = new Vector2(
            (corners[2].x - corners[0].x) / scale.x,
            (corners[2].y - corners[0].y) / scale.y);
    }

    void SetHandFocus(PlayerPanelView panel, int index)
    {
        if (handPanel != null && handPanel != panel)
            foreach (var v in handPanel.HandViews) if (v != null) v.SetFocused(false);

        handPanel = panel;
        if (panel == null) return;
        for (int i = 0; i < panel.HandViews.Count; i++)
            if (panel.HandViews[i] != null) panel.HandViews[i].SetFocused(i == index);
    }

    void Hide()
    {
        if (cursor != null && cursor.gameObject.activeSelf) cursor.gameObject.SetActive(false);
    }
}
