using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Animator leftDancer;
    [SerializeField] private Animator rightDancer;
    [SerializeField] private string gameSceneName = "MainScene";

    private void Start()
    {
        if (leftDancer != null)
        {
            leftDancer.SetBool("Dance 1", true);
            leftDancer.Play("Dance 1", 0, 0f);
        }
        if (rightDancer != null)
        {
            rightDancer.SetBool("Dance 2", true);
            rightDancer.Play("Dance 2", 0, 0f);
        }
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
