using UnityEngine;

[DisallowMultipleComponent]
public sealed class BalloonController : MonoBehaviour {
    private const float MaxRiseHeight = 15f;
    private const float AscendSpeed = 1.2f;
    private const float DescendSpeed = 1.0f;
    private const float LandedEpsilon = 0.08f;

    private const float GroundRayLength = 200f;
    private const float CollisionSkin = 0.01f;

    private Transform passengerAnchor;
    private Transform bottomReference;

    private GameObject burnerOnVisual;
    private Light burnerGlowLight;
    private BoxCollider rootBoxCollider;

    private bool burnerOn;
    private float lastSurfaceY;
    private float takeoffSurfaceY;
    private float takeoffOffsetY;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] movementHits = new RaycastHit[64];
    private readonly Collider[] overlapHits = new Collider[64];
    private bool hasStartupState;
    private Vector3 startupPosition;
    private Quaternion startupRotation;
    private bool hasHorizontalBounds;
    private float boundsMinX;
    private float boundsMaxX;
    private float boundsMinZ;
    private float boundsMaxZ;

    public bool BurnerOn => burnerOn;
    public bool IsLanded {
        get {
            if (burnerOn) return false;
            float refY = GetBottomY();
            return Mathf.Abs(refY - lastSurfaceY) <= LandedEpsilon;
        }
    }

    public Transform PassengerAnchor => passengerAnchor;

    public bool CanOccupyPosition(Vector3 targetPosition) {
        Vector3 centerWorld;
        Vector3 halfExtentsWorld;
        Quaternion orientation;
        if (!TryGetOccupancyBox(out centerWorld, out halfExtentsWorld, out orientation)) {
            return true;
        }

        Vector3 offset = targetPosition - transform.position;
        Vector3 testCenter = centerWorld + offset;

        int count = Physics.OverlapBoxNonAlloc(
            testCenter,
            halfExtentsWorld + Vector3.one * CollisionSkin,
            overlapHits,
            orientation,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++) {
            Collider c = overlapHits[i];
            if (c == null) continue;
            if (c is TerrainCollider) continue;
            if (c.transform != null && c.transform.IsChildOf(transform)) continue;
            if (IsLowGroundSurface(c, targetPosition.y)) continue;
            return false;
        }

        return true;
    }

    private static bool IsLowGroundSurface(Collider collider, float landingY) {
        if (collider == null) return false;

        Bounds b = collider.bounds;
        if (b.max.y > landingY + 0.45f) return false;

        Vector3 size = b.size;
        return size.y <= 0.75f && (size.x >= 1.5f || size.z >= 1.5f);
    }

    private void Awake() {
        if (!hasStartupState) {
            hasStartupState = true;
            startupPosition = transform.position;
            startupRotation = transform.rotation;
        }

        Transform basket = FindChildByName(transform, "Basket");
        if (basket == null) basket = transform;

        passengerAnchor = EnsurePassengerAnchor(basket);
        bottomReference = EnsureBottomReference(basket);

        if (burnerOnVisual == null) {
            burnerOnVisual = TryFindBurnerOnVisual();
        }
        EnsureBurnerGlowLight();

        rootBoxCollider = GetComponent<BoxCollider>();

        CacheHorizontalBounds();
        UpdateSurfaceY();
        takeoffSurfaceY = lastSurfaceY;
        takeoffOffsetY = Mathf.Max(0f, GetBottomY() - lastSurfaceY);
        ApplyBurnerVisual();
    }

    private static Transform FindChildByName(Transform root, string name) {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++) {
            Transform r = FindChildByName(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private void Update() {
        UpdateSurfaceY();
        UpdateVerticalMotion();
    }

    public void ToggleBurner() {
        SetBurnerOn(!burnerOn);
    }

    public void SetBurnerOn(bool on) {
        if (on && !burnerOn) {
            UpdateSurfaceY();
            takeoffSurfaceY = lastSurfaceY;
            takeoffOffsetY = Mathf.Max(0f, GetBottomY() - lastSurfaceY);
        }
        burnerOn = on;
        ApplyBurnerVisual();
    }

    public void ResetToStartupState() {
        if (!hasStartupState) return;

        burnerOn = false;
        ApplyBurnerVisual();

        transform.SetPositionAndRotation(startupPosition, startupRotation);
        if (bottomReference != null) {
            Transform basket = FindChildByName(transform, "Basket");
            if (basket == null) basket = transform;
            bottomReference.position = ComputeBasketBottomCenterWorld(basket);
        }
        UpdateSurfaceY();
    }

    private void ApplyBurnerVisual() {
        if (burnerOnVisual == null) return;
        burnerOnVisual.SetActive(burnerOn);
        if (burnerGlowLight != null) burnerGlowLight.enabled = burnerOn;
    }

    private void EnsureBurnerGlowLight() {
        if (burnerGlowLight != null) return;
        if (burnerOnVisual == null) return;

        burnerGlowLight = burnerOnVisual.GetComponentInChildren<Light>(true);
        if (burnerGlowLight == null) {
            GameObject go = new GameObject("Burner Glow Light");
            go.transform.SetParent(burnerOnVisual.transform, false);
            go.transform.localPosition = Vector3.zero;
            burnerGlowLight = go.AddComponent<Light>();
        }

        burnerGlowLight.type = LightType.Point;
        burnerGlowLight.color = new Color(1f, 0.52f, 0.15f, 1f);
        burnerGlowLight.intensity = 1.0f;
        burnerGlowLight.range = 3.0f;
        burnerGlowLight.shadows = LightShadows.None;
        burnerGlowLight.enabled = burnerOn;
    }

    private void UpdateSurfaceY() {
        Vector3 origin = (bottomReference != null ? bottomReference.position : transform.position) + Vector3.up * 0.25f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, GroundRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return;

        float bestDistance = float.PositiveInfinity;
        float bestY = lastSurfaceY;
        for (int i = 0; i < hitCount; i++) {
            RaycastHit h = groundHits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDistance) {
                bestDistance = h.distance;
                bestY = h.point.y;
            }
        }

        if (bestDistance < float.PositiveInfinity) lastSurfaceY = bestY;
    }

    private void UpdateVerticalMotion() {
        float refY = GetBottomY();
        float targetRefY = burnerOn ? (takeoffSurfaceY + takeoffOffsetY + MaxRiseHeight) : lastSurfaceY;
        float dt = Mathf.Max(0f, Time.deltaTime);
        float speed = (targetRefY > refY) ? AscendSpeed : DescendSpeed;
        float newRefY = Mathf.MoveTowards(refY, targetRefY, speed * dt);

        float delta = newRefY - refY;
        if (Mathf.Abs(delta) <= 0.000001f) return;

        ApplyMovement(new Vector3(0f, delta, 0f));
    }

    private float GetBottomY() {
        return bottomReference != null ? bottomReference.position.y : transform.position.y;
    }

    private static Transform EnsurePassengerAnchor(Transform basket) {
        if (basket == null) return null;
        Transform existing = basket.Find("PassengerAnchor");
        if (existing != null) {
            existing.position = ComputeBasketPassengerAnchorWorld(basket);
            existing.rotation = basket.rotation;
            return existing;
        }

        GameObject go = new GameObject("PassengerAnchor");
        go.transform.SetParent(basket, true);
        go.transform.position = ComputeBasketPassengerAnchorWorld(basket);
        go.transform.rotation = basket.rotation;
        return go.transform;
    }

    private static Transform EnsureBottomReference(Transform basket) {
        if (basket == null) return null;
        Transform existing = basket.Find("BalloonBottomReference");
        if (existing != null) {
            existing.position = ComputeBasketBottomCenterWorld(basket);
            existing.rotation = basket.rotation;
            return existing;
        }

        GameObject go = new GameObject("BalloonBottomReference");
        go.transform.SetParent(basket, true);
        go.transform.position = ComputeBasketBottomCenterWorld(basket);
        go.transform.rotation = basket.rotation;
        return go.transform;
    }

    private GameObject TryFindBurnerOnVisual() {
        Transform burner = FindChildByName(transform, "Burner");
        if (burner == null) return null;

        Transform fire = FindChildByName(burner, "Fire");
        return fire != null ? fire.gameObject : null;
    }

    private static Vector3 ComputeBasketBottomCenterWorld(Transform basket) {
        Renderer[] renderers = basket.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return basket.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    private static Vector3 ComputeBasketCenterWorld(Transform basket) {
        Renderer[] renderers = basket.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return basket.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.center;
    }

    private static Vector3 ComputeBasketPassengerAnchorWorld(Transform basket) {
        Renderer[] renderers = basket.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return basket.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        Vector3 anchor = new Vector3(b.center.x, b.min.y, b.center.z);
        float eyeHeightAboveFloor = Mathf.Clamp(b.size.y * 0.72f, 1.15f, 1.3f);
        anchor.y += eyeHeightAboveFloor;
        return anchor;
    }

    private static Vector3 Abs(Vector3 v) {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private bool TryGetOccupancyBox(out Vector3 centerWorld, out Vector3 halfExtentsWorld, out Quaternion orientation) {
        if (rootBoxCollider != null && rootBoxCollider.enabled) {
            centerWorld = rootBoxCollider.transform.TransformPoint(rootBoxCollider.center);
            halfExtentsWorld = Vector3.Scale(rootBoxCollider.size * 0.5f, Abs(rootBoxCollider.transform.lossyScale));
            orientation = rootBoxCollider.transform.rotation;
            return true;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0) {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            }

            centerWorld = bounds.center;
            halfExtentsWorld = bounds.extents;
            orientation = Quaternion.identity;
            return true;
        }

        centerWorld = transform.position;
        halfExtentsWorld = Vector3.zero;
        orientation = Quaternion.identity;
        return false;
    }

    private void ApplyMovement(Vector3 desiredDelta) {
        if (rootBoxCollider == null || !rootBoxCollider.enabled) {
            transform.position += desiredDelta;
            ClampHorizontalToBounds();
            return;
        }

        float dist = desiredDelta.magnitude;
        if (dist <= 0.000001f) return;

        Vector3 dir = desiredDelta / dist;
        Vector3 centerWorld = rootBoxCollider.transform.TransformPoint(rootBoxCollider.center);
        Vector3 halfExtentsWorld = Vector3.Scale(rootBoxCollider.size * 0.5f, Abs(rootBoxCollider.transform.lossyScale));
        Quaternion orientation = rootBoxCollider.transform.rotation;

        int hitCount = Physics.BoxCastNonAlloc(
            centerWorld,
            halfExtentsWorld,
            dir,
            movementHits,
            orientation,
            dist + CollisionSkin,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );
        float safe = dist;

        if (hitCount > 0) {
            if (hitCount >= movementHits.Length) {
                safe = 0f;
            } else {
                for (int i = 0; i < hitCount; i++) {
                    RaycastHit h = movementHits[i];
                    if (h.collider == null) continue;
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    safe = Mathf.Min(safe, Mathf.Max(0f, h.distance - CollisionSkin));
                }
            }
        }

        transform.position += dir * safe;
        ClampHorizontalToBounds();
    }

    private void CacheHorizontalBounds() {
        hasHorizontalBounds = false;

        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains != null && terrains.Length > 0) {
            bool any = false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

            for (int i = 0; i < terrains.Length; i++) {
                Terrain t = terrains[i];
                if (t == null || t.terrainData == null) continue;

                Vector3 tp = t.transform.position;
                Vector3 size = t.terrainData.size;

                float tMinX = tp.x;
                float tMaxX = tp.x + size.x;
                float tMinZ = tp.z;
                float tMaxZ = tp.z + size.z;

                if (!any) {
                    minX = tMinX;
                    maxX = tMaxX;
                    minZ = tMinZ;
                    maxZ = tMaxZ;
                    any = true;
                } else {
                    minX = Mathf.Min(minX, tMinX);
                    maxX = Mathf.Max(maxX, tMaxX);
                    minZ = Mathf.Min(minZ, tMinZ);
                    maxZ = Mathf.Max(maxZ, tMaxZ);
                }
            }

            if (any) {
                boundsMinX = minX;
                boundsMaxX = maxX;
                boundsMinZ = minZ;
                boundsMaxZ = maxZ;
                hasHorizontalBounds = true;
                return;
            }
        }
    }

    private void ClampHorizontalToBounds() {
        if (!hasHorizontalBounds) return;

        float marginX = 0f;
        float marginZ = 0f;

        if (rootBoxCollider != null && rootBoxCollider.enabled) {
            Vector3 half = Vector3.Scale(rootBoxCollider.size * 0.5f, Abs(rootBoxCollider.transform.lossyScale));
            marginX = Mathf.Abs(half.x);
            marginZ = Mathf.Abs(half.z);
        }

        float minX = boundsMinX + marginX;
        float maxX = boundsMaxX - marginX;
        float minZ = boundsMinZ + marginZ;
        float maxZ = boundsMaxZ - marginZ;

        if (minX > maxX) {
            float c = (boundsMinX + boundsMaxX) * 0.5f;
            minX = c;
            maxX = c;
        }
        if (minZ > maxZ) {
            float c = (boundsMinZ + boundsMaxZ) * 0.5f;
            minZ = c;
            maxZ = c;
        }

        Vector3 p = transform.position;
        float clampedX = Mathf.Clamp(p.x, minX, maxX);
        float clampedZ = Mathf.Clamp(p.z, minZ, maxZ);

        p.x = clampedX;
        p.z = clampedZ;
        transform.position = p;
    }
}
