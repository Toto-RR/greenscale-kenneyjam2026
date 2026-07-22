using UnityEngine;

public class EndGameTrigger : MonoBehaviour
{
    [SerializeField] private string endSceneName = "MainMenu";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() == null) return;
        SceneTransitioner.Load(endSceneName);
    }
}