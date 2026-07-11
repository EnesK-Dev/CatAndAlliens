using UnityEngine;

/// <summary>
/// Kamera arka planını karanlık uzaya çevirir.
/// Prosedürel tek bir Mesh içinde yıldız noktaları oluşturur — sıfır dış asset.
/// </summary>
public class SpaceBackground : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private int starCount = 300;
    [SerializeField] private float fieldRadius = 20f;
    [SerializeField] private float starSizeMin = 0.03f;
    [SerializeField] private float starSizeMax = 0.09f;

    [Tooltip("0 = yıldızlar kamerayla tam senkron, 0.1 = hafif parallax derinliği")]
    [SerializeField, Range(0f, 0.5f)] private float parallaxFactor = 0.08f;

    [SerializeField] private Color spaceColor = new Color(0.01f, 0.01f, 0.04f);
    #endregion

    #region Private Fields
    private GameObject _starObj;
    private Material _starMaterial;
    private Mesh _starMesh;
    private Camera _mainCamera;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _mainCamera = Camera.main;
        _mainCamera.backgroundColor = spaceColor;
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;

        BuildStarMesh();
    }

    private void LateUpdate()
    {
        if (_starObj == null || _mainCamera == null) return;

        Vector3 camPos = _mainCamera.transform.position;
        _starObj.transform.position = new Vector3(
            camPos.x * (1f - parallaxFactor),
            camPos.y * (1f - parallaxFactor),
            camPos.z + 50f
        );
    }

    private void OnDestroy()
    {
        if (_starMesh != null) Destroy(_starMesh);
        if (_starMaterial != null) Destroy(_starMaterial);
    }
    #endregion

    #region Private Methods
    private void BuildStarMesh()
    {
        _starMesh = new Mesh();
        _starMesh.name = "ProceduralStarField";

        var vertices = new Vector3[starCount * 4];
        var triangles = new int[starCount * 6];
        var colors = new Color[starCount * 4];
        var uvs = new Vector2[starCount * 4];

        for (int i = 0; i < starCount; i++)
        {
            float x = Random.Range(-fieldRadius, fieldRadius);
            float y = Random.Range(-fieldRadius, fieldRadius);
            float s = Random.Range(starSizeMin, starSizeMax);

            float brightness = Random.Range(0.45f, 1f);
            // Küçük bir mavi-beyaz tonu — gerçek yıldız rengi
            Color starColor = new Color(
                brightness * Random.Range(0.85f, 1f),
                brightness * Random.Range(0.88f, 1f),
                brightness
            );

            int v = i * 4;
            vertices[v + 0] = new Vector3(x - s, y - s, 0f);
            vertices[v + 1] = new Vector3(x + s, y - s, 0f);
            vertices[v + 2] = new Vector3(x + s, y + s, 0f);
            vertices[v + 3] = new Vector3(x - s, y + s, 0f);

            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = starColor;
            uvs[v] = Vector2.zero;
            uvs[v + 1] = Vector2.right;
            uvs[v + 2] = Vector2.one;
            uvs[v + 3] = Vector2.up;

            int t = i * 6;
            triangles[t + 0] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        _starMesh.vertices = vertices;
        _starMesh.triangles = triangles;
        _starMesh.colors = colors;
        _starMesh.uv = uvs;
        _starMesh.RecalculateBounds();

        _starObj = new GameObject("StarFieldMesh");
        _starObj.transform.SetParent(transform);

        _starObj.AddComponent<MeshFilter>().mesh = _starMesh;

        _starMaterial = new Material(FindCompatibleShader());
        _starMaterial.color = Color.white;

        MeshRenderer mr = _starObj.AddComponent<MeshRenderer>();
        mr.material = _starMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingOrder = -1000;
    }

    private static Shader FindCompatibleShader()
    {
        // Sprites/Default vertex color destekler, URP 2D'de de çalışır
        Shader s = Shader.Find("Sprites/Default");
        if (s != null) return s;

        // URP Particles/Unlit da vertex color destekler
        s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s != null) return s;

        // Son çare — renk varyasyonu olmaz ama yıldızlar görünür
        s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s != null) return s;

        return Shader.Find("Unlit/Color");
    }
    #endregion
}
