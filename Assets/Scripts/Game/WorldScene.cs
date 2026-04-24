using UnityEngine;

public sealed partial class GameManager {
    private void BuildWorldBounds() {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++) {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null) continue;

            Bounds terrainBounds = new Bounds(
                terrain.transform.position + terrain.terrainData.size * 0.5f,
                terrain.terrainData.size);

            if (!hasBounds) {
                bounds = terrainBounds;
                hasBounds = true;
            } else {
                bounds.Encapsulate(terrainBounds);
            }
        }

        if (!hasBounds) {
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.scene.IsValid()) continue;
                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!hasBounds) {
            Vector3 anchor = balloon != null ? balloon.transform.position : head.position;
            bounds = new Bounds(anchor, new Vector3(120f, 40f, 120f));
        }

        bounds.Expand(new Vector3(0f, 10f, 0f));
        worldBounds = bounds;
    }

    private void EnsureHeldFruitAnchor() {
        if (heldFruitAnchor != null) {
            MountHeldFruitAnchor();
            return;
        }
        if (head == null) return;

        Transform existing = head.Find("HeldFruitAnchor");
        if (existing != null) {
            heldFruitAnchor = existing;
            MountHeldFruitAnchor();
            return;
        }

        GameObject global = GameObject.Find("HeldFruitAnchor");
        if (global != null) {
            heldFruitAnchor = global.transform;
            MountHeldFruitAnchor();
            return;
        }

        GameObject anchor = new GameObject("HeldFruitAnchor");
        heldFruitAnchor = anchor.transform;
        MountHeldFruitAnchor();
    }

    private void MountHeldFruitAnchor() {
        if (heldFruitAnchor == null) return;
        if (xrActive && vrControllerRight != null) {
            Transform mount = VrRigVisuals.ResolveVrHeldFruitMount(vrControllerRight);
            heldFruitAnchor.SetParent(mount != null ? mount : vrControllerRight.transform, false);
            heldFruitAnchor.localPosition = mount != null && mount != vrControllerRight.transform
                ? new Vector3(0.01f, -0.005f, 0.045f)
                : new Vector3(0.015f, -0.015f, 0.08f);
        } else {
            heldFruitAnchor.SetParent(head, false);
            heldFruitAnchor.localPosition = new Vector3(0.25f, -0.22f, 0.55f);
        }
        heldFruitAnchor.localRotation = Quaternion.identity;
    }

    private void DiscoverSceneContent() {
        fruitLibrary = SceneContentController.DiscoverSceneContent(feedingZones, fruitDefinitions);
    }

    private void EnsureSimpleBuildingBlockers() {
        SceneContentController.EnsureSimpleBuildingBlockers();
    }

    private float SampleGroundHeight(Vector3 worldPosition) {
        float groundY;
        if (TrySampleGroundHeight(worldPosition, out groundY)) return groundY;
        return worldPosition.y;
    }

    private float SamplePlayerGroundHeight(Vector3 worldPosition) {
        float groundY;
        if (TrySamplePlayerGroundHeight(worldPosition, out groundY)) return groundY;
        return SampleGroundHeight(worldPosition);
    }

    private bool TrySamplePlayerGroundHeight(Vector3 worldPosition, out float groundY) {
        return PlayerLocomotionController.TrySamplePlayerGroundHeight(
            rigRoot,
            balloon,
            worldPosition,
            WalkableGroundNormalMinY,
            MaxPlayerGroundHeightAboveTerrain,
            MaxPlayerStepUpHeight,
            groundHits,
            out groundY);
    }

    private bool TrySampleTerrainHeightOnly(Vector3 worldPosition, out float groundY) {
        return PlayerLocomotionController.TrySampleTerrainHeightOnly(worldPosition, groundHits, out groundY);
    }

    private bool TrySampleBalloonLandingHeight(Vector3 worldPosition, out float groundY) {
        return TrySampleTerrainHeightOnly(worldPosition, out groundY);
    }

    private bool TrySampleGroundHeight(Vector3 worldPosition, out float groundY) {
        return PlayerLocomotionController.TrySampleGroundHeight(
            rigRoot,
            balloon,
            worldPosition,
            WalkableGroundNormalMinY,
            groundHits,
            out groundY);
    }

    private Vector2 WorldToMap01(Vector3 position) {
        float minX;
        float maxX;
        float minZ;
        float maxZ;
        GetMapWorldExtents(out minX, out maxX, out minZ, out maxZ);
        float x = Mathf.InverseLerp(minX, maxX, position.x);
        float y = Mathf.InverseLerp(minZ, maxZ, position.z);
        return new Vector2(x, y);
    }

    private Vector3 Map01ToWorld(Vector2 map01) {
        float minX;
        float maxX;
        float minZ;
        float maxZ;
        GetMapWorldExtents(out minX, out maxX, out minZ, out maxZ);
        float x = Mathf.Lerp(minX, maxX, map01.x);
        float z = Mathf.Lerp(minZ, maxZ, map01.y);
        return new Vector3(x, worldBounds.center.y, z);
    }

    private void GetMapWorldExtents(out float minX, out float maxX, out float minZ, out float maxZ) {
        hudController.GetMapWorldExtents(worldBounds, out minX, out maxX, out minZ, out maxZ);
    }
}
