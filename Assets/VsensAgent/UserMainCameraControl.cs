using UnityEngine;

namespace VsensAgent
{
    public class UserMainCameraControl : MonoBehaviour
    {
        [Header("相机控制设置")]
        public float movementSpeed = 5f;
        public float lookSpeed = 2f;
        
        [Header("输入控制")]
        public bool inputEnabled = true;  // 外部控制输入是否启用
        
        private float yaw = 0f;
        private float pitch = 0f;
        
        void Update()
        {
            // 检查输入是否被禁用
            if (!inputEnabled || !enabled)
            {
                return;
            }
            
            // 检查是否有UI输入框被聚焦
            if (IsUIInputFocused())
            {
                return;
            }

            // 鼠标右键按下时允许视角旋转
            if (Input.GetMouseButton(1))
            {
                HandleMouseLook();
            }

            // 处理键盘移动输入
            HandleKeyboardMovement();
        }
        
        /// <summary>
        /// 检查是否有UI输入框被聚焦
        /// </summary>
        private bool IsUIInputFocused()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                var inputField = eventSystem.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>();
                return inputField != null;
            }
            return false;
        }
        
        /// <summary>
        /// 处理鼠标视角控制
        /// </summary>
        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -89f, 89f);  // 防止翻转

            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
        
        /// <summary>
        /// 处理键盘移动输入
        /// </summary>
        private void HandleKeyboardMovement()
        {
            // 基于当前旋转的方向进行位移
            Vector3 direction = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) direction += transform.forward;
            if (Input.GetKey(KeyCode.S)) direction -= transform.forward;
            if (Input.GetKey(KeyCode.A)) direction -= transform.right;
            if (Input.GetKey(KeyCode.D)) direction += transform.right;
            if (Input.GetKey(KeyCode.Space)) direction += transform.up;
            if (Input.GetKey(KeyCode.LeftShift)) direction -= transform.up;

            if (direction != Vector3.zero)
            {
                transform.position += direction * movementSpeed * Time.deltaTime;
            }
        }
        
        /// <summary>
        /// 外部控制输入启用/禁用
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        /// <summary>
        /// 返回 Main Camera 的位置和朝向
        /// </summary>
        public CameraTransformInfo GetCameraTransformInfo()
        {
            return new CameraTransformInfo()
            {
                position = transform.position,
                rotation = transform.eulerAngles
            };
        }
    }

    /// <summary>
    /// 传输用的数据结构
    /// </summary>
    [System.Serializable]
    public struct CameraTransformInfo
    {
        public Vector3 position;
        public Vector3 rotation;
    }
}