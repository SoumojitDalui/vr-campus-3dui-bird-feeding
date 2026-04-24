using UnityEngine;

public static class PlayerLocomotionController {
    public static void HandleWalking(Transform rigRoot, Transform head, bool xrActive, bool ridingBalloon, Bounds worldBounds, BalloonController balloon, Vector2 moveInput, float walkSpeed, float deltaTime, float playerEyeHeight, float playerCapsuleHeight, float playerCapsuleRadius, float playerCapsuleSkin, float worldBoundsPadding, float walkableGroundNormalMinY, float maxPlayerGroundHeightAboveTerrain, float maxPlayerStepUpHeight, RaycastHit[] movementHits, RaycastHit[] groundHits) {
        if (rigRoot == null || head == null || ridingBalloon) return;
        if (moveInput.sqrMagnitude <= 0.0001f) {
            SnapRigToGround(rigRoot, head, xrActive, worldBounds, balloon, playerEyeHeight, playerCapsuleHeight, playerCapsuleRadius, worldBoundsPadding, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, groundHits);
            return;
        }
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f) forward = rigRoot.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 moveDirection = right * moveInput.x + forward * moveInput.y;
        if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();
        MoveRigWithCollision(rigRoot, worldBounds, balloon, moveDirection * walkSpeed * deltaTime, playerCapsuleHeight, playerCapsuleRadius, playerCapsuleSkin, worldBoundsPadding, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, movementHits, groundHits);
        SnapRigToGround(rigRoot, head, xrActive, worldBounds, balloon, playerEyeHeight, playerCapsuleHeight, playerCapsuleRadius, worldBoundsPadding, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, groundHits);
    }

    public static void HandleVrTurn(Transform rigRoot, Transform head, bool xrActive, bool ridingBalloon, Vector2 turnInput, float turnSpeed, float deltaTime) {
        if (!xrActive || ridingBalloon || rigRoot == null) return;
        if (Mathf.Abs(turnInput.x) <= 0.0001f) return;
        float yawDelta = turnInput.x * turnSpeed * deltaTime;
        Vector3 pivot = head != null ? head.position : rigRoot.position;
        pivot.y = rigRoot.position.y;
        rigRoot.RotateAround(pivot, Vector3.up, yawDelta);
    }

    public static Vector2 UpdateTeleportCursor(Vector2 current, Vector2 input, float cursorSpeed, float deltaTime) {
        current += input * cursorSpeed * deltaTime;
        current.x = Mathf.Clamp01(current.x);
        current.y = Mathf.Clamp01(current.y);
        return current;
    }

    public static float GetCurrentHeadGroundOffset(Transform rigRoot, Transform head, bool xrActive, float desktopEyeHeight, float xrFallbackEyeHeight) {
        if (head == null || rigRoot == null) return desktopEyeHeight;
        if (!xrActive) return desktopEyeHeight;
        float trackedEyeHeight = Mathf.Max(0f, head.position.y - rigRoot.position.y);
        if (trackedEyeHeight <= 0.5f) return xrFallbackEyeHeight;
        return Mathf.Clamp(trackedEyeHeight, 1.0f, 1.9f);
    }

    public static bool TrySamplePlayerGroundHeight(Transform rigRoot, BalloonController balloon, Vector3 worldPosition, float walkableGroundNormalMinY, float maxPlayerGroundHeightAboveTerrain, float maxPlayerStepUpHeight, RaycastHit[] groundHits, out float groundY) {
        return TrySampleGroundCore(rigRoot, balloon, worldPosition, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, groundHits, out groundY, true);
    }

    public static bool TrySampleTerrainHeightOnly(Vector3 worldPosition, RaycastHit[] groundHits, out float groundY) {
        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++) {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 local = worldPosition - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
            groundY = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
            return true;
        }
        Vector3 origin = worldPosition + Vector3.up * 200f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestTerrain = float.PositiveInfinity;
        float bestTerrainY = worldPosition.y;
        for (int i = 0; i < count; i++) {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || !(hit.collider is TerrainCollider)) continue;
            if (hit.distance < bestTerrain) {
                bestTerrain = hit.distance;
                bestTerrainY = hit.point.y;
            }
        }
        if (!float.IsPositiveInfinity(bestTerrain)) {
            groundY = bestTerrainY;
            return true;
        }
        groundY = worldPosition.y;
        return false;
    }

    public static bool TrySampleGroundHeight(Transform rigRoot, BalloonController balloon, Vector3 worldPosition, float walkableGroundNormalMinY, RaycastHit[] groundHits, out float groundY) {
        return TrySampleGroundCore(rigRoot, balloon, worldPosition, walkableGroundNormalMinY, float.PositiveInfinity, float.PositiveInfinity, groundHits, out groundY, false);
    }

    private static bool TrySampleGroundCore(Transform rigRoot, BalloonController balloon, Vector3 worldPosition, float walkableGroundNormalMinY, float maxPlayerGroundHeightAboveTerrain, float maxPlayerStepUpHeight, RaycastHit[] groundHits, out float groundY, bool playerMode) {
        float terrainY;
        bool hasTerrain = TrySampleTerrainHeightOnly(worldPosition, groundHits, out terrainY);
        float currentBaseY = rigRoot != null ? rigRoot.position.y : worldPosition.y;
        Vector3 origin = worldPosition + Vector3.up * 200f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestTerrain = float.PositiveInfinity;
        float bestTerrainY = worldPosition.y;
        float bestWalkable = float.PositiveInfinity;
        float bestWalkableY = worldPosition.y;
        for (int i = 0; i < count; i++) {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null) continue;
            if (rigRoot != null && hit.collider.transform != null && hit.collider.transform.IsChildOf(rigRoot)) continue;
            if (balloon != null && hit.collider.transform != null && hit.collider.transform.IsChildOf(balloon.transform)) continue;
            if (hit.collider is TerrainCollider) {
                if (hit.distance < bestTerrain) {
                    bestTerrain = hit.distance;
                    bestTerrainY = hit.point.y;
                }
                continue;
            }
            if (hit.normal.y < walkableGroundNormalMinY) continue;
            if (playerMode) {
                if (hit.point.y > currentBaseY + maxPlayerStepUpHeight) continue;
                if (hasTerrain && hit.point.y > terrainY + maxPlayerGroundHeightAboveTerrain) continue;
            }
            if (hit.distance < bestWalkable) {
                bestWalkable = hit.distance;
                bestWalkableY = hit.point.y;
            }
        }
        if (playerMode) {
            if (!float.IsPositiveInfinity(bestWalkable)) {
                groundY = bestWalkableY;
                return true;
            }
            if (hasTerrain) {
                groundY = terrainY;
                return true;
            }
            groundY = worldPosition.y;
            return false;
        }
        if (!float.IsPositiveInfinity(bestTerrain)) {
            groundY = bestTerrainY;
            return true;
        }
        if (!float.IsPositiveInfinity(bestWalkable)) {
            groundY = bestWalkableY;
            return true;
        }
        return TrySampleTerrainHeightOnly(worldPosition, groundHits, out groundY);
    }

    private static void MoveRigWithCollision(Transform rigRoot, Bounds worldBounds, BalloonController balloon, Vector3 worldDelta, float playerCapsuleHeight, float playerCapsuleRadius, float playerCapsuleSkin, float worldBoundsPadding, float walkableGroundNormalMinY, float maxPlayerGroundHeightAboveTerrain, float maxPlayerStepUpHeight, RaycastHit[] movementHits, RaycastHit[] groundHits) {
        float distance = worldDelta.magnitude;
        if (distance <= 0.0001f || rigRoot == null) return;
        Vector3 dir = worldDelta / distance;
        Vector3 start = ClampRigPositionToWorldXZ(worldBounds, rigRoot.position, worldBoundsPadding);
        Vector3 probe = ClampRigPositionToWorldXZ(worldBounds, start, worldBoundsPadding);
        float groundY;
        if (!TrySamplePlayerGroundHeight(rigRoot, balloon, probe, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, groundHits, out groundY)) groundY = probe.y;
        Vector3 feet = new Vector3(probe.x, groundY, probe.z);
        float capsuleHeight = Mathf.Max(playerCapsuleRadius * 2f, playerCapsuleHeight);
        Vector3 p1 = feet + Vector3.up * playerCapsuleRadius;
        Vector3 p2 = feet + Vector3.up * (capsuleHeight - playerCapsuleRadius);
        int count = Physics.CapsuleCastNonAlloc(p1, p2, playerCapsuleRadius, dir, movementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        Vector3 hitNormal = Vector3.zero;
        for (int i = 0; i < count; i++) {
            RaycastHit hit = movementHits[i];
            Collider c = hit.collider;
            if (c == null || c is TerrainCollider) continue;
            if (rigRoot != null && c.transform != null && c.transform.IsChildOf(rigRoot)) continue;
            if (hit.distance < best) {
                best = hit.distance;
                hitNormal = hit.normal;
            }
        }
        float allowed = float.IsPositiveInfinity(best) ? distance : Mathf.Max(0f, best - playerCapsuleSkin);
        Vector3 nextPosition = start;
        if (allowed > 0.0001f) nextPosition += dir * allowed;
        float remaining = Mathf.Max(0f, distance - allowed);
        if (!float.IsPositiveInfinity(best) && remaining > 0.0001f && hitNormal.sqrMagnitude > 0.0001f) {
            Vector3 slide = Vector3.ProjectOnPlane(dir * remaining, hitNormal);
            slide.y = 0f;
            if (slide.sqrMagnitude > 0.0001f) {
                slide = slide.normalized * Mathf.Min(remaining, playerCapsuleRadius * 0.75f);
                nextPosition += slide;
            }
        }
        rigRoot.position = ClampRigPositionToWorldXZ(worldBounds, nextPosition, worldBoundsPadding);
    }

    private static void SnapRigToGround(Transform rigRoot, Transform head, bool xrActive, Bounds worldBounds, BalloonController balloon, float playerEyeHeight, float playerCapsuleHeight, float playerCapsuleRadius, float worldBoundsPadding, float walkableGroundNormalMinY, float maxPlayerGroundHeightAboveTerrain, float maxPlayerStepUpHeight, RaycastHit[] groundHits) {
        if (rigRoot == null || head == null) return;
        Vector3 position = ClampRigPositionToWorldXZ(worldBounds, rigRoot.position, worldBoundsPadding);
        Vector3 probe = ClampRigPositionToWorldXZ(worldBounds, head.position + Vector3.up * 0.5f, worldBoundsPadding);
        float groundY;
        if (!TrySamplePlayerGroundHeight(rigRoot, balloon, probe, walkableGroundNormalMinY, maxPlayerGroundHeightAboveTerrain, maxPlayerStepUpHeight, groundHits, out groundY)) {
            rigRoot.position = position;
            return;
        }
        float desiredHeadY = groundY + GetCurrentHeadGroundOffset(rigRoot, head, xrActive, playerEyeHeight, playerEyeHeight);
        float deltaY = desiredHeadY - head.position.y;
        position.y += deltaY;
        rigRoot.position = position;
    }

    private static Vector3 ClampRigPositionToWorldXZ(Bounds worldBounds, Vector3 position, float worldBoundsPadding) {
        if (worldBounds.size.x <= 0.001f || worldBounds.size.z <= 0.001f) return position;
        float minX = worldBounds.min.x + worldBoundsPadding;
        float maxX = worldBounds.max.x - worldBoundsPadding;
        float minZ = worldBounds.min.z + worldBoundsPadding;
        float maxZ = worldBounds.max.z - worldBoundsPadding;
        if (minX > maxX) {
            float midX = (worldBounds.min.x + worldBounds.max.x) * 0.5f;
            minX = maxX = midX;
        }
        if (minZ > maxZ) {
            float midZ = (worldBounds.min.z + worldBounds.max.z) * 0.5f;
            minZ = maxZ = midZ;
        }
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        return position;
    }
}
