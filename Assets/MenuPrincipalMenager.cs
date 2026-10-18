using System.Diagnostics;
using System.Security.Permissions;
using UnityEngine;

public class MenuMenager : MonoBehaviour
{
    public GameObjetc painelMenuPrincipal;
    public GameObjetc painelOpcoes;

    void jogar()
    {
        
    }
    
    void AbrirOpcoes()
    {
        painelMenuPrincipal.SetActive(false);
        painelOpcoes.SetActive(true);
        
    }
     void FecharOpcoes()
    {
        painelOpcoes.SetActive(false);
        painelMenuPrincipal.SetActive(true);
        
    }

     void Sair()
    {
        Debug.Log("Sair do jogo");
        Application.Quit();
        
    }

}
