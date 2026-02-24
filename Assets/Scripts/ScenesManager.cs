using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenesManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("1 Patient Session SG");
    }

    public void CreateTrajectory()
    {
        SceneManager.LoadScene("0 Theraphist Menu");
    }
}
