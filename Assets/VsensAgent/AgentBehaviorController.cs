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
        WsClient.OnBehaviorCommand += PerformBehavior;
    }

    void OnDisable()
    {
        WsClient.OnBehaviorCommand -= PerformBehavior;
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
    public void PerformBehavior(WsClient.BehaviorMessage msg)
    {
        Debug.Log($"[Behavior] Action: {msg.action}, Target: {msg.target}, Emotion: {msg.emotion}");

        // 示例行为逻辑
        if (msg.action == "wave")
        {
            // 播放动画或执行动作
        }

        if (msg.target == "user")
        {
            // 将眼睛快速看向用户
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
}
