using UnityEngine;

public class UserMainCameraControl : MonoBehaviour
{
    public float movementSpeed = 5f;
    public float lookSpeed = 2f;

    private float yaw = 0f;
    private float pitch = 0f;

    void Update()
    {
        // 鼠标右键按下时允许视角旋转
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -89f, 89f);  // 防止翻转

            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // 基于当前旋转的方向进行位移
        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) direction += transform.forward;
        if (Input.GetKey(KeyCode.S)) direction -= transform.forward;
        if (Input.GetKey(KeyCode.A)) direction -= transform.right;
        if (Input.GetKey(KeyCode.D)) direction += transform.right;
        if (Input.GetKey(KeyCode.LeftShift)) direction += transform.up;
        if (Input.GetKey(KeyCode.LeftControl)) direction -= transform.up;

        transform.position += direction * movementSpeed * Time.deltaTime;
    }
}
