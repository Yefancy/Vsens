using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;
using VsensAgent.Tests.Editor.SceneApi;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class MonitorManagerAvatarUiTests
    {
        [Test]
        public void RefreshSensorList_CreatesAvatarUiItemForManagedAvatar()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("RuntimeRoot");
            var uiRoot = new GameObject("MonitorRoot");
            var itemContainer = new GameObject("ItemContainer").transform;
            itemContainer.SetParent(uiRoot.transform, false);
            var scrollRect = uiRoot.AddComponent<ScrollRect>();
            scrollRect.content = itemContainer as RectTransform;
            var manager = uiRoot.AddComponent<MonitorManager>();
            var avatarPrefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = runtimeRoot.AddComponent<SceneRegistry>();
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(avatarPrefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", Vector3.zero, Vector3.zero, out var spawnError), Is.True, spawnError);

                manager.itemContainer = itemContainer;
                manager.sensorItemPrefab = CreateSensorItemPrefab();
                manager.avatarItemPrefab = CreateAvatarItemPrefab();

                manager.RefreshSensorList();

                var avatarUiItem = itemContainer.GetComponentInChildren<AvatarUIItem>(true);
                Assert.That(avatarUiItem, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(manager.sensorItemPrefab);
                Object.DestroyImmediate(manager.avatarItemPrefab);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(avatarPrefab);
                Object.DestroyImmediate(uiRoot);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AvatarUiItem_Click_SelectsAvatarInEditMode()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("RuntimeRoot");
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
            var avatarPrefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(avatarPrefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", Vector3.zero, Vector3.zero, out var spawnError), Is.True, spawnError);

                var registry = runtimeRoot.AddComponent<RuntimeEditableObjectRegistry>();
                registry.Configure(runtime);

                var controller = runtimeRoot.AddComponent<RuntimeEditModeController>();
                controller.Configure(runtime);
                controller.SetEditMode(true);

                var itemGo = CreateAvatarItemPrefab();
                var avatarUiItem = itemGo.GetComponent<AvatarUIItem>();
                avatarUiItem.Initialize(runtime.GetAvatarQueryModels(null)[0]);

                avatarUiItem.OnPointerClick(new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left
                });

                Assert.That(controller.SelectedObjectId, Is.EqualTo("avatar_main"));
                Assert.That(avatarUiItem.selectionHighlight.color, Is.EqualTo(avatarUiItem.selectedHighlightColor));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(avatarPrefab);
                Object.DestroyImmediate(eventSystemGo);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AvatarUiDeleteButton_RemovesAvatarAndItem()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("RuntimeRoot");
            var uiRoot = new GameObject("MonitorRoot");
            var itemContainer = new GameObject("ItemContainer").transform;
            itemContainer.SetParent(uiRoot.transform, false);
            var avatarPrefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(avatarPrefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", Vector3.zero, Vector3.zero, out var spawnError), Is.True, spawnError);

                var manager = uiRoot.AddComponent<MonitorManager>();
                manager.itemContainer = itemContainer;
                manager.sensorItemPrefab = CreateSensorItemPrefab();
                manager.avatarItemPrefab = CreateAvatarItemPrefab();
                manager.RefreshSensorList();

                var avatarUiItem = itemContainer.GetComponentInChildren<AvatarUIItem>(true);
                Assert.That(avatarUiItem, Is.Not.Null);
                Assert.That(avatarUiItem.deleteButton, Is.Not.Null);

                avatarUiItem.deleteButton.onClick.Invoke();

                Assert.That(runtime.GetManagedAvatarObject(), Is.Null);
                Assert.That(itemContainer.GetComponentInChildren<AvatarUIItem>(true), Is.Null);
            }
            finally
            {
                var manager = uiRoot.GetComponent<MonitorManager>();
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.sensorItemPrefab);
                    Object.DestroyImmediate(manager.avatarItemPrefab);
                }

                Object.DestroyImmediate(avatarPrefab);
                Object.DestroyImmediate(uiRoot);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        private static GameObject CreateAvatarItemPrefab()
        {
            var root = new GameObject("AvatarItemPrefab");
            var item = root.AddComponent<AvatarUIItem>();

            item.avatarNameText = new GameObject("AvatarName").AddComponent<TextMeshProUGUI>();
            item.avatarNameText.transform.SetParent(root.transform, false);

            item.avatarStatusText = new GameObject("AvatarStatus").AddComponent<TextMeshProUGUI>();
            item.avatarStatusText.transform.SetParent(root.transform, false);

            item.statusIndicator = new GameObject("StatusIndicator").AddComponent<Image>();
            item.statusIndicator.transform.SetParent(root.transform, false);

            item.selectionHighlight = new GameObject("SelectionHighlight").AddComponent<Image>();
            item.selectionHighlight.transform.SetParent(root.transform, false);

            item.detailContainer = new GameObject("DetailContainer");
            item.detailContainer.transform.SetParent(root.transform, false);
            item.detailContainer.SetActive(false);

            var toggleGo = new GameObject("DetailToggle");
            toggleGo.transform.SetParent(root.transform, false);
            item.detailToggle = toggleGo.AddComponent<Toggle>();

            return root;
        }

        private static GameObject CreateSensorItemPrefab()
        {
            var root = new GameObject("SensorItemPrefab");
            root.AddComponent<SensorUIItem>();
            return root;
        }
    }
}
