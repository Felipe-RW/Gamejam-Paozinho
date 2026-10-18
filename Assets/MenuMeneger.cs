using System.Diagnostics;
using System.Security.Permissions;
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
        
        Application.Quit();
        
    }

}
