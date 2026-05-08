using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Function to start the game
    public void PlayGame()
    {
        // Loads the next scene in the build index
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // Function to exit the game
    public void QuitGame()
    {
    // This code only runs when testing inside the Unity Editor
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        // This code runs in the actual built application (.exe, .app, etc.)
        Application.Quit();
    #endif
    }

    // Drag your "HowToPlayWindow" GameObject here in the Inspector
    public GameObject instructionWindow;

    public void OpenInstructions()
    {
        if (instructionWindow != null)
        {
            instructionWindow.SetActive(true);
        }
    }

    public void CloseInstructions()
    {
        if (instructionWindow != null)
        {
            instructionWindow.SetActive(false);
        }
    }
}