using UnityEngine;
using UnityEngine.UI;

public sealed class HudController {
    private readonly HudPanelsController panels = new HudPanelsController();
    private readonly MinimapController minimap = new MinimapController();
    private readonly GazeReticleController gazeReticle = new GazeReticleController();

    public void Bind(Bounds worldBounds) {
        panels.Bind();
        minimap.Bind(worldBounds);
        gazeReticle.Bind();
    }

    public void RefreshCamera(Camera headCamera, Transform head, bool vrView) {
        panels.RefreshCamera(headCamera, head, vrView);
        gazeReticle.RefreshCamera(headCamera, vrView);
    }

    public void RefreshState(
        Bounds worldBounds,
        Camera headCamera,
        Transform head,
        bool vrView,
        bool checklistVisible,
        bool minimapVisible,
        bool ridingBalloon,
        string controls,
        string prompt,
        string checklist,
        Vector2 teleportCursor01,
        Vector3 markerWorldPosition) {
        panels.RefreshText(controls, prompt, checklist, checklistVisible);
        minimap.Refresh(minimapVisible || ridingBalloon, ridingBalloon, teleportCursor01, markerWorldPosition, worldBounds);
        gazeReticle.RefreshState(head, vrView);
    }

    public void GetMapWorldExtents(Bounds worldBounds, out float minX, out float maxX, out float minZ, out float maxZ) {
        minimap.GetMapWorldExtents(worldBounds, out minX, out maxX, out minZ, out maxZ);
    }
}

public sealed class HudPanelsController {
    private Canvas hudCanvas;
    private RectTransform checklistPanel;
    private Text checklistText;
    private Text promptText;
    private Text statusText;
    private Text controlsText;

    public void Bind() {
        GameObject hudRoot = GameObject.Find("HUD");
        if (hudRoot == null) {
            Debug.LogError("HUD GameObject is missing from the scene.");
            return;
        }

        hudCanvas = hudRoot.GetComponent<Canvas>();
        if (hudCanvas == null) {
            Debug.LogError("HUD is missing a Canvas component.");
            return;
        }

        statusText = FindRequiredComponentInChildren<Text>(hudRoot.transform, "StatusText");
        controlsText = FindRequiredComponentInChildren<Text>(hudRoot.transform, "ControlsText");
        promptText = FindRequiredComponentInChildren<Text>(hudRoot.transform, "PromptText");
        RectTransform statusPanel = FindRequiredComponentInChildren<RectTransform>(hudRoot.transform, "StatusPanel");
        RectTransform controlsPanel = FindRequiredComponentInChildren<RectTransform>(hudRoot.transform, "ControlsPanel");
        RectTransform promptPanel = FindRequiredComponentInChildren<RectTransform>(hudRoot.transform, "PromptPanel");
        checklistPanel = FindRequiredComponentInChildren<RectTransform>(hudRoot.transform, "ChecklistPanel");
        checklistText = FindRequiredComponentInChildren<Text>(hudRoot.transform, "ChecklistText");

        ConfigurePanelRect(statusPanel, new Vector2(-28f, 28f), new Vector2(390f, 74f), TextAnchor.LowerRight);
        ConfigurePanelRect(controlsPanel, new Vector2(28f, 28f), new Vector2(200f, 166f), TextAnchor.LowerLeft);
        ConfigurePanelRect(promptPanel, new Vector2(-28f, 28f), new Vector2(390f, 74f), TextAnchor.LowerRight);
        ConfigurePanelRect(checklistPanel, new Vector2(0f, -16f), new Vector2(420f, 250f), TextAnchor.UpperCenter);

        ConfigureTextRect(statusText, 19, TextAnchor.UpperLeft, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        ConfigureTextRect(controlsText, 15, TextAnchor.UpperLeft, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        ConfigureTextRect(promptText, 17, TextAnchor.UpperLeft, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        ConfigureTextRect(checklistText, 18, TextAnchor.UpperLeft, new Vector2(12f, 10f), new Vector2(-12f, -10f));

        if (statusPanel != null) statusPanel.gameObject.SetActive(false);
        if (checklistText != null) checklistText.supportRichText = true;
    }

    public void RefreshCamera(Camera headCamera, Transform head, bool vrView) {
        if (hudCanvas == null || head == null) return;

        CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
        hudCanvas.enabled = vrView;
        if (vrView) {
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.worldCamera = null;

            RectTransform rect = hudCanvas.GetComponent<RectTransform>();
            if (rect != null) {
                rect.sizeDelta = new Vector2(1100f, 650f);
            }

            Transform t = hudCanvas.transform;
            t.SetParent(head, false);
            t.localPosition = new Vector3(0f, 0.04f, 1.1f);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * 0.00135f;

            if (headCamera != null && headCamera.nearClipPlane > 0.05f) {
                headCamera.nearClipPlane = 0.05f;
            }

            if (scaler != null) {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }
        } else {
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.worldCamera = null;

            Transform t = hudCanvas.transform;
            if (t.parent != null) t.SetParent(null, true);
            t.localScale = Vector3.one;

            if (scaler != null) {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
        }
    }

    public void RefreshText(string controls, string prompt, string checklist, bool checklistVisible) {
        if (statusText != null) {
            statusText.text = string.Empty;
        }

        if (controlsText != null) {
            controlsText.text = controls;
        }

        if (promptText != null) {
            promptText.text = prompt;
        }

        if (checklistText != null) {
            checklistText.text = checklist;
        }

        if (checklistPanel != null) {
            checklistPanel.gameObject.SetActive(checklistVisible);
        }
    }

    private static T FindRequiredComponentInChildren<T>(Transform root, string objectName) where T : Component {
        if (root == null || string.IsNullOrEmpty(objectName)) return null;

        Transform target = FindDeepChild(root, objectName);
        if (target == null) {
            Debug.LogError(objectName + " is missing under " + root.name + ".");
            return null;
        }

        T component = target.GetComponent<T>();
        if (component == null) {
            Debug.LogError(objectName + " is missing component " + typeof(T).Name + ".");
        }
        return component;
    }

    private static Transform FindDeepChild(Transform root, string name) {
        if (root == null || string.IsNullOrEmpty(name)) return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) {
            Transform child = children[i];
            if (child != null && child.name == name) return child;
        }

        return null;
    }

    private static void ConfigurePanelRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor) {
        if (rect == null) return;

        switch (anchor) {
            case TextAnchor.UpperLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                break;
            case TextAnchor.UpperCenter:
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                break;
            case TextAnchor.UpperRight:
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                break;
            case TextAnchor.LowerLeft:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                break;
            case TextAnchor.LowerRight:
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                break;
            default:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                break;
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void ConfigureTextRect(Text text, int fontSize, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax) {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
    }
}

public sealed class MinimapController {
    private RectTransform mapPanel;
    private RawImage mapImage;
    private RawImage playerDot;
    private RawImage cursorDot;
    private Camera miniMapCamera;
    private RenderTexture miniMapTexture;

    public void Bind(Bounds worldBounds) {
        GameObject hudRoot = GameObject.Find("HUD");
        if (hudRoot == null) {
            Debug.LogError("HUD GameObject is missing from the scene.");
            return;
        }

        mapPanel = FindRequiredComponentInChildren<RectTransform>(hudRoot.transform, "MiniMapPanel");
        mapImage = FindRequiredComponentInChildren<RawImage>(hudRoot.transform, "MiniMapPanel");
        playerDot = FindRequiredComponentInChildren<RawImage>(hudRoot.transform, "PlayerDot");
        cursorDot = FindRequiredComponentInChildren<RawImage>(hudRoot.transform, "CursorDot");
        GameObject miniCameraObject = GameObject.Find("MinimapCamera");
        miniMapCamera = miniCameraObject != null ? miniCameraObject.GetComponent<Camera>() : null;

        ConfigurePanelRect(mapPanel, new Vector2(0f, -16f), new Vector2(230f, 230f), TextAnchor.UpperCenter);

        if (playerDot != null) {
            ConfigureDot(playerDot, new Color(1f, 0.95f, 0.18f, 1f), 12f);
        }

        if (cursorDot != null) {
            ConfigureDot(cursorDot, new Color(1f, 1f, 1f, 1f), 14f);
        }

        if (miniMapCamera == null) {
            Debug.LogError("MinimapCamera is missing from the scene.");
            return;
        }

        if (miniMapTexture == null) {
            miniMapTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            miniMapTexture.Create();
        }

        miniMapCamera.orthographic = true;
        miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
        miniMapCamera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        miniMapCamera.cullingMask = Physics.DefaultRaycastLayers;
        miniMapCamera.targetTexture = miniMapTexture;
        miniMapCamera.transform.position = new Vector3(worldBounds.center.x, worldBounds.max.y + 80f, worldBounds.center.z);
        miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        miniMapCamera.orthographicSize = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z) + 8f;

        if (mapImage != null) {
            mapImage.color = Color.white;
            mapImage.texture = miniMapTexture;
        }
    }

    public void Refresh(bool visible, bool ridingBalloon, Vector2 teleportCursor01, Vector3 markerWorldPosition, Bounds worldBounds) {
        if (mapPanel == null) return;

        mapPanel.gameObject.SetActive(visible);
        if (!visible) return;

        ApplyMapLayout(ridingBalloon);
        UpdateMapMarkers(markerWorldPosition, teleportCursor01, worldBounds);
    }

    public void GetMapWorldExtents(Bounds worldBounds, out float minX, out float maxX, out float minZ, out float maxZ) {
        if (miniMapCamera != null) {
            Vector3 camPos = miniMapCamera.transform.position;
            float halfHeight = miniMapCamera.orthographicSize;
            float halfWidth = halfHeight * miniMapCamera.aspect;
            minX = camPos.x - halfWidth;
            maxX = camPos.x + halfWidth;
            minZ = camPos.z - halfHeight;
            maxZ = camPos.z + halfHeight;
            return;
        }

        minX = worldBounds.min.x;
        maxX = worldBounds.max.x;
        minZ = worldBounds.min.z;
        maxZ = worldBounds.max.z;
    }

    private void ApplyMapLayout(bool ridingBalloon) {
        if (ridingBalloon) {
            mapPanel.anchorMin = new Vector2(0.5f, 0.5f);
            mapPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mapPanel.pivot = new Vector2(0.5f, 0.5f);
            mapPanel.anchoredPosition = new Vector2(0f, 26f);
            mapPanel.sizeDelta = new Vector2(420f, 420f);
        } else {
            mapPanel.anchorMin = new Vector2(0.5f, 1f);
            mapPanel.anchorMax = new Vector2(0.5f, 1f);
            mapPanel.pivot = new Vector2(0.5f, 1f);
            mapPanel.anchoredPosition = new Vector2(0f, -16f);
            mapPanel.sizeDelta = new Vector2(230f, 230f);
        }

        if (cursorDot != null) cursorDot.gameObject.SetActive(ridingBalloon);
    }

    private void UpdateMapMarkers(Vector3 markerWorldPosition, Vector2 teleportCursor01, Bounds worldBounds) {
        if (mapPanel == null || playerDot == null) return;

        Vector2 player01 = WorldToMap01(markerWorldPosition, worldBounds);
        PlaceMarker(playerDot.rectTransform, player01);

        if (cursorDot != null && cursorDot.gameObject.activeSelf) {
            PlaceMarker(cursorDot.rectTransform, teleportCursor01);
        }
    }

    private Vector2 WorldToMap01(Vector3 position, Bounds worldBounds) {
        float minX;
        float maxX;
        float minZ;
        float maxZ;
        GetMapWorldExtents(worldBounds, out minX, out maxX, out minZ, out maxZ);
        return new Vector2(Mathf.InverseLerp(minX, maxX, position.x), Mathf.InverseLerp(minZ, maxZ, position.z));
    }

    private void PlaceMarker(RectTransform marker, Vector2 normalized) {
        float x = (normalized.x - 0.5f) * mapPanel.rect.width;
        float y = (normalized.y - 0.5f) * mapPanel.rect.height;
        marker.anchoredPosition = new Vector2(x, y);
    }

    private static T FindRequiredComponentInChildren<T>(Transform root, string objectName) where T : Component {
        if (root == null || string.IsNullOrEmpty(objectName)) return null;

        Transform target = FindDeepChild(root, objectName);
        if (target == null) {
            Debug.LogError(objectName + " is missing under " + root.name + ".");
            return null;
        }

        T component = target.GetComponent<T>();
        if (component == null) {
            Debug.LogError(objectName + " is missing component " + typeof(T).Name + ".");
        }
        return component;
    }

    private static Transform FindDeepChild(Transform root, string name) {
        if (root == null || string.IsNullOrEmpty(name)) return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) {
            Transform child = children[i];
            if (child != null && child.name == name) return child;
        }

        return null;
    }

    private static void ConfigurePanelRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor) {
        if (rect == null) return;

        switch (anchor) {
            case TextAnchor.UpperCenter:
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                break;
            default:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                break;
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void ConfigureDot(RawImage image, Color color, float size) {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        image.texture = Texture2D.whiteTexture;
        image.color = color;
    }
}

public sealed class GazeReticleController {
    private Image gazeCrosshairOuter;
    private Image gazeCrosshairInner;
    private Canvas gazeCrosshairCanvas;
    private GameObject gazeCrosshairRoot;
    private GameObject xrGazeReticle;

    private static Sprite gazeCrosshairDotSprite;
    private static Sprite gazeCrosshairRingSprite;
    private static Material xrGazeReticleMaterial;

    public void Bind() {
        EnsureCrosshairSprites();

        gazeCrosshairRoot = GameObject.Find("GazeCrosshair");
        if (gazeCrosshairRoot == null) {
            Debug.LogError("GazeCrosshair GameObject is missing from the scene.");
            return;
        }

        gazeCrosshairCanvas = gazeCrosshairRoot.GetComponent<Canvas>();
        if (gazeCrosshairCanvas == null) {
            Debug.LogError("GazeCrosshair is missing a Canvas component.");
            return;
        }

        gazeCrosshairOuter = FindRequiredComponentInChildren<Image>(gazeCrosshairRoot.transform, "DotOuter");
        gazeCrosshairInner = FindRequiredComponentInChildren<Image>(gazeCrosshairRoot.transform, "DotInner");
        RectTransform crosshairRect = FindRequiredComponentInChildren<RectTransform>(gazeCrosshairRoot.transform, "Crosshair");

        if (gazeCrosshairOuter != null) {
            gazeCrosshairOuter.sprite = gazeCrosshairRingSprite;
            gazeCrosshairOuter.raycastTarget = false;
            gazeCrosshairOuter.color = new Color(1f, 1f, 1f, 0.78f);
            RectTransform outerRt = gazeCrosshairOuter.rectTransform;
            outerRt.anchorMin = new Vector2(0.5f, 0.5f);
            outerRt.anchorMax = new Vector2(0.5f, 0.5f);
            outerRt.anchoredPosition = Vector2.zero;
            outerRt.sizeDelta = new Vector2(18f, 18f);
        }

        if (gazeCrosshairInner != null) {
            gazeCrosshairInner.sprite = gazeCrosshairDotSprite;
            gazeCrosshairInner.raycastTarget = false;
            gazeCrosshairInner.color = new Color(1f, 1f, 1f, 0.92f);
            RectTransform innerRt = gazeCrosshairInner.rectTransform;
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerRt.anchoredPosition = Vector2.zero;
            innerRt.sizeDelta = new Vector2(4f, 4f);
        }

        if (crosshairRect != null) {
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = new Vector2(28f, 28f);
        }
    }

    public void RefreshCamera(Camera headCamera, bool vrView) {
        if (gazeCrosshairCanvas == null || gazeCrosshairRoot == null || headCamera == null) return;

        Transform canvasTransform = gazeCrosshairCanvas.transform;
        if (!vrView) {
            gazeCrosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gazeCrosshairCanvas.worldCamera = null;
            gazeCrosshairCanvas.overrideSorting = true;
            gazeCrosshairCanvas.sortingOrder = 6000;

            if (canvasTransform.parent != null) canvasTransform.SetParent(null, true);
            canvasTransform.localScale = Vector3.one;

            gazeCrosshairRoot.SetActive(true);
            return;
        }

        const float xrUiDistance = 0.5f;
        const float xrUiBaseDistance = 1.2f;
        const float xrUiBaseScale = 0.001f;
        const float uiZTestAlways = 8f;

        gazeCrosshairCanvas.renderMode = RenderMode.WorldSpace;
        gazeCrosshairCanvas.worldCamera = headCamera;
        gazeCrosshairCanvas.overrideSorting = true;
        gazeCrosshairCanvas.sortingOrder = 6000;
        Shader.SetGlobalFloat("unity_GUIZTestMode", uiZTestAlways);

        RectTransform rect = gazeCrosshairCanvas.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(200f, 200f);

        canvasTransform.SetParent(headCamera.transform, false);
        canvasTransform.localPosition = new Vector3(0f, 0f, xrUiDistance);
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * (xrUiBaseScale * (xrUiDistance / xrUiBaseDistance));

        gazeCrosshairRoot.SetActive(true);
    }

    public void RefreshState(Transform head, bool vrView) {
        UpdateGazeCrosshairActiveState(vrView);
        UpdateXrWorldReticle(head, vrView);
    }

    private void UpdateGazeCrosshairActiveState(bool vrView) {
        if (gazeCrosshairOuter != null) gazeCrosshairOuter.gameObject.SetActive(false);
        if (gazeCrosshairInner != null) gazeCrosshairInner.gameObject.SetActive(false);

        if (gazeCrosshairRoot != null) {
            gazeCrosshairRoot.SetActive(!vrView);
        }
    }

    private void EnsureXrWorldReticle() {
        if (xrGazeReticle != null) return;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "XRGazeReticle";
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.Destroy(collider);

        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
        if (renderer != null) {
            if (xrGazeReticleMaterial == null) {
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null) {
                    Material material = new Material(shader);
                    material.color = Color.white;
                    material.renderQueue = 3000;
                    xrGazeReticleMaterial = material;
                }
            }

            if (xrGazeReticleMaterial != null) {
                renderer.sharedMaterial = xrGazeReticleMaterial;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        xrGazeReticle = sphere;
        xrGazeReticle.SetActive(false);
    }

    private void UpdateXrWorldReticle(Transform head, bool vrView) {
        EnsureXrWorldReticle();
        if (xrGazeReticle == null || head == null) return;

        xrGazeReticle.SetActive(vrView);
        if (!vrView) return;

        Transform reticleTransform = xrGazeReticle.transform;
        if (reticleTransform.parent != head) {
            reticleTransform.SetParent(head, false);
        }

        reticleTransform.localPosition = new Vector3(0f, 0f, 0.65f);
        reticleTransform.localRotation = Quaternion.identity;
        reticleTransform.localScale = Vector3.one * 0.008f;
    }

    private static T FindRequiredComponentInChildren<T>(Transform root, string objectName) where T : Component {
        if (root == null || string.IsNullOrEmpty(objectName)) return null;

        Transform target = FindDeepChild(root, objectName);
        if (target == null) {
            Debug.LogError(objectName + " is missing under " + root.name + ".");
            return null;
        }

        T component = target.GetComponent<T>();
        if (component == null) {
            Debug.LogError(objectName + " is missing component " + typeof(T).Name + ".");
        }
        return component;
    }

    private static Transform FindDeepChild(Transform root, string name) {
        if (root == null || string.IsNullOrEmpty(name)) return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) {
            Transform child = children[i];
            if (child != null && child.name == name) return child;
        }

        return null;
    }

    private static void EnsureCrosshairSprites() {
        if (gazeCrosshairDotSprite == null) {
            const int size = 32;
            const float radius = 14.5f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.name = "GazeCrosshairDot";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float r0 = radius - 1f;
            float r1 = radius + 0.5f;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a;
                    if (d <= r0) a = 1f;
                    else if (d >= r1) a = 0f;
                    else a = 1f - Mathf.InverseLerp(r0, r1, d);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, true);
            gazeCrosshairDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        if (gazeCrosshairRingSprite == null) {
            const int size = 64;
            const float outerRadius = 28f;
            const float innerRadius = 19f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.name = "GazeCrosshairRing";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float outerSoftStart = outerRadius - 1.5f;
            float outerSoftEnd = outerRadius + 0.5f;
            float innerSoftStart = innerRadius - 0.5f;
            float innerSoftEnd = innerRadius + 1.5f;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float outerAlpha;
                    if (d <= outerSoftStart) outerAlpha = 1f;
                    else if (d >= outerSoftEnd) outerAlpha = 0f;
                    else outerAlpha = 1f - Mathf.InverseLerp(outerSoftStart, outerSoftEnd, d);

                    float innerAlpha;
                    if (d <= innerSoftStart) innerAlpha = 1f;
                    else if (d >= innerSoftEnd) innerAlpha = 0f;
                    else innerAlpha = 1f - Mathf.InverseLerp(innerSoftStart, innerSoftEnd, d);

                    float ringAlpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha));
                }
            }

            tex.Apply(false, true);
            gazeCrosshairRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
