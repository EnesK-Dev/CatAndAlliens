using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss oyuncunun ekraninda gorunmuyorsa, ekran kenarinda boss'un yonunu gosteren bir OK cizer.
/// BossController.OnBossSpawned/OnBossDefeated (static event) ile boss'u takip eder — Update'te
/// FindObjectOfType YOK. Boss ekranda ise ok gizlenir. Screen Space Overlay canvas'ta calisir
/// (RectTransform.position = ekran pikseli). Tek-sorumluluk: sadece gorsel yon gostergesi.
/// Not: BossController tabanli bosslar (dash/laser/kamikaze/burst) icin calisir; enemy-tabanli
/// splitter boss OnBossSpawned firlatmaz (parcalar zaten oyuncunun etrafinda kumelenir).
/// </summary>
public class BossOffscreenIndicator : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Ekran kenarinda gosterilecek ok (Overlay canvas child). Root'u bu script'in gosterip gizledigi obje.")]
    [SerializeField] private RectTransform arrow;

    [Tooltip("Bos ise Main Camera otomatik bulunur. Boss'un ekranda olup olmadigi bu kameraya gore hesaplanir.")]
    [SerializeField] private Camera targetCamera;

    [Header("Yerlesim")]
    [Tooltip("Okun ekran kenarindan iceri bosluk (piksel). Buyudukce ok kenardan daha iceride durur.")]
    [SerializeField] private float screenEdgePadding = 90f;

    [Tooltip("Ok grafigi VARSAYILAN olarak hangi yone bakiyorsa ona gore aci ofseti (derece). " +
             "Ok yukari bakiyorsa -90, saga bakiyorsa 0 gir.")]
    [SerializeField] private float arrowAngleOffset = -90f;
    #endregion

    #region Private Fields
    private Transform _boss;
    private BossController _bossCtrl;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        HideArrow();
    }

    private void OnEnable()
    {
        BossController.OnBossSpawned += HandleBossSpawned;
        BossController.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        BossController.OnBossSpawned -= HandleBossSpawned;
        BossController.OnBossDefeated -= HandleBossDefeated;
    }

    private void LateUpdate()
    {
        // Boss yok / olmus / kamera yok -> ok gizli
        if (_boss == null || _bossCtrl == null || _bossCtrl.IsDying || !BossController.AnyBossAlive || targetCamera == null)
        {
            HideArrow();
            return;
        }

        Vector3 sp = targetCamera.WorldToScreenPoint(_boss.position);

        // Kameranin ARKASINDA ise (z<0) ekran koordinati ters doner — duzelt.
        if (sp.z < 0f)
        {
            sp.x = Screen.width - sp.x;
            sp.y = Screen.height - sp.y;
        }

        bool onScreen = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
        if (onScreen)
        {
            HideArrow();
            return;
        }

        // Ekran disinda: merkezden boss yonune, padding'li dikdortgen kenarina yansit.
        Vector3 center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Vector3 dir = (Vector3)((Vector2)sp - (Vector2)center);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;

        float maxX = Screen.width * 0.5f - screenEdgePadding;
        float maxY = Screen.height * 0.5f - screenEdgePadding;
        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;
        float scale = Mathf.Min(scaleX, scaleY);

        Vector3 edgePos = center + dir * scale;

        if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
        arrow.position = edgePos;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + arrowAngleOffset;
        arrow.rotation = Quaternion.Euler(0f, 0f, angle);
    }
    #endregion

    #region Private Methods
    private void HandleBossSpawned(BossController boss)
    {
        _bossCtrl = boss;
        _boss = boss != null ? boss.transform : null;
    }

    private void HandleBossDefeated(bool isFinal)
    {
        _boss = null;
        _bossCtrl = null;
        HideArrow();
    }

    private void HideArrow()
    {
        if (arrow != null && arrow.gameObject.activeSelf)
            arrow.gameObject.SetActive(false);
    }
    #endregion
}
