using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using VsensAgent.UI;

namespace VsensAgent.Editor
{
    public class SensorMonitorUICreator : EditorWindow
    {
        [MenuItem("VsensAgent/UI Tools/Create Sensor Monitor UI")]
        public static void ShowWindow()
        {
            GetWindow<SensorMonitorUICreator>("Sensor Monitor Creator");
        }

        private Canvas mainCanvas;
        private bool createWithPrefab = false;

        void OnGUI()
        {
            GUILayout.Label("Sensor Monitor UI 自动创建工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            mainCanvas = (Canvas)EditorGUILayout.ObjectField("Main Canvas", mainCanvas, typeof(Canvas), true);
            
            EditorGUILayout.HelpBox("请将场景中的 MainCanvas 拖拽到上面的字段", MessageType.Info);
            
            EditorGUILayout.Space(10);
            createWithPrefab = EditorGUILayout.Toggle("同时创建 Sensor Item 预制件", createWithPrefab);
            
            EditorGUILayout.Space(20);

            GUI.enabled = mainCanvas != null;
            if (GUILayout.Button("🚀 创建 Sensor Monitor UI", GUILayout.Height(40)))
            {
                CreateSensorMonitorUI();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);
            if (GUILayout.Button("📖 打开设置指南"))
            {
                Application.OpenURL("file://" + Application.dataPath + "/../SENSOR_MONITOR_SETUP_GUIDE.md");
            }
        }

        void CreateSensorMonitorUI()
        {
            if (mainCanvas == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 MainCanvas！", "确定");
                return;
            }

            // 开始记录Undo
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Sensor Monitor UI");

            try
            {
                // 1. 创建主Panel
                GameObject panel = CreateSensorMonitorPanel();
                
                // 2. 创建ScrollView（不需要Header了）
                GameObject scrollView = CreateScrollView(panel.transform);
                
                // 3. 添加管理器脚本
                AttachManagerScript(panel, scrollView);
                
                // 5. 可选：创建预制件
                if (createWithPrefab)
                {
                    CreateSensorItemPrefab();
                }

                // 选中创建的Panel
                Selection.activeGameObject = panel;
                
                EditorUtility.DisplayDialog("完成", 
                    "✅ Sensor Monitor UI 创建成功！\n\n" +
                    "已创建：\n" +
                    "• SensorMonitorPanel (右侧圆角面板)\n" +
                    "• Scrollable Sensor List\n" +
                    "• SensorMonitorManager 脚本\n\n" +
                    (createWithPrefab ? "• SensorItem 预制件\n\n" : "") +
                    "现在可以运行场景测试了！", 
                    "太棒了！");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", 
                    "创建UI时出错：\n" + e.Message, 
                    "确定");
                Debug.LogError("[SensorMonitorUICreator] " + e);
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        GameObject CreateSensorMonitorPanel()
        {
            GameObject panel = new GameObject("SensorMonitorPanel");
            Undo.RegisterCreatedObjectUndo(panel, "Create Panel");
            
            panel.transform.SetParent(mainCanvas.transform, false);
            
            // 添加RectTransform
            RectTransform rt = panel.AddComponent<RectTransform>();
            
            // 设置锚点到右侧，上下拉伸（类似ChatWindow但在右侧）
            rt.anchorMin = new Vector2(0.75f, 0f);  // 从75%开始
            rt.anchorMax = new Vector2(1f, 1f);     // 到100%，上下拉伸
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            // 添加Image背景
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.7f); // 半透明黑色
            img.type = Image.Type.Sliced; // 使用切片类型以支持圆角
            
            // 添加Outline组件实现圆角边框效果
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            outline.effectDistance = new Vector2(2, -2);
            
            // 添加Shadow组件增加深度感
            Shadow shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(3, -3);
            
            Debug.Log("[SensorMonitorUICreator] ✅ Created SensorMonitorPanel (right-side, stretched, with rounded style)");
            return panel;
        }

        GameObject CreateScrollView(Transform parent)
        {
            // ScrollView
            GameObject scrollView = new GameObject("SensorListScrollView");
            Undo.RegisterCreatedObjectUndo(scrollView, "Create ScrollView");
            
            scrollView.transform.SetParent(parent, false);
            
            RectTransform svRT = scrollView.AddComponent<RectTransform>();
            svRT.anchorMin = Vector2.zero;
            svRT.anchorMax = Vector2.one;
            svRT.offsetMin = new Vector2(10f, 10f);  // 左、下边距
            svRT.offsetMax = new Vector2(-10f, -10f); // 右、上边距（去掉Header后改为-10）
            
            Image svImg = scrollView.AddComponent<Image>();
            svImg.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            
            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 20f;
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            Undo.RegisterCreatedObjectUndo(viewport, "Create Viewport");
            
            viewport.transform.SetParent(scrollView.transform, false);
            
            RectTransform vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.pivot = new Vector2(0.5f, 0.5f);
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            
            viewport.AddComponent<RectMask2D>();
            
            // Content
            GameObject content = new GameObject("Content");
            Undo.RegisterCreatedObjectUndo(content, "Create Content");
            
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform ctRT = content.AddComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0f, 1f);
            ctRT.anchorMax = new Vector2(1f, 1f);
            ctRT.pivot = new Vector2(0.5f, 1f);
            ctRT.anchoredPosition = Vector2.zero;
            ctRT.sizeDelta = new Vector2(0f, 0f);
            
            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Scrollbar
            GameObject scrollbar = new GameObject("Scrollbar Vertical");
            Undo.RegisterCreatedObjectUndo(scrollbar, "Create Scrollbar");
            
            scrollbar.transform.SetParent(scrollView.transform, false);
            
            RectTransform sbRT = scrollbar.AddComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1f, 0f);
            sbRT.anchorMax = new Vector2(1f, 1f);
            sbRT.pivot = new Vector2(1f, 0.5f);
            sbRT.anchoredPosition = Vector2.zero;
            sbRT.sizeDelta = new Vector2(20f, 0f);
            
            Image sbImg = scrollbar.AddComponent<Image>();
            sbImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            
            Scrollbar sb = scrollbar.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            
            // Scrollbar Handle
            GameObject handle = new GameObject("Handle");
            Undo.RegisterCreatedObjectUndo(handle, "Create Handle");
            
            handle.transform.SetParent(scrollbar.transform, false);
            
            RectTransform hRT = handle.AddComponent<RectTransform>();
            hRT.anchorMin = Vector2.zero;
            hRT.anchorMax = Vector2.one;
            hRT.offsetMin = new Vector2(5f, 5f);
            hRT.offsetMax = new Vector2(-5f, -5f);
            
            Image hImg = handle.AddComponent<Image>();
            hImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            
            sb.handleRect = hRT;
            sb.targetGraphic = hImg;
            
            // 连接ScrollRect引用
            scrollRect.viewport = vpRT;
            scrollRect.content = ctRT;
            scrollRect.verticalScrollbar = sb;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            
            Debug.Log("[SensorMonitorUICreator] ✅ Created ScrollView with Content");
            return scrollView;
        }

        void AttachManagerScript(GameObject panel, GameObject scrollView)
        {
            SensorMonitorManager manager = panel.AddComponent<SensorMonitorManager>();
            Undo.RegisterCreatedObjectUndo(manager, "Add Manager Script");
            
            // 设置引用
            SerializedObject so = new SerializedObject(manager);
            
            so.FindProperty("sensorMonitorPanel").objectReferenceValue = panel;
            so.FindProperty("sensorListScrollRect").objectReferenceValue = scrollView.GetComponent<ScrollRect>();
            so.FindProperty("sensorItemContainer").objectReferenceValue = scrollView.transform.Find("Viewport/Content");
            
            so.FindProperty("updateInterval").floatValue = 0.5f;
            so.FindProperty("autoRefreshList").boolValue = true;
            so.FindProperty("listRefreshInterval").floatValue = 2f;
            so.FindProperty("showDebugInfo").boolValue = true;
            
            so.ApplyModifiedProperties();
            
            Debug.Log("[SensorMonitorUICreator] ✅ Attached and configured SensorMonitorManager");
        }

        void CreateSensorItemPrefab()
        {
            // TODO: 如果需要的话，可以创建一个精致的预制件
            Debug.Log("[SensorMonitorUICreator] 📦 Sensor Item Prefab creation - Coming soon!");
        }
    }
}