using System.Diagnostics;
using System.Security.Permissions;
using UnityEngine;

public class MenuMeneger : MonoBehaviour
{
    public GameObject painelMenuPrincipal;
    public GameObject painelOpcoes;

    public void jogar()
    {
        
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
