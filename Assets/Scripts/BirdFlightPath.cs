using UnityEngine;

[DisallowMultipleComponent]
public class BirdFlightPath : MonoBehaviour {
    private const int IgnoreRaycastLayer = 2;
    private const float TwoPi = Mathf.PI * 2f;

    private float radiusX = 10f;
    private float radiusZ = 10f;
    private float midLowestY = 2f;

    private float speed = 5f;

    private Vector3 centerBaseWorldPosition;
    private bool centerInitialized;

    private float angleRad;

    private void Awake() {
        if (!centerInitialized) {
            centerBaseWorldPosition = transform.position;
            centerInitialized = true;
        }
        DisablePhysicsForFlight();
    }

    private void DisablePhysicsForFlight() {
        if (!Application.isPlaying) return;
        SetLayerRecursively(transform, IgnoreRaycastLayer);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) {
            Collider c = colliders[i];
            if (c == null) continue;
            c.enabled = false;
        }

        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++) {
            Rigidbody body = bodies[i];
            if (body == null) continue;
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private static void SetLayerRecursively(Transform root, int layer) {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void OnEnable() {
        DisablePhysicsForFlight();

        if (!centerInitialized) {
            centerBaseWorldPosition = transform.position;
            centerInitialized = true;
        }
        angleRad = 0f;
    }

    private void OnValidate() {
        if (!Application.isPlaying) {
            if (!centerInitialized) {
                centerBaseWorldPosition = transform.position;
                centerInitialized = true;
            }
        }
    }

    private void Update() {
        float rx = Mathf.Abs(radiusX);
        float rz = Mathf.Abs(radiusZ);
        if (rx <= 0.00001f && rz <= 0.00001f) return;

        ApplyPose();
    }

    private void ApplyPose() {
        float rx = Mathf.Abs(radiusX);
        float rz = Mathf.Abs(radiusZ);
        if (rx <= 0.00001f && rz <= 0.00001f) return;

        Vector3 pos = EvaluatePosition(angleRad);
        Vector3 vel = EvaluateVelocity(angleRad);

        float dt = Mathf.Max(0f, Time.deltaTime);
        float vMag = vel.magnitude;
        if (vMag > 0.00001f) {
            angleRad = Mathf.Repeat(angleRad + (speed * dt) / vMag, TwoPi);
        } else {
            float fallbackR = Mathf.Max(0.001f, Mathf.Max(rx, rz));
            angleRad = Mathf.Repeat(angleRad + (speed * dt) / fallbackR, TwoPi);
        }

        transform.position = pos;

        if (vel.sqrMagnitude > 0.000001f) {
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
        }
    }

    private Vector3 GetCenter() {
        return centerBaseWorldPosition;
    }

    public void SetCenterXZ(Vector3 centerWorld) {
        if (!centerInitialized) {
            centerBaseWorldPosition = transform.position;
            centerInitialized = true;
        }
        centerBaseWorldPosition = new Vector3(centerWorld.x, centerBaseWorldPosition.y, centerWorld.z);
        angleRad = 0f;
    }

    private Vector3 EvaluatePosition(float tRad) {
        Vector3 c = GetCenter();
        float rx = Mathf.Abs(radiusX);
        float rz = Mathf.Abs(radiusZ);

        float s = Mathf.Sin(tRad);

        float x = rx * s;
        float z = rz * Mathf.Sin(2f * tRad);

        float y = Mathf.Lerp(midLowestY, c.y, Mathf.Abs(s));

        return new Vector3(c.x + x, y, c.z + z);
    }

    private Vector3 EvaluateVelocity(float tRad) {
        const float eps = 0.0005f;
        Vector3 p0 = EvaluatePosition(tRad);
        Vector3 p1 = EvaluatePosition(tRad + eps);
        return (p1 - p0) / eps;
    }

}
