using UnityEngine;
using UnityEngine.UI;

public class AreaPersonagem : MonoBehaviour
{
    public Slot[] slots;
    public Button botaoJogar;
    public int minimoJogadores = 1;

    private void Update()
    {
        botaoJogar.interactable = ContarJogadores() >= minimoJogadores;
    }

    private int ContarJogadores()
    {
        int total = 0;
        foreach (Slot slot in slots)
        {
            if (slot.Ativo)
                total++;
        }
        return total;
    }

    // Ligar no BotaoJogar
    public void Jogar()
    {
        Debug.Log("Jogo iniciado com " + ContarJogadores() + " jogadores");
        
    }
}