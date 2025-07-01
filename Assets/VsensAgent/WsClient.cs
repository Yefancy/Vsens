using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Threading.Tasks;

public class WsTestClient : MonoBehaviour
{
    WebSocket websocket;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () => {
            Debug.Log("[Unity System] Connection Established.");
            websocket.SendText("{\"type\": \"unity_system\", \"content\": \"Connection Established.\"}");
        };

        websocket.OnError += (e) => {
            Debug.Log("[Unity System] Error: " + e);
        };

        websocket.OnClose += (e) => {
            Debug.Log("[Unity System] Connection closed!");
        };

        websocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("[Unity System] Received: " + message);
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif

        // <<< 新增：按下空格键时发送假数据
        if (Input.GetKeyDown(KeyCode.Space) && websocket != null && websocket.State == WebSocketState.Open)
            {
                // 加载 Resources/SceneDescription/test_1.txt
                TextAsset sceneTextAsset = Resources.Load<TextAsset>("SceneDescription/test_1");
                if (sceneTextAsset != null)
                {
                    string sceneSnapshot = sceneTextAsset.text.Replace("\n", " ").Replace("\"", "\\\"");  // 清洗换行与引号
                    string fakeData = $"{{\"type\": \"transcribe_and_reply\", \"audio_path\": \"./data/voices/message2.mp3\", \"scene_snapshot\": \"{sceneSnapshot}\"}}";
                    websocket.SendText(fakeData);
                    Debug.Log("[Data Send] " + fakeData);
                }
                else
                {
                    Debug.LogWarning("Failed to load scene description text file.");
                }
            }
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }
}
