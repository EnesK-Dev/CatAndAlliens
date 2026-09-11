using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Acilis + loading sinemasi: kedi soldan saga yurur, gectikce arkasindaki "MEOWVIVORS" yazisi harf harf
/// belirir (maxVisibleCharacters kedinin x konumuna baglanir). Bitince (ya da dokunulunca) SceneLoader ile
/// hedef sahneye fade gecisi.
///
/// HEDEF SAHNE: normalde serialized 'nextScene' (MainMenu). Ama baska bir yerden (ornek: MainMenu PLAY)
/// once <see cref="NextSceneOverride"/> set edilirse o sahneye gider (loading screen gibi kullanim).
/// Boylece TEK sahne hem acilista (->MainMenu) hem PLAY'de (->SampleScene) kullanilir.
/// </summary>
public class SplashIntro : MonoBehaviour
{
    #region Static Routing
    /// <summary>Bir sonraki gecisin hedef sahnesi. Bos degilse serialized nextScene yerine bu kullanilir (bir kerelik, okununca temizlenir).</summary>
    public static string NextSceneOverride;
    #endregion

    #region Serialized Fields
    [Header("Kedi")]
    [Tooltip("Yuruyen kedi transform'u (SpriteAnimator ile yurume frame'leri oynar).")]
    [SerializeField] private Transform cat;
    [Tooltip("Kedinin yurume hizi (birim/sn).")]
    [SerializeField] private float walkSpeed = 6f;
    [Tooltip("Kedinin baslangic X'i (ekran solundan disari).")]
    [SerializeField] private float catStartX = -10f;
    [Tooltip("Kedi bu X'e ulasinca yuruyus biter (ekran sagindan disari).")]
    [SerializeField] private float catEndX = 10f;

    [Header("Baslik (MEOWVIVORS)")]
    [SerializeField] private TMP_Text title;
    [Tooltip("Kedi bu X'e gelince ilk harf belirir.")]
    [SerializeField] private float revealStartX = -5f;
    [Tooltip("Kedi bu X'e gelince son harf belirir.")]
    [SerializeField] private float revealEndX = 5f;

    [Header("Gecis")]
    [SerializeField] private SceneLoader sceneLoader;
    [Tooltip("Override yoksa gidilecek varsayilan sahne (acilis -> MainMenu).")]
    [SerializeField] private string nextScene = "MainMenu";
    [Tooltip("Yazi tamamlandiktan sonra bekleme (sn), sonra gecis.")]
    [SerializeField] private float holdAfterReveal = 1.2f;
    [Tooltip("Basta kisa gecikme (sn) — birden baslamasin.")]
    [SerializeField] private float startDelay = 0.4f;
    #endregion

    #region Private Fields
    private int _totalChars;
    private float _timer;
    private float _holdTimer;
    private bool _transitioned;
    private string _target;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        // Hedef: override varsa onu kullan (bir kerelik), yoksa varsayilan. Boylece PLAY -> SampleScene, acilis -> MainMenu.
        _target = !string.IsNullOrEmpty(NextSceneOverride) ? NextSceneOverride : nextScene;
        NextSceneOverride = null;

        if (cat != null) { Vector3 p = cat.position; p.x = catStartX; cat.position = p; }
        if (title != null)
        {
            title.ForceMeshUpdate();
            _totalChars = title.textInfo.characterCount;
            title.maxVisibleCharacters = 0;
        }
    }

    private void Update()
    {
        if (_transitioned) return;

        if (SkipPressed()) { GoNext(); return; }

        _timer += Time.deltaTime;
        if (_timer < startDelay) return;

        if (cat != null && cat.position.x < catEndX)
            cat.position += Vector3.right * (walkSpeed * Time.deltaTime);

        if (title != null && _totalChars > 0 && cat != null)
        {
            float t = Mathf.InverseLerp(revealStartX, revealEndX, cat.position.x);
            title.maxVisibleCharacters = Mathf.Clamp(Mathf.RoundToInt(t * _totalChars), 0, _totalChars);
        }

        bool walkDone = cat == null || cat.position.x >= catEndX;
        bool revealDone = title == null || title.maxVisibleCharacters >= _totalChars;
        if (walkDone && revealDone)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdAfterReveal) GoNext();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>Herhangi bir dokunma / fare tiklamasi / klavye tusu splash'i atlar.</summary>
    private static bool SkipPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        return false;
    }

    private void GoNext()
    {
        _transitioned = true;
        string target = string.IsNullOrEmpty(_target) ? nextScene : _target;
        if (sceneLoader != null) sceneLoader.LoadScene(target);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(target);
    }
    #endregion
}
