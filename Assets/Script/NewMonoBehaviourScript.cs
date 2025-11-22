using UnityEngine;

public class RotateZ : MonoBehaviour
{
    public float speed = 100f;

    void Update()
    {
        transform.Rotate(0, speed * Time.deltaTime, 0);
    }
}