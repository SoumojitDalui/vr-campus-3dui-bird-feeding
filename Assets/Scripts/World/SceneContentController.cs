using System.Collections.Generic;
using UnityEngine;

public static class SceneContentController {
    public static FruitLibrary DiscoverSceneContent(List<FeedingZoneData> feedingZones, FruitDefinition[] fruitDefinitions) {
        feedingZones.Clear();
        FruitLibrary fruitLibrary = UnityEngine.Object.FindFirstObjectByType<FruitLibrary>();
        FruitPickup[] fruits = UnityEngine.Object.FindObjectsByType<FruitPickup>(FindObjectsSortMode.None);
        for (int i = 0; i < fruits.Length; i++) {
            FruitPickup fruit = fruits[i];
            if (fruit == null) continue;
            fruit.EnsureCollider();
        }
        BirdFetcher[] birds = UnityEngine.Object.FindObjectsByType<BirdFetcher>(FindObjectsSortMode.None);
        FeedingZone[] zones = UnityEngine.Object.FindObjectsByType<FeedingZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++) {
            FeedingZone zoneComponent = zones[i];
            if (zoneComponent == null) continue;
            int typeIndex = Mathf.Clamp(zoneComponent.FruitTypeIndex, 0, fruitDefinitions.Length - 1);
            FeedingZoneData zone = new FeedingZoneData();
            zone.FruitTypeIndex = typeIndex;
            zone.Label = "Feeding Zone";
            zone.Center = zoneComponent.GetCenterWorld();
            zone.Radius = Mathf.Max(0.1f, zoneComponent.Radius);
            zone.Bird = zoneComponent.Bird != null ? zoneComponent.Bird : FindClosestBirdForZone(typeIndex, zone.Center, birds);
            feedingZones.Add(zone);
        }
        ApplyBirdPathCentersFromZones(feedingZones, birds);
        return fruitLibrary;
    }

    public static void EnsureSimpleBuildingBlockers() {
        Transform[] sceneTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++) {
            Transform candidate = sceneTransforms[i];
            if (candidate == null || candidate.name != "Building") continue;
            Transform wallsRoot = candidate.Find("Walls");
            Renderer[] renderers = wallsRoot != null ? wallsRoot.GetComponentsInChildren<Renderer>(true) : candidate.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) {
                Renderer renderer = renderers[r];
                if (renderer != null) bounds.Encapsulate(renderer.bounds);
            }
            BoxCollider rootCollider = candidate.GetComponent<BoxCollider>();
            if (rootCollider == null) rootCollider = candidate.gameObject.AddComponent<BoxCollider>();
            Vector3 scale = candidate.lossyScale;
            rootCollider.center = candidate.InverseTransformPoint(bounds.center);
            rootCollider.size = new Vector3(DivideByScale(bounds.size.x, scale.x), DivideByScale(bounds.size.y, scale.y), DivideByScale(bounds.size.z, scale.z));
            rootCollider.isTrigger = false;
            Collider[] colliders = candidate.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < colliders.Length; c++) {
                Collider collider = colliders[c];
                if (collider == null || collider == rootCollider) continue;
                collider.enabled = false;
            }
        }
    }

    private static void ApplyBirdPathCentersFromZones(List<FeedingZoneData> feedingZones, BirdFetcher[] birds) {
        if (birds == null || birds.Length == 0 || feedingZones.Count == 0) return;
        for (int i = 0; i < birds.Length; i++) {
            BirdFetcher bird = birds[i];
            if (bird == null) continue;
            BirdFlightPath path = bird.GetComponent<BirdFlightPath>();
            if (path == null) continue;
            FeedingZoneData best = null;
            float bestDistance = float.PositiveInfinity;
            for (int j = 0; j < feedingZones.Count; j++) {
                FeedingZoneData zone = feedingZones[j];
                if (zone == null || zone.FruitTypeIndex != bird.FruitTypeIndex) continue;
                Vector3 bpos = bird.transform.position;
                float dx = bpos.x - zone.Center.x;
                float dz = bpos.z - zone.Center.z;
                float d = dx * dx + dz * dz;
                if (d < bestDistance) {
                    bestDistance = d;
                    best = zone;
                }
            }
            if (best != null) path.SetCenterXZ(best.Center);
        }
    }

    private static BirdFetcher FindClosestBirdForZone(int fruitTypeIndex, Vector3 zoneCenter, BirdFetcher[] birds) {
        if (birds == null || birds.Length == 0) return null;
        BirdFetcher closest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < birds.Length; i++) {
            BirdFetcher bird = birds[i];
            if (bird == null || bird.FruitTypeIndex != fruitTypeIndex) continue;
            float d = Vector3.Distance(bird.transform.position, zoneCenter);
            if (d < bestDistance) {
                bestDistance = d;
                closest = bird;
            }
        }
        return closest;
    }

    private static float DivideByScale(float value, float scale) {
        float absScale = Mathf.Abs(scale);
        return absScale > 0.0001f ? value / absScale : value;
    }
}
