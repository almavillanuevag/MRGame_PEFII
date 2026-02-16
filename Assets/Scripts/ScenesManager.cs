using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenesManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("FullGame v1");
    }

    public void CreateTrajectory()
    {
        SceneManager.LoadScene("Dibujos2");
    }
}
