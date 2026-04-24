using UnityEngine;

[DisallowMultipleComponent]
public sealed class FruitPickup : MonoBehaviour {
    [SerializeField] private int fruitTypeIndex;

    private Vector3 baseScale;
    private Renderer cachedRenderer;

    public int FruitTypeIndex => fruitTypeIndex;
    public string FruitTypeName => BuildFallbackFruitName();
    public bool IsCollected { get; private set; }

    private void Awake() {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
        if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
        EnsureCollider();
    }

    public void SetHovered(bool hovered) {
        if (IsCollected) return;
        transform.localScale = hovered ? baseScale * 1.18f : baseScale;
    }

    public void Collect() {
        if (IsCollected) return;
        IsCollected = true;
        gameObject.SetActive(false);
    }

    public void EnsureCollider() {
        if (GetComponent<Collider>() != null) return;

        Renderer renderer = cachedRenderer != null ? cachedRenderer : GetComponent<Renderer>();
        if (renderer == null) {
            SphereCollider fallback = gameObject.AddComponent<SphereCollider>();
            fallback.center = Vector3.zero;
            fallback.radius = 0.2f;
            return;
        }

        Bounds b = renderer.bounds;
        SphereCollider c = gameObject.AddComponent<SphereCollider>();
        c.center = transform.InverseTransformPoint(b.center);
        Vector3 extentsLocal = Abs(transform.InverseTransformVector(b.extents));
        c.radius = Mathf.Max(0.05f, extentsLocal.magnitude);
    }

    private static Vector3 Abs(Vector3 v) {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private string BuildFallbackFruitName() {
        string raw = gameObject != null ? gameObject.name : string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) {
            return "Fruit " + fruitTypeIndex;
        }

        raw = raw.Replace("(Clone)", string.Empty).Trim();
        int suffixStart = raw.LastIndexOf(" (", System.StringComparison.Ordinal);
        if (suffixStart >= 0 && raw.EndsWith(")", System.StringComparison.Ordinal)) {
            string suffix = raw.Substring(suffixStart + 2, raw.Length - suffixStart - 3);
            int duplicateIndex;
            if (int.TryParse(suffix, out duplicateIndex)) {
                raw = raw.Substring(0, suffixStart).TrimEnd();
            }
        }
        raw = raw.Replace('_', ' ');
        return raw;
    }

    public void TryApplyMaterial(Material material) {
        if (material == null) return;
        if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null) return;
        cachedRenderer.sharedMaterial = material;
    }
}
