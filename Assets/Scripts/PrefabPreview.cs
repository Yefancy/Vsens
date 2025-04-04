using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrefabPreview : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public Image preview;
    [SerializeField] public TextMeshProUGUI prefabName;
    [SerializeField] public int width = 256;
    [SerializeField] public int height = 256;
    [SerializeField] private Texture2D _textureCache;
    [SerializeField] public bool clonePrefab = false;
    [SerializeField] public Color backgroundColor = Color.clear;
    [SerializeField] public bool orthographicMode = true;
    [SerializeField] public Vector3 cameraDirection = new Vector3(-1, -1, -1);
    
    public Texture2D TextureCache
    {
        get => _textureCache;
        set
        {
            if (_textureCache != null)
            {
                Destroy(_textureCache);
            }
            _textureCache = value;
            if (preview == null) return;
            preview.sprite = Sprite.Create(value, new Rect(0, 0, value.width, value.height), Vector2.zero);
        }
    }
        
    public void Start()
    {
        if (preview == null)
        {
            preview = GetComponentInChildren<Image>();
        }
        if (prefabName == null)
        {
            prefabName = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (prefab != null && preview != null && preview.sprite == null)
        {
            StartCoroutine(RenderingPreview(prefab));
        }
    }

    public void SetPrefab(GameObject prefab)
    {
        this.prefab = prefab;
        prefabName.text = prefab.name;
        StartCoroutine(RenderingPreview(prefab));
    }

    public IEnumerator RenderingPreview(GameObject modelPrefab)
    {
        Texture2D texture2D = null;
        RuntimePreviewGenerator.OrthographicMode = orthographicMode;
        RuntimePreviewGenerator.BackgroundColor = backgroundColor;
        RuntimePreviewGenerator.MarkTextureNonReadable = false;
        RuntimePreviewGenerator.PreviewDirection = cameraDirection.normalized;
        RuntimePreviewGenerator.GenerateModelPreviewAsync(tex => texture2D = tex, 
            modelPrefab.transform, shouldCloneModel: clonePrefab, shouldIgnoreParticleSystems:true, width:width, height:height);
        yield return new WaitUntil(() => texture2D != null);
        TextureCache = texture2D;
    }
}