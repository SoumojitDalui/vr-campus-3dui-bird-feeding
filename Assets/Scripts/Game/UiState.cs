using UnityEngine;
using UnityEngine.XR;

public sealed partial class GameManager {
    private float GetCurrentHeadGroundOffset() {
        return PlayerLocomotionController.GetCurrentHeadGroundOffset(
            rigRoot,
            head,
            xrActive,
            PlayerEyeHeight,
            VrDeviceModeEyeHeight);
    }

    private void UpdateHudState() {
        hudController.RefreshState(
            worldBounds,
            headCamera,
            head,
            IsVrViewActive(),
            checklistVisible,
            minimapVisible,
            IsRidingBalloon(),
            BuildControlsText(),
            BuildPromptText(),
            checklistTextValue,
            teleportCursor01,
            IsRidingBalloon() && balloon != null ? balloon.transform.position : head.position);
    }

    private bool IsVrViewActive() {
        if (xrActive) return true;
        if (XRSettings.isDeviceActive) return true;
        if (headCamera != null && headCamera.stereoEnabled) return true;
        return false;
    }

    private string BuildPromptText() {
        if (Time.time <= statusMessageUntil && !string.IsNullOrEmpty(statusMessage)) {
            return statusMessage;
        }

        if (IsRidingBalloon()) {
            return "Ride mode: use the joystick to place the cursor on the map, press A to teleport, press B (Right) to get off.";
        }

        if (focusedFruit != null) {
            return "Collect " + focusedFruit.FruitTypeName + " with A.";
        }

        if (heldFruitType >= 0) {
            return "Hold the " + fruitDefinitions[heldFruitType].Name + " in the matching feeding bubble until the bird takes it.";
        }

        if (activeZone != null) {
            if (fedCounts[activeZone.FruitTypeIndex] >= requiredCounts[activeZone.FruitTypeIndex] &&
                AvailableFruitCount(activeZone.FruitTypeIndex) <= 0) {
                return activeZone.Label + " is complete.";
            }
            return "Press A to place a " + fruitDefinitions[activeZone.FruitTypeIndex].Name + " on your hand for the bird.";
        }

        BalloonController closestBalloon = GetClosestBalloon();
        if (closestBalloon != null && Vector3.Distance(head.position, closestBalloon.transform.position) <= BalloonBoardDistance) {
            return "Press B (Right) to hop onto the hot air balloon.";
        }

        if (AllFeedingTargetsMet()) {
            return "All birds have been fed. Checklist complete.";
        }

        return "Collect fruits from the trees, then feed the matching birds inside their colored zones.";
    }

    private string BuildControlsText() {
        if (IsRidingBalloon()) {
            return "Cursor: Right Stick\nTeleport: A\nGet Off: B (Right)";
        }

        return "Move: Right Stick\nTurn: Left Stick\nLook: Headset\nInteract: A\nBalloon: B (Right)\nMinimap: X (Left)\nChecklist: Y (Left)";
    }

    private void UpdateChecklistText() {
        checklistBuilder.Length = 0;
        checklistBuilder.AppendLine("<b>Bird Feeding Checklist</b>");
        checklistBuilder.AppendLine("<color=#A9B0B7>Legend: C = collected, F = fed</color>");
        checklistBuilder.AppendLine();

        for (int i = 0; i < fruitDefinitions.Length; i++) {
            bool collectDone = collectedCounts[i] >= requiredCounts[i];
            bool feedDone = fedCounts[i] >= requiredCounts[i];
            string hex = ColorUtility.ToHtmlStringRGB(fruitDefinitions[i].FruitColor);

            checklistBuilder.Append("<color=#").Append(hex).Append(">■</color> ");
            checklistBuilder.Append("<b>").Append(fruitDefinitions[i].Name).Append("</b>");
            if (fruitDefinitions[i].Name.Length < 10) checklistBuilder.Append(' ', 10 - fruitDefinitions[i].Name.Length);

            checklistBuilder.Append("  C ");
            checklistBuilder.Append(collectedCounts[i]).Append("/").Append(requiredCounts[i]);
            if (collectDone) checklistBuilder.Append(" <color=#56E05A>✓</color>");

            checklistBuilder.Append("   F ");
            checklistBuilder.Append(Mathf.Min(fedCounts[i], requiredCounts[i])).Append("/").Append(requiredCounts[i]);
            if (feedDone) checklistBuilder.Append(" <color=#56E05A>✓</color>");

            if (collectDone && feedDone) checklistBuilder.Append("  <color=#56E05A><b>DONE</b></color>");
            checklistBuilder.AppendLine();
        }

        checklistTextValue = checklistBuilder.ToString();
    }

    private BalloonController GetClosestBalloon() {
        return BalloonRideController.GetClosestBalloon(head);
    }

    private bool IsRidingBalloon() {
        return ridingBalloon;
    }

    private bool EnterBalloonRide(BalloonController targetBalloon) {
        return BalloonRideController.EnterBalloonRide(
            targetBalloon,
            rigRoot,
            ref rigOriginalParent,
            ref balloon,
            ref ridingBalloon,
            TeleportRigTo,
            DisableXrBalloonControls);
    }

    private void ExitBalloonRide() {
        BalloonRideController.ExitBalloonRide(
            rigRoot,
            head,
            worldBounds,
            balloon,
            ref ridingBalloon,
            rigOriginalParent,
            PlayerEyeHeight,
            PlayerCapsuleHeight,
            PlayerCapsuleRadius,
            BalloonExitRadius,
            BalloonExitClearanceRadius,
            WorldBoundsPadding,
            WalkableGroundNormalMinY,
            groundHits,
            overlapHits,
            TeleportRigTo,
            GetCurrentHeadGroundOffset,
            SnapRigToGround,
            DisableXrBalloonControls);
    }

    private void TeleportRigTo(Vector3 headWorldPosition, float yawDegrees) {
        BalloonRideController.TeleportRigTo(rigRoot, head, headWorldPosition, yawDegrees);
    }

    private bool AllFeedingTargetsMet() {
        for (int i = 0; i < requiredCounts.Length; i++) {
            if (fedCounts[i] < requiredCounts[i]) return false;
        }
        return true;
    }

    private int AvailableFruitCount(int fruitTypeIndex) {
        int reserved = heldFruitType == fruitTypeIndex ? 1 : 0;
        return collectedCounts[fruitTypeIndex] - fedCounts[fruitTypeIndex] - reserved;
    }

    private void SetStatus(string message, float duration = 2.5f) {
        statusMessage = message;
        statusMessageUntil = Time.time + duration;
    }

    private Vector2 ReadMoveInput() {
        return VrInputAdapter.ReadMoveInput(xrActive, IsRidingBalloon(), VrStickDeadzone);
    }

    private Vector2 ReadTeleportCursorInput() {
        return VrInputAdapter.ReadTeleportCursorInput(xrActive, VrStickDeadzone);
    }

    private Vector2 ReadVrTurnInput() {
        return VrInputAdapter.ReadVrTurnInput(xrActive, VrStickDeadzone);
    }

    private bool TryGetPrimaryInteractPressedThisFrame(out string source) {
        return VrInputAdapter.TryGetPrimaryInteractPressedThisFrame(
            xrActive,
            ref prevVrRightPrimary,
            ref prevVrRightTrigger,
            ref prevVrLeftPrimaryInteract,
            ref prevVrLeftTriggerInteract,
            out source);
    }

    private bool IsPrimaryInteractHeld() {
        return VrInputAdapter.IsPrimaryInteractHeld(xrActive);
    }
}
