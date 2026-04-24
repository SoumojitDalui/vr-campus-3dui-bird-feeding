using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
#if UNITY_ANDROID
using Unity.XR.Oculus;
#endif

public static class QuestRuntimeTuning {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() {
#if UNITY_ANDROID && !UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 72;
        XRSettings.eyeTextureResolutionScale = Mathf.Clamp(XRSettings.eyeTextureResolutionScale, 0.8f, 1.0f);

        QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 4);
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.6f);
        QualitySettings.maximumLODLevel = Mathf.Max(QualitySettings.maximumLODLevel, 1);
        QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

        Utils.useDynamicFoveatedRendering = true;
        if (Utils.foveatedRenderingLevel < 2) Utils.foveatedRenderingLevel = 2;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySceneTuning() {
#if UNITY_ANDROID && !UNITY_EDITOR
        QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 25f);

        ApplyBuildingLightingLayer();

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++) {
            Terrain t = terrains[i];
            if (t == null) continue;
            t.drawInstanced = true;

            t.treeDistance = Mathf.Min(t.treeDistance, 80f);
            t.treeBillboardDistance = 0.1f;
            t.treeCrossFadeLength = 0f;
            t.treeMaximumFullLODCount = 0;
            t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, 45f);
            t.detailObjectDensity = Mathf.Min(t.detailObjectDensity, 0.6f);
        }

        BalloonController balloon = Object.FindAnyObjectByType<BalloonController>();
        if (balloon != null) {
            ConfigureBalloonLights(balloon);
        }

        FixTerrainDetailFlowerTint();
        OptimizeLegacyTreeObjects();
        OptimizeTreeRenderers();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void ConfigureBalloonLights(BalloonController balloon) {
        int groundLayer = LayerMask.NameToLayer("GroundLit");
        int defaultMask = 1 << 0;
        int groundMask = groundLayer >= 0 ? 1 << groundLayer : 0;
        int groundOnlyMask = groundMask != 0 ? groundMask : ~0;

        Light[] lights = balloon.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++) {
            Light l = lights[i];
            if (l == null) continue;

            l.shadows = LightShadows.None;
            l.bounceIntensity = 0f;

            if (l.name == "Spot Light") {
                l.type = LightType.Point;
                l.renderMode = LightRenderMode.Auto;
                l.cullingMask = defaultMask;
                l.color = new Color(1f, 0.62f, 0.25f, 1f);
                l.intensity = Mathf.Clamp(l.intensity, 0f, 0.45f);
                l.range = Mathf.Clamp(l.range, 2f, 4f);

            } else if (l.type == LightType.Spot || l.name.StartsWith("Spot Light")) {
                l.type = LightType.Spot;
                l.cullingMask = groundOnlyMask;
                l.renderMode = LightRenderMode.ForcePixel;
                l.color = new Color(1f, 0.95f, 0.85f, 1f);
                l.intensity = Mathf.Clamp(l.intensity, 1.8f, 2.6f);
                l.range = Mathf.Clamp(Mathf.Max(l.range, 45f), 10f, 80f);
                l.spotAngle = Mathf.Clamp(l.spotAngle, 18f, 45f);
            }
        }
    }

    private static void OptimizeLegacyTreeObjects() {
        Tree[] trees = Object.FindObjectsByType<Tree>(FindObjectsSortMode.None);
        for (int i = 0; i < trees.Length; i++) {
            Tree tree = trees[i];
            if (tree == null) continue;

            MeshRenderer mr = tree.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            Material[] shared = mr.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;

            Material[] simplified = new Material[shared.Length];
            for (int mIndex = 0; mIndex < shared.Length; mIndex++) {
                Material src = shared[mIndex];
                if (src == null) {
                    simplified[mIndex] = null;
                    continue;
                }

                Texture mainTex = null;
                if (src.HasProperty("_MainTex")) mainTex = src.GetTexture("_MainTex");
                if (mainTex == null && src.HasProperty("_BaseMap")) mainTex = src.GetTexture("_BaseMap");

                bool alphaTested = src.IsKeywordEnabled("_ALPHATEST_ON") || src.renderQueue == (int)RenderQueue.AlphaTest;

                Shader shader = alphaTested
                    ? Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse")
                    : Shader.Find("Legacy Shaders/Diffuse");

                if (shader == null) {
                    simplified[mIndex] = src;
                    continue;
                }

                Material dst = new Material(shader);
                if (mainTex != null && dst.HasProperty("_MainTex")) dst.SetTexture("_MainTex", mainTex);
                if (dst.HasProperty("_Color")) dst.SetColor("_Color", Color.white);
                if (alphaTested && dst.HasProperty("_Cutoff")) dst.SetFloat("_Cutoff", 0.6f);
                simplified[mIndex] = dst;
            }

            mr.sharedMaterials = simplified;
        }
    }

    private static void OptimizeTreeRenderers() {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++) {
            Renderer r = renderers[i];
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;

            Material m = r.sharedMaterial;
            if (m == null) continue;

            string shaderName = m.shader != null ? m.shader.name : string.Empty;
            if (string.IsNullOrEmpty(shaderName)) continue;

            bool looksLikeTreeShader =
                shaderName.Contains("SpeedTree") ||
                shaderName.Contains("Tree Creator") ||
                shaderName.Contains("Nature/Tree") ||
                shaderName.Contains("Nature/SpeedTree");

            bool alphaTested = m.IsKeywordEnabled("_ALPHATEST_ON") || m.renderQueue == (int)RenderQueue.AlphaTest;

            if (!looksLikeTreeShader && !alphaTested) continue;

            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private static void FixTerrainDetailFlowerTint() {
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++) {
            Terrain t = terrains[i];
            if (t == null) continue;
            TerrainData td = t.terrainData;
            if (td == null) continue;

            DetailPrototype[] details = td.detailPrototypes;
            if (details == null || details.Length == 0) continue;

            bool changed = false;
            for (int d = 0; d < details.Length; d++) {
                DetailPrototype p = details[d];

                string texName = p.prototypeTexture != null ? p.prototypeTexture.name : string.Empty;
                string meshName = p.prototype != null ? p.prototype.name : string.Empty;
                string name = (texName + " " + meshName).ToLowerInvariant();

                bool looksLikeFlower = name.Contains("flower") || name.Contains("grassflower");
                if (!looksLikeFlower) continue;

                if (p.healthyColor != Color.white || p.dryColor != Color.white) {
                    p.healthyColor = Color.white;
                    p.dryColor = Color.white;
                    details[d] = p;
                    changed = true;
                }
            }

            if (changed) {
                td.detailPrototypes = details;
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer) {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static void ApplyBuildingLightingLayer() {
        int groundLayer = LayerMask.NameToLayer("GroundLit");
        if (groundLayer < 0) return;

        GameObject building = GameObject.Find("Building");
        if (building == null) return;
        SetLayerRecursively(building.transform, groundLayer);
    }
#endif
}
