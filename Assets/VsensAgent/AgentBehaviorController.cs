using UnityEngine;

public class AgentBehaviorController : MonoBehaviour
{
    void OnEnable()
    {
        WsClient.OnBehaviorCommand += PerformBehavior;
    }

    void OnDisable()
    {
        WsClient.OnBehaviorCommand -= PerformBehavior;
    }

    public void PerformBehavior(WsClient.BehaviorMessage msg)
    {
        Debug.Log($"[Behavior] Action: {msg.action}, Target: {msg.target}, Emotion: {msg.emotion}");

        // 示例行为处理逻辑（你可以替换成 Animator / IK / NavMesh 等）
        if (msg.action == "wave")
        {
            // 播放动画
        }

        if (msg.target == "user")
        {
            // 转向用户
        }
    }
}
