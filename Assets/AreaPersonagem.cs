using UnityEngine;
using UnityEngine.UI;

public class TelaSelecao : MonoBehaviour
{
    [SerializeField] private Slot[] slots;
    [SerializeField] private Button botaoJogar;
    [SerializeField] private int minimoJogadores = 2;

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