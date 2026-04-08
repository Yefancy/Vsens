using UnityEngine;
using VsensAgent.Core;

namespace VsensAgent
{
    public class UserMainCameraControl : MonoBehaviour
    {
        // 相机模式枚举
        public enum CameraMode
        {
            FirstPerson,  // 第一人称模式
            GodView       // 上帝视角模式（45度俯视）
        }
        
        [Header("相机模式设置")]
        public CameraMode currentMode = CameraMode.FirstPerson;
        public KeyCode toggleModeKey = Constants.InputKeys.TOGGLE_VIEW;  // 切换视角快捷键
        
        [Header("第一人称设置")]
        public float firstPersonMovementSpeed = Constants.Camera.FIRST_PERSON_MOVEMENT_SPEED;
        public float firstPersonLookSpeed = Constants.Camera.FIRST_PERSON_LOOK_SPEED;
        
        [Header("上帝视角设置")]
        public float godViewMovementSpeed = Constants.Camera.GOD_VIEW_MOVEMENT_SPEED;
        public float godViewLookSpeed = Constants.Camera.GOD_VIEW_LOOK_SPEED;
        public float godViewDistance = Constants.Camera.GOD_VIEW_DISTANCE;         // 上帝视角默认距离
        public float godViewAngle = Constants.Camera.GOD_VIEW_ANGLE;            // 俯视角度
        public float godViewScrollSpeed = Constants.Camera.GOD_VIEW_SCROLL_SPEED;       // 鼠标滚轮缩放速度
        public float godViewMinDistance = Constants.Camera.GOD_VIEW_MIN_DISTANCE;       // 最小距离
        public float godViewMaxDistance = Constants.Camera.GOD_VIEW_MAX_DISTANCE;      // 最大距离
        
        [Header("模式切换设置")]
        public float transitionSpeed = Constants.Camera.TRANSITION_SPEED;          // 模式切换过渡速度
        public bool smoothTransition = true;        // 是否启用平滑过渡
        
        [Header("输入控制")]
        public bool inputEnabled = true;            // 外部控制输入是否启用
        [SerializeField] private float lookSpeedMultiplier = 1.2f;
        
        // 第一人称私有变量
        private float firstPersonYaw = 0f;
        private float firstPersonPitch = 0f;
        private Vector3 savedFirstPersonPosition;
        private Quaternion savedFirstPersonRotation;
        
        // 上帝视角私有变量
        private Vector3 savedGodViewPosition;
        private Vector3 godViewTargetPoint;         // 上帝视角观察的目标点
        private float savedGodViewYaw = 0f;
        private float currentGodViewDistance;       // 相机到目标点的距离
        
        // 过渡状态
        private bool isTransitioning = false;
        private Vector3 transitionStartPos;
        private Quaternion transitionStartRot;
        private Vector3 transitionTargetPos;
        private Quaternion transitionTargetRot;
        private float transitionProgress = 0f;
        
        void Start()
        {
            // 注册到服务定位器
            ServiceLocator.Register<UserMainCameraControl>(this);
            
            // 初始化当前距离
            currentGodViewDistance = godViewDistance;
            
            // 保存初始第一人称位置
            savedFirstPersonPosition = transform.position;
            savedFirstPersonRotation = transform.rotation;
            firstPersonYaw = transform.eulerAngles.y;
            firstPersonPitch = transform.eulerAngles.x;
            
            // 初始化上帝视角目标点（当前位置）
            godViewTargetPoint = transform.position;
            savedGodViewYaw = 0f;
        }
        
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

            // 处理模式切换快捷键
            if (Input.GetKeyDown(toggleModeKey))
            {
                ToggleCameraMode();
            }

            // 如果正在过渡，处理过渡动画
            if (isTransitioning)
            {
                HandleTransition();
                return;
            }

            // 根据当前模式处理输入
            switch (currentMode)
            {
                case CameraMode.FirstPerson:
                    UpdateFirstPersonMode();
                    break;
                case CameraMode.GodView:
                    UpdateGodViewMode();
                    break;
            }
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
        
        // ========== 第一人称模式更新 ==========
        
        private void UpdateFirstPersonMode()
        {
            // 鼠标右键按下时允许视角旋转
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X") * firstPersonLookSpeed * lookSpeedMultiplier;
                float mouseY = Input.GetAxis("Mouse Y") * firstPersonLookSpeed * lookSpeedMultiplier;

                firstPersonYaw += mouseX;
                firstPersonPitch -= mouseY;
                firstPersonPitch = Mathf.Clamp(firstPersonPitch, -89f, 89f);

                transform.eulerAngles = new Vector3(firstPersonPitch, firstPersonYaw, 0f);
            }

            // 处理WASD移动（基于相机朝向的3D移动）
            Vector3 direction = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) direction += transform.forward;
            if (Input.GetKey(KeyCode.S)) direction -= transform.forward;
            if (Input.GetKey(KeyCode.A)) direction -= transform.right;
            if (Input.GetKey(KeyCode.D)) direction += transform.right;
            if (Input.GetKey(KeyCode.Space)) direction += Vector3.up;
            if (Input.GetKey(KeyCode.LeftShift)) direction -= Vector3.up;

            if (direction != Vector3.zero)
            {
                transform.position += direction.normalized * firstPersonMovementSpeed * Time.deltaTime;
            }
            
            // 保存当前位置和旋转
            savedFirstPersonPosition = transform.position;
            savedFirstPersonRotation = transform.rotation;
        }
        
        // ========== 上帝视角模式更新 ==========
        
        private void UpdateGodViewMode()
        {
            // 鼠标右键按下时允许水平旋转
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X") * godViewLookSpeed * lookSpeedMultiplier;
                savedGodViewYaw += mouseX;
            }

            // 处理WASD平面移动（移动目标点）
            Vector3 forward = new Vector3(Mathf.Sin(savedGodViewYaw * Mathf.Deg2Rad), 0, Mathf.Cos(savedGodViewYaw * Mathf.Deg2Rad));
            Vector3 right = new Vector3(forward.z, 0, -forward.x);
            
            Vector3 movement = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) movement += forward;
            if (Input.GetKey(KeyCode.S)) movement -= forward;
            if (Input.GetKey(KeyCode.A)) movement -= right;
            if (Input.GetKey(KeyCode.D)) movement += right;

            if (movement != Vector3.zero)
            {
                // 移动目标点（只在XZ平面）
                godViewTargetPoint += movement.normalized * godViewMovementSpeed * Time.deltaTime;
            }
            
            // 鼠标滚轮控制距离（拉近/拉远）
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentGodViewDistance -= scroll * godViewScrollSpeed;
                currentGodViewDistance = Mathf.Clamp(currentGodViewDistance, godViewMinDistance, godViewMaxDistance);
            }
            
            // 根据目标点、距离和角度计算相机位置
            UpdateGodViewTransform();
            
            // 保存当前位置
            savedGodViewPosition = transform.position;
        }
        
        /// <summary>
        /// 更新上帝视角的Transform以保持正确的俯视角度和距离
        /// </summary>
        private void UpdateGodViewTransform()
        {
            // 根据俯视角度和距离计算相机相对于目标点的偏移
            float height = currentGodViewDistance * Mathf.Sin(godViewAngle * Mathf.Deg2Rad);
            float horizontalDistance = currentGodViewDistance * Mathf.Cos(godViewAngle * Mathf.Deg2Rad);
            
            // 计算相机位置（目标点 + 偏移）
            Vector3 offset = new Vector3(
                -horizontalDistance * Mathf.Sin(savedGodViewYaw * Mathf.Deg2Rad),
                height,
                -horizontalDistance * Mathf.Cos(savedGodViewYaw * Mathf.Deg2Rad)
            );
            
            transform.position = godViewTargetPoint + offset;
            
            // 让相机始终朝向目标点
            transform.LookAt(godViewTargetPoint);
        }
        
        // ========== 模式切换方法 ==========
        
        /// <summary>
        /// 切换相机模式（由按钮或快捷键调用）
        /// </summary>
        public void ToggleCameraMode()
        {
            if (currentMode == CameraMode.FirstPerson)
            {
                SwitchToGodView();
            }
            else
            {
                SwitchToFirstPerson();
            }
        }
        
        /// <summary>
        /// 切换到第一人称模式
        /// </summary>
        private void SwitchToFirstPerson()
        {
            // 保存当前上帝视角位置
            savedGodViewPosition = transform.position;
            
            if (smoothTransition)
            {
                // 启动平滑过渡
                StartTransition(savedFirstPersonPosition, savedFirstPersonRotation);
            }
            else
            {
                // 立即切换
                transform.position = savedFirstPersonPosition;
                transform.rotation = savedFirstPersonRotation;
            }
            
            currentMode = CameraMode.FirstPerson;
        }
        
        /// <summary>
        /// 切换到上帝视角模式
        /// </summary>
        private void SwitchToGodView()
        {
            // 保存当前第一人称位置和旋转
            savedFirstPersonPosition = transform.position;
            savedFirstPersonRotation = transform.rotation;
            
            // 第一次切换时，将目标点设置为当前位置
            if (savedGodViewPosition == Vector3.zero)
            {
                godViewTargetPoint = transform.position;
                
                // 计算初始相机位置
                float height = currentGodViewDistance * Mathf.Sin(godViewAngle * Mathf.Deg2Rad);
                float horizontalDistance = currentGodViewDistance * Mathf.Cos(godViewAngle * Mathf.Deg2Rad);
                
                Vector3 offset = new Vector3(
                    -horizontalDistance * Mathf.Sin(savedGodViewYaw * Mathf.Deg2Rad),
                    height,
                    -horizontalDistance * Mathf.Cos(savedGodViewYaw * Mathf.Deg2Rad)
                );
                
                savedGodViewPosition = godViewTargetPoint + offset;
            }
            
            // 计算目标旋转（朝向目标点）
            Vector3 directionToTarget = (godViewTargetPoint - savedGodViewPosition).normalized;
            Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
            
            if (smoothTransition)
            {
                // 启动平滑过渡
                StartTransition(savedGodViewPosition, targetRot);
            }
            else
            {
                // 立即切换
                transform.position = savedGodViewPosition;
                transform.rotation = targetRot;
            }
            
            currentMode = CameraMode.GodView;
        }
        
        /// <summary>
        /// 开始平滑过渡
        /// </summary>
        private void StartTransition(Vector3 targetPos, Quaternion targetRot)
        {
            isTransitioning = true;
            transitionProgress = 0f;
            transitionStartPos = transform.position;
            transitionStartRot = transform.rotation;
            transitionTargetPos = targetPos;
            transitionTargetRot = targetRot;
        }
        
        /// <summary>
        /// 处理平滑过渡动画
        /// </summary>
        private void HandleTransition()
        {
            transitionProgress += Time.deltaTime * transitionSpeed;
            
            if (transitionProgress >= 1f)
            {
                // 过渡完成
                transform.position = transitionTargetPos;
                transform.rotation = transitionTargetRot;
                isTransitioning = false;
                transitionProgress = 1f;
            }
            else
            {
                // 平滑插值
                transform.position = Vector3.Lerp(transitionStartPos, transitionTargetPos, transitionProgress);
                transform.rotation = Quaternion.Slerp(transitionStartRot, transitionTargetRot, transitionProgress);
            }
        }
        
        // ========== 公共接口 ==========
        
        /// <summary>
        /// 外部控制输入启用/禁用
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }
        
        /// <summary>
        /// 获取当前相机模式
        /// </summary>
        public CameraMode GetCurrentMode()
        {
            return currentMode;
        }
        
        /// <summary>
        /// 设置相机模式（供外部调用）
        /// </summary>
        public void SetCameraMode(CameraMode mode)
        {
            if (mode != currentMode)
            {
                ToggleCameraMode();
            }
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
