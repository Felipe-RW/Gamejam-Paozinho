using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Slot : MonoBehaviour
{
    [Header("Objetos")]
     public GameObject botaoStart;
     public GameObject areaPersonagem;
     public Image imagemPersonagem;
     public TextMeshProUGUI textoNome;

    [Header("Personagens")]
     public Sprite[] sprites;
     public string[] nomes;

    private int indice;

    public bool Ativo { get; private set; }

    private void Start()
    {
        
        botaoStart.SetActive(true);
        areaPersonagem.SetActive(false);
    }


    public void Entrar()
    {
        Ativo = true;
        botaoStart.SetActive(false);
        areaPersonagem.SetActive(true);
        indice = 0;
        Atualizar();
    }

   
    public void Proximo()
    {
        indice = (indice + 1) % sprites.Length;
        Atualizar();
    }

    public void Anterior()
    {
        indice = (indice - 1 + sprites.Length) % sprites.Length;
        Atualizar();
    }

    private void Atualizar()
    {
        imagemPersonagem.sprite = sprites[indice];
        textoNome.text = nomes[indice];
    }
}