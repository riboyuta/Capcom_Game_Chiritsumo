using UnityEngine;


public class CameraController : MonoBehaviour
{
    public float speed = 10f;

    void Update()
    {
        float x = 0;
        float y = 0;
        float z = 0;

        if (Input.GetKey(KeyCode.LeftArrow)) x = -1;
        if (Input.GetKey(KeyCode.RightArrow)) x = 1;
        if (Input.GetKey(KeyCode.UpArrow)) y = 1;
        if (Input.GetKey(KeyCode.DownArrow)) y = -1;

        if (Input.GetKey(KeyCode.RightShift)) z = 1;
        if (Input.GetKey(KeyCode.LeftShift)) z = -1;

        //if (Input.GetKey(KeyCode.A)) x = -1;
        //if (Input.GetKey(KeyCode.D)) x = 1;
        //if (Input.GetKey(KeyCode.W)) y = 1;
        //if (Input.GetKey(KeyCode.S)) y = -1;

        if (Input.GetKey(KeyCode.Space))
        {
            transform.position = new Vector3(0, 1, -10);
        }



        Vector3 move = new Vector3(x, y, z);

        transform.position += move * speed * Time.deltaTime;
    }
}