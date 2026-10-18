using System.Diagnostics;
using System.Security.Permissions;
using UnityEngine;

public class MenuMenager : MonoBehaviour
{
    [SerializeField] private GameObjetc painelMenuPrincipal;
    [SerializeField] private GameObjetc painelOpcoes;

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
        Debug.Log("Sair do jogo");
        Application.Quit();
        
    }
}
