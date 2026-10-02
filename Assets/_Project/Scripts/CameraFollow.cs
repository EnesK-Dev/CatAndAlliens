using UnityEngine;

/// <summary>
/// Kamera RIG'ine eklenir (kamera bu rig'in child'idir). Rig'i oyuncuya YUMUSAK (SmoothDamp) takip
/// ettirir; birebir kilitli takip yerine hafif gecikme -> hareket daha "yumusak" hisseder. Sadece X/Y
/// takip eder, Z sabit (kamera derinligi child'in local Z'sinden gelir).
///
/// CameraShake kameranin LOCAL pozisyonunu sarstigi icin (rig'e gore), takip ile CAKISMAZ: rig world
/// pozisyonu buradan, shake local offset CameraShake'ten gelir. Oyuncu OLUNCE takip durur ki
/// CameraDeathZoom (kamerayi olum noktasina pan/zoom eder) devralabilsin.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Takip edilecek hedef (bos = otomatik olarak player bulunur).")]
    [SerializeField] private Transform target;

    [Tooltip("Yumusatma suresi (sn). Kucuk = daha ani/sikko takip, buyuk = daha tembel/yumusak.")]
    [SerializeField] private float smoothTime = 0.12f;

    [Tooltip("Kameranin ulasabilecegi maksimum takip hizi (birim/sn). Cok hizli kacan hedefte kopmayi sinirlar.")]
    [SerializeField] private float maxSpeed = 60f;
    #endregion

    #region Private Fields
    private Vector3 _velocity;   // SmoothDamp'in dahili hiz referansi (alloc'suz)
    private bool _following = true;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (target == null)
        {
            player p = FindFirstObjectByType<player>();
            if (p != null) target = p.transform;
        }
    }

    private void OnEnable()
    {
        // Oyuncu olunce takibi birak -> CameraDeathZoom kamerayi serbestce pan/zoom edebilsin.
        player.OnPlayerDeathStarted += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        player.OnPlayerDeathStarted -= HandlePlayerDeath;
    }

    // Hareket/fizik (FixedUpdate) ve shake (LateUpdate) sonrasi rig'i konumla -> hedefin son konumunu baz alir.
    private void LateUpdate()
    {
        if (!_following || target == null) return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime, maxSpeed);
    }
    #endregion

    #region Private Methods
    private void HandlePlayerDeath(Vector3 deathPosition) => _following = false;
    #endregion
}
