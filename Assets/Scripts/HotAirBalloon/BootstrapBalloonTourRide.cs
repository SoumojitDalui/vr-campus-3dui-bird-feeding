using UnityEngine;

public static class BootstrapBalloonTourRide {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists() {
        if (!TrySetup()) {
            BootstrapBalloonTourRideRunner.EnsureRunner();
        }
    }

    public static bool TrySetup() {
        Camera cam = FindBestCamera();

        GameObject balloon = FindBalloonRoot();
        if (balloon == null) return false;

        if (balloon.GetComponent<BalloonController>() == null) {
            balloon.AddComponent<BalloonController>();
        }

        GameObject legacyModeManager = GameObject.Find("ModeManager");
        if (legacyModeManager != null) {
            Object.Destroy(legacyModeManager);
        }

        GameObject legacySceneAnchor = GameObject.Find("SceneModeAnchor");
        if (legacySceneAnchor != null) {
            Object.Destroy(legacySceneAnchor);
        }

        Transform basket = FindChildByName(balloon.transform, "Basket");
        if (basket != null) {
            Transform basketGaze = basket.Find("BasketGaze");
            if (basketGaze != null) Object.Destroy(basketGaze.gameObject);
            Transform bottomGaze = basket.Find("BasketBottomGaze");
            if (bottomGaze != null) Object.Destroy(bottomGaze.gameObject);
            DestroyLegacyComponentsByName(basket, "GazeTeleportTarget");
        }

        Transform burner = FindChildByName(balloon.transform, "Burner");
        if (burner == null) burner = FindChildByNameContains(balloon.transform, "burner");
        if (burner == null) {
            Transform fire = FindChildByName(balloon.transform, "Fire");
            if (fire == null) fire = FindChildByNameContains(balloon.transform, "fire");
            if (fire != null) burner = fire.parent != null ? fire.parent : fire;
        }
        if (burner != null) {
            Transform burnerGaze = burner.Find("BurnerGaze");
            if (burnerGaze != null) {
                Object.Destroy(burnerGaze.gameObject);
            }
            DestroyLegacyComponentsByName(burner, "GazeBurnerToggleTarget");
        }

        return cam != null;
    }

    private static Transform FindChildByName(Transform root, string name) {
        if (root == null) return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++) {
            Transform r = FindChildByName(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private static Transform FindChildByNameContains(Transform root, string containsLower) {
        if (root == null) return null;
        string n = root.name;
        if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains(containsLower)) return root;
        for (int i = 0; i < root.childCount; i++) {
            Transform r = FindChildByNameContains(root.GetChild(i), containsLower);
            if (r != null) return r;
        }
        return null;
    }

    private static void DestroyLegacyComponentsByName(Transform root, string typeName) {
        if (root == null || string.IsNullOrEmpty(typeName)) return;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++) {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (!string.Equals(behaviour.GetType().Name, typeName, System.StringComparison.Ordinal)) continue;
            Object.Destroy(behaviour);
        }
    }

    private static GameObject FindBalloonRoot() {
        GameObject balloon = GameObject.Find("Hot Air Balloon");
        if (balloon != null) return balloon;
        balloon = GameObject.Find("HotAirBalloon");
        if (balloon != null) return balloon;
        BalloonController existing = Object.FindFirstObjectByType<BalloonController>();
        if (existing != null) return existing.gameObject;

        Transform[] sceneRoots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneRoots.Length; i++) {
            Transform t = sceneRoots[i];
            if (t == null) continue;
            if (t.parent != null) continue;
            string n = t.name;
            if (string.IsNullOrEmpty(n)) continue;
            if (n.ToLowerInvariant().Contains("balloon") && FindChildByName(t, "Basket") != null) {
                return t.gameObject;
            }
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++) {
            Transform t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!string.Equals(t.name, "Basket", System.StringComparison.OrdinalIgnoreCase)) continue;
            Transform root = t.root;
            if (root == null) continue;
            if (FindChildByName(root, "Basket") == null) continue;
            return root.gameObject;
        }
        return null;
    }

    private static Camera FindBestCamera() {
        Camera[] cams = Camera.allCameras;
        Camera firstEnabled = null;
        Camera mainEnabled = null;
        Camera stereoBoth = null;
        Camera stereoAny = null;
        for (int i = 0; i < cams.Length; i++) {
            Camera c = cams[i];
            if (c == null || !c.enabled) continue;
            if (firstEnabled == null) firstEnabled = c;
            if (mainEnabled == null && c.CompareTag("MainCamera")) mainEnabled = c;
            if (c.stereoEnabled) {
                if (c.stereoTargetEye == StereoTargetEyeMask.Both) stereoBoth = c;
                if (stereoAny == null) stereoAny = c;
            }
        }
        if (stereoBoth != null) return stereoBoth;
        if (stereoAny != null) return stereoAny;
        if (mainEnabled != null) return mainEnabled;
        if (firstEnabled != null) return firstEnabled;
        return Object.FindFirstObjectByType<Camera>();
    }
}
