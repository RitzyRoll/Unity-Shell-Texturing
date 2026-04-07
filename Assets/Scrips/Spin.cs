using UnityEngine;

public class Spin : MonoBehaviour
{
    [Range(0f, 1f)]
    public float spinSpeed = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float movement = 20f * spinSpeed * Time.deltaTime;
        transform.Rotate(movement, movement, movement, Space.Self);
    }
}
