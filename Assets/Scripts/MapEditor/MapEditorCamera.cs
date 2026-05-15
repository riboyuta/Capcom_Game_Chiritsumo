using UnityEngine;


public class CameraController : MonoBehaviour
{
    public float speed = 40.0f;
    public float dushCoef = 2.0f;
    public float doubleTapThreshold = 0.3f; // É_ÉuÉãàµÇ¢Ç…Ç»ÇÈóPó\

    private float lastPressTime = 0f;
    private bool isDush = false;
    private KeyCode lastKey = KeyCode.None;


    void Update()
    {
        HandleInput();

        Move();
    }


    void HandleInput()
    {
        CheckDoubleTap(KeyCode.LeftArrow);
        CheckDoubleTap(KeyCode.RightArrow);
        CheckDoubleTap(KeyCode.UpArrow);
        CheckDoubleTap(KeyCode.DownArrow);
        CheckDoubleTap(KeyCode.LeftShift);
        CheckDoubleTap(KeyCode.RightShift);
    }

    void Move()
    {
        float x = 0;
        float y = 0;
        float z = 0;

        if (Input.GetKey(KeyCode.LeftArrow)) x = -1;
        else if (Input.GetKey(KeyCode.RightArrow)) x = 1;

        if (Input.GetKey(KeyCode.UpArrow)) y = 1;
        else if (Input.GetKey(KeyCode.DownArrow)) y = -1;

        if (Input.GetKey(KeyCode.RightShift)) z = 1;
        else if (Input.GetKey(KeyCode.LeftShift)) z = -1;

        Vector3 move = new Vector3(x, y, z);

        float currentSpeed = isDush ? speed * dushCoef : speed;

        transform.position += move * currentSpeed * Time.deltaTime;
    }


    void CheckDoubleTap(KeyCode key)
    {
        if (Input.GetKeyDown(key))
        {
            // ìØÇ∂ÉLÅ[Ç©Ç¬íZéûä‘
            if (lastKey == key && Time.time - lastPressTime < doubleTapThreshold)
            {
                isDush = true;
            }

            lastKey = key;
            lastPressTime = Time.time;
        }

        if (Input.GetKeyUp(key))
        {
            isDush = false;
        }
    }
}