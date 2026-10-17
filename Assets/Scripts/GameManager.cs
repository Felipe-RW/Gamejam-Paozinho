using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int score = 0;
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            score++;
            Debug.Log("Score: " + score);
        }
    }
}
