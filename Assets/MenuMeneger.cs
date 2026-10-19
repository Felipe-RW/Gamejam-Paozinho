using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuMeneger : MonoBehaviour
{
    public string abrirplayers;
    public GameObject painelMenuPrincipal;
    public GameObject painelOpcoes;
    public GameObject painelRegras;

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

    public void AbrirRegras()
    {
        painelRegras.SetActive(true);
        painelMenuPrincipal.SetActive(false);

    }
    public void FecharRegras()
    {
        painelMenuPrincipal.SetActive(true);
        painelRegras.SetActive(false);
    }


     public void Sair()
    {
        Debug.Log("Saiu do Jogo");
        Application.Quit();
        
    }

}
