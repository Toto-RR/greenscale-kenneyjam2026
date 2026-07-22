using Unity.Cinemachine;
using UnityEngine;

public class EndTutorial : MonoBehaviour
{
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [SerializeField] private Vector3 offsetToApply;


    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (positionComposer != null)
        {
            Debug.Log("Collide!");
            if (collision.collider.GetComponent<PlayerMovement>() == true)
            {
                positionComposer.TargetOffset = offsetToApply;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (positionComposer != null)
        {
            Debug.Log("Collide!");
            if (collision.GetComponent<PlayerMovement>() == true)
            {
                positionComposer.TargetOffset = offsetToApply;
            }
        }
    }
}
