using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Silahlarin STATLARLA level atlamasini yonetir + upgrade panelinin SAG kolonunda takip eder.
/// Her silahin BIRDEN COK upgrade track'i vardir; her track bir "imza stat"a baglidir
/// (orn. Orbital: Might->hasar, Haste->hiz, Amount->+1 orb, Area->buyume). O stat yeterince secilince
/// o track bir level atlar (silahin kendi ApplyTrack'i). Esik ARTAR (3->4->5...). Ilerleme kucuk kare pip'lerle.
/// Sadece OKUR + uygular; gevsek bagli (UpgradeSelectionUI.OnUpgradeSelected).
/// </summary>
public class WeaponLevelTracker : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private Transform weaponColumn;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private player playerRef;
    [SerializeField] private Sprite clawsIcon;

    [Header("Denge")]
    [SerializeField] private int firstCost = 3;
    [SerializeField] private float clawsDamagePerLevel = 20f;
    [SerializeField] private int maxPips = 6;

    [Header("Fontlar (silah kolonu)")]
    [SerializeField] private float weaponNameSize = 42f;
    [SerializeField] private float trackLabelSize = 30f;
    #endregion

    #region Nested
    private class Track
    {
        public UpgradeSelectionUI.UpgradeType stat;
        public string statLabel, effect, trackKey;
        public WeaponBase weapon;   // null = Sharp Claws
        public int level = 0, progress = 0, cost = 3;
        public bool instant; // Amount: esik yok, her alimda aninda +1 level (pip yok)
        public TMP_Text label;
        public Transform pipRow;
        public readonly List<Image> pips = new List<Image>();
    }
    #endregion

    #region Private Fields
    private readonly List<Track> _tracks = new List<Track>();
    private bool _built;
    private TMP_FontAsset _font;
    private static readonly Color PipFull = new Color(1f, 0.82f, 0.30f, 1f);
    private static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.18f);
    #endregion

    #region Unity Callbacks
    private void OnEnable()
    {
        UpgradeSelectionUI.OnUpgradeSelected += HandleStatPicked;
        if (!_built) BuildRows();
        RefreshAll();
    }
    private void OnDisable() { UpgradeSelectionUI.OnUpgradeSelected -= HandleStatPicked; }
    #endregion

    #region Build
    private void BuildRows()
    {
        if (weaponColumn == null) return;
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        if (weaponManager == null && playerRef != null) weaponManager = playerRef.GetComponent<WeaponManager>();
        _font = FindFont();

        var D = UpgradeSelectionUI.UpgradeType.Damage;      // Might
        var H = UpgradeSelectionUI.UpgradeType.AttackSpeed; // Haste
        var A = UpgradeSelectionUI.UpgradeType.AttackRange; // Area
        var N = UpgradeSelectionUI.UpgradeType.Amount;      // Amount
        var CH = UpgradeSelectionUI.UpgradeType.Charge;     // Orbital hasari
        var FP = UpgradeSelectionUI.UpgradeType.Firepower;  // Blaster hasari
        var IM = UpgradeSelectionUI.UpgradeType.Impact;     // Boomerang hasari

        // Sharp Claws — SADECE claw silahi kusanildiysa goster (artik claw da cikarilabilir silah)
        var clawW = FindWeapon("claw");
        if (clawW != null && clawW.IsAcquired)
        {
            var clawCard = CardCatalog.Get("wpn_claw");
            Sprite ci = clawCard != null && clawCard.icon != null ? clawCard.icon : clawsIcon;
            AddWeapon("Sharp Claws", ci, null, new object[] { D, "Might", "Damage", "" });
        }

        // Orbital: Might->hasar, Haste->hiz, Amount->+1 orb, Area->buyume
        AddWeaponIfOwned("orbital", new object[] {
            CH,"Charge","Damage","damage",  H,"Haste","Speed","speed",
            N,"Amount","Orbs","count",      A,"Area","Size","radius" });

        // Boomerang: Might->hasar, Haste->hiz, Amount->+1 boomerang
        AddWeaponIfOwned("boomerang", new object[] {
            IM,"Impact","Damage","damage",  H,"Haste","Speed","speed",
            N,"Amount","Boomerangs","count" });

        // Auto-Blaster: Might->hasar, Haste->atis hizi, Amount->+1 hedef
        AddWeaponIfOwned("blaster", new object[] {
            FP,"Firepower","Damage","damage",  H,"Haste","Fire Rate","firerate",
            N,"Amount","Targets","targets" });

        // Multi-Slash: Might->hasar (esikli, claw buyur), Amount->+1 yon (aninda)
        AddWeaponIfOwned("multislash", new object[] {
            D,"Might","Damage","damage",  N,"Amount","Slashes","dir" });

        _built = true;
    }

    private void AddWeaponIfOwned(string weaponId, object[] tracks)
    {
        var w = FindWeapon(weaponId);
        if (w == null || !w.IsAcquired) return;
        var card = CardCatalog.Get("wpn_" + weaponId);
        Sprite icon = card != null ? card.icon : w.WeaponIcon;
        AddWeapon(w.WeaponName, icon, w, tracks);
    }

    /// <summary>Bir silah blogu: baslik (ikon+ad) + her track icin bir satir (etiket + pip'ler).</summary>
    private void AddWeapon(string name, Sprite icon, WeaponBase weapon, object[] tracks)
    {
        // Baslik satiri
        var head = NewRect("WHead", weaponColumn);
        var hh = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        hh.spacing = 14; hh.childAlignment = TextAnchor.MiddleLeft;
        hh.childControlWidth = true; hh.childControlHeight = true;
        hh.childForceExpandWidth = false; hh.childForceExpandHeight = false;
        var hLE = head.gameObject.AddComponent<LayoutElement>(); hLE.minHeight = 84; hLE.preferredHeight = 84;
        var ic = NewRect("Icon", head);
        var icImg = ic.gameObject.AddComponent<Image>(); icImg.sprite = icon; icImg.enabled = icon != null; icImg.preserveAspect = true; icImg.raycastTarget = false;
        var icLE = ic.gameObject.AddComponent<LayoutElement>(); icLE.preferredWidth = 76; icLE.preferredHeight = 76; icLE.minWidth = 76;
        var nameT = NewText("Name", head, weaponNameSize, TextAlignmentOptions.Left);
        nameT.text = name; nameT.fontStyle = FontStyles.Bold;
        var nameLE = nameT.gameObject.AddComponent<LayoutElement>(); nameLE.flexibleWidth = 1;

        // Track satirlari
        for (int i = 0; i < tracks.Length; i += 4)
        {
            var t = new Track {
                stat = (UpgradeSelectionUI.UpgradeType)tracks[i],
                statLabel = (string)tracks[i+1],
                effect = (string)tracks[i+2],
                trackKey = (string)tracks[i+3],
                weapon = weapon, cost = firstCost,
                instant = ((UpgradeSelectionUI.UpgradeType)tracks[i] == UpgradeSelectionUI.UpgradeType.Amount)
            };
            var row = NewRect("Track", weaponColumn);
            var rh = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rh.spacing = 12; rh.childAlignment = TextAnchor.MiddleLeft; rh.padding = new RectOffset(24,0,0,0);
            rh.childControlWidth = true; rh.childControlHeight = true;
            rh.childForceExpandWidth = false; rh.childForceExpandHeight = false;
            var rLE = row.gameObject.AddComponent<LayoutElement>(); rLE.minHeight = 58; rLE.preferredHeight = 58;

            t.label = NewText("Lbl", row, trackLabelSize, TextAlignmentOptions.Left);
            var lblLE = t.label.gameObject.AddComponent<LayoutElement>(); lblLE.flexibleWidth = 1;

            var pips = NewRect("Pips", row);
            var ph = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            ph.spacing = 6; ph.childAlignment = TextAnchor.MiddleRight;
            ph.childControlWidth = true; ph.childControlHeight = true;
            ph.childForceExpandWidth = false; ph.childForceExpandHeight = false;
            t.pipRow = pips;

            _tracks.Add(t);
        }
    }
    #endregion

    #region Runtime
    private void HandleStatPicked(UpgradeSelectionUI.UpgradeType type)
    {
        foreach (var t in _tracks)
        {
            if (t.stat != type) continue;
            if (t.instant) { t.level++; ApplyLevel(t); } // Amount: aninda level
            else { t.progress++; if (t.progress >= t.cost) { t.progress -= t.cost; t.level++; t.cost++; ApplyLevel(t); } }
        }
        RefreshAll();
    }

    private void ApplyLevel(Track t)
    {
        if (t.weapon != null)
        {
            if (!string.IsNullOrEmpty(t.trackKey)) t.weapon.ApplyTrack(t.trackKey, 1);
            t.weapon.RefreshStats();
        }
        else if (playerRef != null) playerRef.AddDamage(clawsDamagePerLevel); // Sharp Claws
    }

    private void RefreshAll() { foreach (var t in _tracks) RefreshTrack(t); }

    private void RefreshTrack(Track t)
    {
        if (t.label != null) t.label.text = t.effect + "  <color=#B9B9C9>" + t.statLabel + "</color>  Lv." + t.level;
        int shown = t.instant ? 0 : Mathf.Min(t.cost, maxPips); // Amount: pip yok
        while (t.pips.Count < shown) t.pips.Add(NewPip(t.pipRow));
        for (int i = 0; i < t.pips.Count; i++)
        {
            bool use = i < shown;
            t.pips[i].gameObject.SetActive(use);
            if (use) t.pips[i].color = i < t.progress ? PipFull : PipEmpty;
        }
    }
    #endregion

    #region UI helpers
    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
    private TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; // tek satir (alta sarma yok)
        return t;
    }
    private Image NewPip(Transform parent)
    {
        var rt = NewRect("Pip", parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = PipEmpty; img.raycastTarget = false;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 28; le.preferredHeight = 28; le.minWidth = 28; le.minHeight = 28;
        return img;
    }
    private TMP_FontAsset FindFont()
    {
        var cv = GetComponentInParent<Canvas>();
        if (cv != null) foreach (var t in cv.GetComponentsInChildren<TMP_Text>(true)) if (t.font != null) return t.font;
        return null;
    }
    private WeaponBase FindWeapon(string weaponId)
    {
        if (weaponManager == null || weaponManager.Weapons == null || string.IsNullOrEmpty(weaponId)) return null;
        string key = weaponId.ToLowerInvariant();
        var list = weaponManager.Weapons;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].GetType().Name.ToLowerInvariant().Contains(key)) return list[i];
        return null;
    }
    #endregion
}
