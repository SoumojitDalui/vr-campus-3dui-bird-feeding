using UnityEngine;

[DisallowMultipleComponent]
public sealed class BootstrapBalloonTourRideRunner : MonoBehaviour {
    private float deadline;

    public static void EnsureRunner() {
        if (Object.FindFirstObjectByType<BootstrapBalloonTourRideRunner>() != null) return;
        GameObject go = new GameObject("BootstrapBalloonTourRideRunner");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
        go.AddComponent<BootstrapBalloonTourRideRunner>();
    }

    private void Awake() {
        deadline = Time.unscaledTime + 12f;
    }

    private void Update() {
        if (BootstrapBalloonTourRide.TrySetup()) {
            Destroy(gameObject);
            return;
        }

        if (Time.unscaledTime >= deadline) {
            Destroy(gameObject);
        }
    }
}

