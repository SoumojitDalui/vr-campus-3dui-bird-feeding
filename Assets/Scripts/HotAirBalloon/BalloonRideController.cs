using System;
using UnityEngine;

public static class BalloonRideController {
    public static BalloonController GetClosestBalloon(Transform head) {
        if (head == null) return null;

        BalloonController[] balloons = UnityEngine.Object.FindObjectsByType<BalloonController>(FindObjectsSortMode.None);
        BalloonController closest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < balloons.Length; i++) {
            BalloonController candidate = balloons[i];
            if (candidate == null) continue;
            float distance = Vector3.Distance(head.position, candidate.transform.position);
            if (distance < bestDistance) {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public static bool EnterBalloonRide(
        BalloonController targetBalloon,
        Transform rigRoot,
        ref Transform rigOriginalParent,
        ref BalloonController balloon,
        ref bool ridingBalloon,
        Action<Vector3, float> teleportRigTo,
        Action disableXrSuppression) {
        if (targetBalloon == null || rigRoot == null) return false;

        Transform anchor = targetBalloon.PassengerAnchor;
        if (anchor == null) return false;

        if (rigOriginalParent == null) rigOriginalParent = rigRoot.parent;

        rigRoot.SetParent(rigOriginalParent, true);
        teleportRigTo(anchor.position, anchor.eulerAngles.y);
        rigRoot.SetParent(anchor, true);

        balloon = targetBalloon;
        ridingBalloon = true;
        disableXrSuppression();
        return true;
    }

    public static bool ExitBalloonRide(
        Transform rigRoot,
        Transform head,
        Bounds worldBounds,
        BalloonController balloon,
        ref bool ridingBalloon,
        Transform rigOriginalParent,
        float playerEyeHeight,
        float playerCapsuleHeight,
        float playerCapsuleRadius,
        float balloonExitRadius,
        float balloonExitClearanceRadius,
        float worldBoundsPadding,
        float walkableGroundNormalMinY,
        RaycastHit[] groundHits,
        Collider[] overlapHits,
        Action<Vector3, float> teleportRigTo,
        Func<float> getCurrentHeadGroundOffset,
        Action snapRigToGround,
        Action disableXrSuppression) {
        if (!ridingBalloon || rigRoot == null || balloon == null) return false;

        Vector3 exitHeadPosition = GetBalloonExitHeadPosition(
            rigRoot,
            head,
            worldBounds,
            balloon,
            balloon.transform.position,
            rigRoot.right,
            playerEyeHeight,
            playerCapsuleHeight,
            playerCapsuleRadius,
            balloonExitRadius,
            balloonExitClearanceRadius,
            worldBoundsPadding,
            walkableGroundNormalMinY,
            groundHits,
            overlapHits,
            getCurrentHeadGroundOffset);

        rigRoot.SetParent(rigOriginalParent, true);
        teleportRigTo(exitHeadPosition, rigRoot.eulerAngles.y);
        ridingBalloon = false;
        snapRigToGround();
        disableXrSuppression();
        return true;
    }

    public static Vector2 ConfirmTeleportSelection(
        BalloonController balloon,
        Bounds worldBounds,
        Vector2 teleportCursor01,
        float balloonTeleportRetargetDistance,
        Func<Vector2, Vector3> map01ToWorld,
        Func<Vector3, (bool ok, float groundY)> sampleBalloonLandingHeight,
        Action<string> setStatus) {
        if (balloon == null) return teleportCursor01;

        Vector3 target = map01ToWorld(teleportCursor01);
        var landing = sampleBalloonLandingHeight(target + Vector3.up * 12f);
        if (!landing.ok) {
            setStatus("The balloon cannot land on buildings.");
            return teleportCursor01;
        }

        Vector3 nextBalloonPosition = new Vector3(target.x, landing.groundY, target.z);
        Vector3 currentBalloonPosition = balloon.transform.position;

        if (!balloon.CanOccupyPosition(nextBalloonPosition)) {
            setStatus("The balloon cannot land on buildings.");
            return teleportCursor01;
        }

        if (Vector3.Distance(new Vector3(currentBalloonPosition.x, 0f, currentBalloonPosition.z),
                             new Vector3(nextBalloonPosition.x, 0f, nextBalloonPosition.z)) < balloonTeleportRetargetDistance) {
            setStatus("Choose a different location on the minimap.");
            return teleportCursor01;
        }

        balloon.transform.position = nextBalloonPosition;
        setStatus("Teleport target selected. Press B again to hop off the balloon.");
        return WorldToMap01(balloon.transform.position, worldBounds, map01ToWorld);
    }

    public static void TeleportRigTo(Transform rigRoot, Transform head, Vector3 headWorldPosition, float yawDegrees) {
        if (rigRoot == null) return;

        if (head == null) {
            rigRoot.SetPositionAndRotation(headWorldPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            return;
        }

        Vector3 headLocal = rigRoot.InverseTransformPoint(head.position);
        rigRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        rigRoot.position = headWorldPosition - (rigRoot.rotation * headLocal);
    }

    private static Vector3 GetBalloonExitHeadPosition(
        Transform rigRoot,
        Transform head,
        Bounds worldBounds,
        BalloonController balloon,
        Vector3 balloonPosition,
        Vector3 preferredRight,
        float playerEyeHeight,
        float playerCapsuleHeight,
        float playerCapsuleRadius,
        float balloonExitRadius,
        float balloonExitClearanceRadius,
        float worldBoundsPadding,
        float walkableGroundNormalMinY,
        RaycastHit[] groundHits,
        Collider[] overlapHits,
        Func<float> getCurrentHeadGroundOffset) {
        float headOffset = getCurrentHeadGroundOffset();
        Vector3 right = preferredRight.sqrMagnitude > 0.0001f ? preferredRight.normalized : Vector3.right;
        right.y = 0f;
        if (right.sqrMagnitude <= 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
        if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.Cross(Vector3.up, right);
        forward.Normalize();

        Vector3[] directions = new Vector3[] {
            right, -right, forward, -forward,
            (right + forward).normalized, (right - forward).normalized,
            (-right + forward).normalized, (-right - forward).normalized
        };

        float[] radii = new float[] { balloonExitRadius, balloonExitRadius + 1.5f, balloonExitRadius + 3f };
        bool hasFallback = false;
        Vector3 fallbackHead = new Vector3(balloonPosition.x, balloonPosition.y + headOffset, balloonPosition.z);

        for (int r = 0; r < radii.Length; r++) {
            float radius = radii[r];
            for (int i = 0; i < directions.Length; i++) {
                Vector3 candidateXZ = balloonPosition + directions[i] * radius;
                float groundY;
                if (!PlayerLocomotionController.TrySampleGroundHeight(rigRoot, balloon, candidateXZ + Vector3.up * 12f, walkableGroundNormalMinY, groundHits, out groundY)) {
                    continue;
                }

                Vector3 candidateHead = new Vector3(candidateXZ.x, groundY + headOffset, candidateXZ.z);
                if (!hasFallback) {
                    fallbackHead = candidateHead;
                    hasFallback = true;
                }

                if (IsBalloonExitPointClear(
                        rigRoot,
                        balloon,
                        candidateHead,
                        headOffset,
                        playerEyeHeight,
                        playerCapsuleHeight,
                        playerCapsuleRadius,
                        balloonExitClearanceRadius,
                        overlapHits)) {
                    return candidateHead;
                }
            }
        }

        if (hasFallback) return fallbackHead;

        float fallbackGroundY;
        if (PlayerLocomotionController.TrySampleGroundHeight(rigRoot, balloon, balloonPosition + Vector3.up * 12f, walkableGroundNormalMinY, groundHits, out fallbackGroundY)) {
            return new Vector3(balloonPosition.x, fallbackGroundY + headOffset, balloonPosition.z);
        }

        return new Vector3(balloonPosition.x, balloonPosition.y + headOffset, balloonPosition.z);
    }

    private static bool IsBalloonExitPointClear(
        Transform rigRoot,
        BalloonController balloon,
        Vector3 candidateHeadPosition,
        float headOffset,
        float playerEyeHeight,
        float playerCapsuleHeight,
        float playerCapsuleRadius,
        float balloonExitClearanceRadius,
        Collider[] overlapHits) {
        Vector3 feet = candidateHeadPosition - Vector3.up * Mathf.Max(playerEyeHeight, headOffset);
        float capsuleHeight = Mathf.Max(playerCapsuleRadius * 2f, playerCapsuleHeight);
        Vector3 p1 = feet + Vector3.up * playerCapsuleRadius;
        Vector3 p2 = feet + Vector3.up * (capsuleHeight - playerCapsuleRadius);

        int count = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            balloonExitClearanceRadius,
            overlapHits,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++) {
            Collider c = overlapHits[i];
            if (c == null || c is TerrainCollider) continue;
            if (balloon != null && c.transform != null && c.transform.IsChildOf(balloon.transform)) continue;
            if (rigRoot != null && c.transform != null && c.transform.IsChildOf(rigRoot)) continue;
            return false;
        }

        return true;
    }

    private static Vector2 WorldToMap01(Vector3 position, Bounds worldBounds, Func<Vector2, Vector3> map01ToWorld) {
        Vector3 leftWorld = map01ToWorld(new Vector2(0f, 0.5f));
        Vector3 rightWorld = map01ToWorld(new Vector2(1f, 0.5f));
        Vector3 bottomWorld = map01ToWorld(new Vector2(0.5f, 0f));
        Vector3 topWorld = map01ToWorld(new Vector2(0.5f, 1f));

        float minX = Mathf.Min(leftWorld.x, rightWorld.x);
        float maxX = Mathf.Max(leftWorld.x, rightWorld.x);
        float minZ = Mathf.Min(bottomWorld.z, topWorld.z);
        float maxZ = Mathf.Max(bottomWorld.z, topWorld.z);

        if (Mathf.Approximately(minX, maxX)) {
            minX = worldBounds.min.x;
            maxX = worldBounds.max.x;
        }

        if (Mathf.Approximately(minZ, maxZ)) {
            minZ = worldBounds.min.z;
            maxZ = worldBounds.max.z;
        }

        return new Vector2(
            Mathf.InverseLerp(minX, maxX, position.x),
            Mathf.InverseLerp(minZ, maxZ, position.z));
    }
}
