using UnityEngine;

public class LayoutSelector : MonoBehaviour
{
    public GameObject[] canvases = new GameObject[4];
    [Range(1, 4)] public int testPlayerCount = 4;

    void Awake()
    {
        int count = testPlayerCount;
        if (GameSession.HasSelection)
        {
            count = 0;
            foreach (var c in GameSession.SeatClasses) if (c != null) count++;
        }

        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null) canvases[i].SetActive(i == count - 1);
    }
}
