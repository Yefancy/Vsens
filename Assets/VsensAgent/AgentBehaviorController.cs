using UnityEngine;

public class AgentBehaviorController : MonoBehaviour
{
    [Header("Idle Float Settings")]
    public Transform agentRoot;
    public float floatAmplitude = 0.05f;  // 上下浮动幅度
    public float floatFrequency = 1f;     // 浮动频率
    public float rotationSpeed = 15f;     // 轻微自转速度

    [Header("Eye Tracking Settings")]
    public Transform eye;              // Eye父物体（包含EyeWhite和Pupil）
    public Transform cameraTarget;     // 主摄像机
    public float eyeRadius = 0.55f;    // 眼睛贴在AgentBody表面的半径
    public float followSpeed = 5f;     // 眼睛跟随速度（平滑）

    private Vector3 eyeBaseDir;        // 眼睛初始方向（相对AgentBody中心）
    private Transform agentBody;       // 用于计算球面方向的父物体
    private Vector3 initialPosition;

    void Start()
    {
        // 记录agent整体初始位置
        initialPosition = agentRoot.position;

        // 默认寻找摄像机
        if (cameraTarget == null && Camera.main != null)
        {
            cameraTarget = Camera.main.transform;
        }

        // AgentBody 是眼睛的父物体
        if (eye != null)
        {
            agentBody = eye.parent;
            eyeBaseDir = eye.localPosition.normalized;
        }
    }

    void OnEnable()
    {
        WsClient.OnAgentBehavior += PerformBehavior;
    }

    void OnDisable()
    {
        WsClient.OnAgentBehavior -= PerformBehavior;
    }

    void Update()
    {
        UpdateEyeTracking();
        UpdateIdleFloat();
    }

    private void UpdateIdleFloat()
    {
        // 上下浮动
        float newY = initialPosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;

        // 轻微自转
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 行为指令回调
    /// </summary>
    public void PerformBehavior(WsClient.AgentBehavior msg)
    {
        Debug.Log($"[Behavior] Action: {msg.action}, Target: {msg.target}, Emotion: {msg.emotion}");

        // 根据不同的行为执行相应的动作
        switch (msg.action)
        {
            case "wave":
                PerformWaveAction();
                break;
            case "nod":
                PerformNodAction();
                break;
            case "look_at":
                PerformLookAtAction(msg.target);
                break;
            case "float_up":
                PerformFloatUpAction();
                break;
            case "float_down":
                PerformFloatDownAction();
                break;
            default:
                Debug.LogWarning($"[Behavior] Unknown action: {msg.action}");
                break;
        }

        // 处理情绪相关的行为
        if (!string.IsNullOrEmpty(msg.emotion))
        {
            ApplyEmotionBehavior(msg.emotion);
        }

        // 处理目标相关的行为
        if (msg.target == "user" || msg.target == "camera")
        {
            LookAtUserInstantly();
        }
    }

    /// <summary>
    /// 眼睛沿着球面跟随摄像机移动
    /// </summary>
    private void UpdateEyeTracking()
    {
        if (eye == null || cameraTarget == null || agentBody == null)
            return;

        // 1. 从AgentBody中心指向摄像机方向
        Vector3 dirToCam = (cameraTarget.position - agentBody.position).normalized;

        // 2. 平滑插值方向（模拟眼睛慢慢转）
        eyeBaseDir = Vector3.Slerp(eyeBaseDir, dirToCam, Time.deltaTime * followSpeed);

        // 3. 更新眼睛在球面上的位置
        eye.localPosition = eyeBaseDir * eyeRadius;

        // 4. 让眼睛正面朝向摄像机
        eye.LookAt(cameraTarget.position);
    }

    /// <summary>
    /// 让眼睛立即看向用户（可用于行为指令响应）
    /// </summary>
    private void LookAtUserInstantly()
    {
        if (eye == null || cameraTarget == null || agentBody == null)
            return;

        Vector3 dirToCam = (cameraTarget.position - agentBody.position).normalized;
        eyeBaseDir = dirToCam; // 立即更新方向
    }

    #region 行为动作实现方法

    private void PerformWaveAction()
    {
        Debug.Log("[Behavior] Performing wave action - 占位实现");
        // TODO: 实现挥手动画
        // 可以使用 Animator 或者简单的 Transform 动画
    }

    private void PerformNodAction()
    {
        Debug.Log("[Behavior] Performing nod action - 占位实现");
        // TODO: 实现点头动画
        // 可以让 agentRoot 做上下点头动作
    }

    private void PerformLookAtAction(string target)
    {
        Debug.Log($"[Behavior] Looking at target: {target} - 占位实现");
        // TODO: 根据目标名称查找场景中的对象并看向它
        if (target == "user" || target == "camera")
        {
            LookAtUserInstantly();
        }
    }

    private void PerformFloatUpAction()
    {
        Debug.Log("[Behavior] Floating up - 占位实现");
        // TODO: 让Agent向上浮动
        // 可以临时增加 floatAmplitude 或调整 initialPosition
    }

    private void PerformFloatDownAction()
    {
        Debug.Log("[Behavior] Floating down - 占位实现");
        // TODO: 让Agent向下浮动
    }

    private void ApplyEmotionBehavior(string emotion)
    {
        Debug.Log($"[Behavior] Applying emotion: {emotion} - 占位实现");
        // TODO: 根据情绪调整行为
        switch (emotion)
        {
            case "happy":
                // 增加浮动频率，让动作更活泼
                break;
            case "sad":
                // 降低浮动频率，让动作更缓慢
                break;
            case "excited":
                // 增加旋转速度
                break;
            case "calm":
                // 减少所有动作幅度
                break;
        }
    }

    #endregion

    #region 测试方法（可在Inspector中调用）

    [ContextMenu("Test Wave Behavior")]
    public void TestWaveBehavior()
    {
        var testBehavior = new WsClient.AgentBehavior
        {
            type = "agent_behavior",
            action = "wave",
            target = "user",
            emotion = "happy"
        };
        PerformBehavior(testBehavior);
    }

    [ContextMenu("Test Look At User")]
    public void TestLookAtUser()
    {
        var testBehavior = new WsClient.AgentBehavior
        {
            type = "agent_behavior",
            action = "look_at",
            target = "user",
            emotion = "curious"
        };
        PerformBehavior(testBehavior);
    }

    #endregion
}
