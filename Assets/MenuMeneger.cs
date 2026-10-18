using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuMeneger : MonoBehaviour
{
    public string abrirplayers;
    public GameObject painelMenuPrincipal;
    public GameObject painelOpcoes;

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

}
