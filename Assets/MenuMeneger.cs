using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuMeneger : MonoBehaviour
{
    public string abrirplayers;
    public GameObject painelMenuPrincipal;
    public GameObject painelOpcoes;

    Selectable highlighted;
    Vector3 highlightedScale;

    public void jogar()
    {
        SceneManager.LoadScene(abrirplayers);
    }

    public void AbrirOpcoes()
    {
        painelMenuPrincipal.SetActive(false);
        painelOpcoes.SetActive(true);

    }
    public void FecharOpcoes()
    {
        painelOpcoes.SetActive(false);
        painelMenuPrincipal.SetActive(true);

    }

     public void Sair()
    {
        Debug.Log("Saiu do Jogo");
        Application.Quit();

    }

    // Controle/teclado: o EventSystem so navega se algo estiver selecionado.
    void Update()
    {
        var es = EventSystem.current;
        var panel = painelOpcoes.activeInHierarchy ? painelOpcoes
                  : painelMenuPrincipal.activeInHierarchy ? painelMenuPrincipal
                  : null;
        if (es == null || panel == null)
        {
            ClearHighlight();
            return;
        }

        var selected = es.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            foreach (int dev in PlayerInputs.Devices())
            {
                if (!PlayerInputs.Up(dev) && !PlayerInputs.Down(dev) && !PlayerInputs.Left(dev)
                    && !PlayerInputs.Right(dev) && !PlayerInputs.Submit(dev)) continue;
                SelectFirst(es, panel);
                break;
            }
        }

        if (painelOpcoes.activeInHierarchy)
        {
            foreach (int dev in PlayerInputs.Devices())
                if (PlayerInputs.Cancel(dev)) { FecharOpcoes(); break; }
        }

        UpdateHighlight(es.currentSelectedGameObject);
    }

    void SelectFirst(EventSystem es, GameObject panel)
    {
        foreach (var s in panel.GetComponentsInChildren<Selectable>())
        {
            if (!s.IsInteractable() || !s.gameObject.activeInHierarchy) continue;
            es.SetSelectedGameObject(s.gameObject);
            return;
        }
    }

    // A cor de "selecionado" dos botoes quase nao muda, entao aumentamos o botao em foco.
    void UpdateHighlight(GameObject selected)
    {
        var next = selected != null ? selected.GetComponent<Selectable>() : null;
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
}
