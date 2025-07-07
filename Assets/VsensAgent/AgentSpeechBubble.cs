using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections;

public class AgentSpeechBubble : MonoBehaviour
{
    [Header("References")]
    public Canvas bubbleCanvas;
    public TextMeshProUGUI textField;

    [Header("Settings")]
    public float visibleDuration = 4f;
    public float typeSpeed = 0.03f;

    [Header("Editor Debug")]
    public string testMessage = "Hello from Inspector";

    private Coroutine hideCoroutine;
    private Coroutine typingCoroutine;

    void OnEnable()
    {
        WsClient.OnAgentSpeechText += ShowText;
    }

    void OnDisable()
    {
        WsClient.OnAgentSpeechText -= ShowText;
    }

    void Update()
    {
        if (bubbleCanvas != null && Camera.main != null)
        {
            // 使其朝向 Main Camera
            transform.LookAt(Camera.main.transform);
            // 可选：反向旋转，使文本正面朝向摄像机
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }

    public void ShowText(string fullText)
    {
        bubbleCanvas.enabled = true;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        typingCoroutine = StartCoroutine(TypeText(fullText));
    }

    private IEnumerator TypeText(string message)
    {
        textField.text = "";

        foreach (char letter in message)
        {
            textField.text += letter;
            yield return new WaitForSeconds(typeSpeed);
        }

        hideCoroutine = StartCoroutine(AutoHide());
    }

    private IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(visibleDuration);
        bubbleCanvas.enabled = false;
    }

    public void ForceHide()
    {
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        bubbleCanvas.enabled = false;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AgentSpeechBubble))]
public class AgentSpeechBubbleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AgentSpeechBubble bubble = (AgentSpeechBubble)target;

        GUILayout.Space(10);
        if (GUILayout.Button("▶ Show Test Message"))
        {
            bubble.ShowText(bubble.testMessage);
        }
    }
}
#endif