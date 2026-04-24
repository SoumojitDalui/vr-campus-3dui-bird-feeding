using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

[DisallowMultipleComponent]
public sealed partial class GameManager : MonoBehaviour {
    private const float PlayerEyeHeight = 1.6f;
    private const float WalkSpeed = 4.5f;
    private const float VrTurnSpeed = 120f;
    private const float VrDeviceModeEyeHeight = PlayerEyeHeight;
    private const float FruitInteractDistance = 5f;
    private const float FruitPickupLockSeconds = 0.2f;
    private const float BalloonBoardDistance = 10f;
    private const float BalloonTeleportRetargetDistance = 1.5f;
    private const float BalloonExitRadius = 4f;
    private const float BalloonExitClearanceRadius = 0.3f;
    private const float TeleportCursorSpeed = 0.45f;
    private const float PlayerCapsuleHeight = 1.2f;
    private const float PlayerCapsuleRadius = 0.35f;
    private const float PlayerCapsuleSkin = 0.04f;
    private const float FeedingZoneInteractMargin = 0.75f;
    private const float MaxFeedingZoneInteractRadius = 3f;
    private const float MaxPlayerGroundHeightAboveTerrain = 0.45f;
    private const float MaxPlayerStepUpHeight = 0.35f;
    private const float HeldFruitScaleMultiplier = 10f;
    private const float WalkableGroundNormalMinY = 0.6f;
    private const float WorldBoundsPadding = 0.75f;
    private const float VrStickDeadzone = 0.2f;

    private readonly FruitDefinition[] fruitDefinitions = new FruitDefinition[] {
        new FruitDefinition("Apple", new Color(0.86f, 0.22f, 0.22f, 1f)),
        new FruitDefinition("Pear", new Color(0.93f, 0.85f, 0.22f, 1f)),
        new FruitDefinition("Peach", new Color(0.98f, 0.58f, 0.34f, 1f)),
        new FruitDefinition("Lemon", new Color(0.95f, 0.90f, 0.22f, 1f)),
        new FruitDefinition("Strawberry", new Color(0.90f, 0.18f, 0.22f, 1f))
    };

    private readonly List<FeedingZoneData> feedingZones = new List<FeedingZoneData>(5);
    private readonly StringBuilder checklistBuilder = new StringBuilder(512);
    private readonly int[] collectedCounts = new int[5];
    private readonly int[] requiredCounts = new int[] { 2, 2, 2, 2, 2 };
    private readonly int[] fedCounts = new int[5];
    private readonly RaycastHit[] movementHits = new RaycastHit[16];
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly Collider[] overlapHits = new Collider[24];
    private static readonly List<UnityEngine.XR.XRDisplaySubsystem> xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>(4);
    private readonly HudController hudController = new HudController();

    private Transform rigRoot;
    private Transform head;
    private Camera headCamera;
    private Transform xrTrackingSpace;
    private Transform heldFruitAnchor;
    private GameObject heldFruitVisual;

    private bool xrActive;
    private FruitPickup focusedFruit;
    private FeedingZoneData activeZone;
    private int heldFruitType = -1;
    private FeedingZoneData heldFruitZone;
    private BirdFetcher dispatchedBird;

    private BalloonController balloon;
    private bool ridingBalloon;
    private Transform rigOriginalParent;
    private readonly List<Behaviour> suppressedXrLocomotionBehaviours = new List<Behaviour>(8);
    private readonly List<Behaviour> suppressedXrControllerInteractionBehaviours = new List<Behaviour>(16);

    private FruitLibrary fruitLibrary;

    private Bounds worldBounds;
    private Vector2 teleportCursor01 = new Vector2(0.5f, 0.5f);
    private float statusMessageUntil;
    private float nextFruitPickupAllowedTime;
    private string statusMessage = string.Empty;
    private string checklistTextValue = string.Empty;
    private bool primaryInteractReleaseRequired;
    private bool minimapVisible;
    private bool checklistVisible;

    private bool prevVrRightPrimary;
    private bool prevVrRightTrigger;
    private bool prevVrLeftPrimaryInteract;
    private bool prevVrLeftTriggerInteract;
    private bool prevVrLeftPrimaryHud;
    private bool prevVrLeftSecondaryHud;
    private bool prevVrRightSecondaryRide;

    private GameObject vrControllerLeft;
    private GameObject vrControllerRight;

    private void Start() {
        CacheSceneReferences();
        BuildWorldBounds();
        SpawnVrControllerModels();
        EnsureHeldFruitAnchor();
        DiscoverSceneContent();
        EnsureSimpleBuildingBlockers();
        hudController.Bind(worldBounds);
        hudController.RefreshCamera(headCamera, head, IsVrViewActive());
        UpdateChecklistText();

        teleportCursor01 = WorldToMap01(head.position);
    }

    private void Update() {
        if (head == null || rigRoot == null) return;

        RefreshHeadCameraIfNeeded();
        RefreshXrState();
        if (xrActive) {
            SetXrRigLocomotionEnabled(false);
            SetXrRigControllerInteractionsEnabled(false);
        }

        HandleModeToggleInput();
        HandleHudToggleInput();

        if (IsRidingBalloon()) {
            UpdateTeleportCursor();
        } else {
            if (xrActive) {
                HandleVrTurn();
            }
            HandleWalking();
        }

        UpdateVrControllerModels();
        UpdateFocusedFruit();
        UpdateActiveZone();
        UpdateHeldFruitDispatchState();
        HandlePrimaryInteraction();
        TryDispatchHeldFruitToBird();
        UpdateHudState();
    }

    private void HandleModeToggleInput() {
        bool vrRideToggle = VrInputAdapter.ReadRideToggleDown(ref prevVrRightSecondaryRide);
        if (!vrRideToggle) return;
        if (rigRoot == null) return;
        if (vrRideToggle) Debug.Log("Balloon toggled (VR B).");

        if (IsRidingBalloon()) {
            if (balloon == null) return;
            ExitBalloonRide();
            minimapVisible = false;
            SetStatus("You stepped off the hot air balloon.");
            return;
        }

        BalloonController closest = GetClosestBalloon();
        if (closest == null) {
            SetStatus("No hot air balloon found in the scene.");
            return;
        }

        if (Vector3.Distance(head.position, closest.transform.position) > BalloonBoardDistance) {
            SetStatus("Move closer to the hot air balloon to board it.");
            return;
        }

        if (!closest.IsLanded) {
            SetStatus("The hot air balloon needs to be landed first.");
            return;
        }

        if (EnterBalloonRide(closest)) {
            minimapVisible = true;
            checklistVisible = false;
            teleportCursor01 = WorldToMap01(closest.transform.position);
            SetStatus("Use the minimap to choose a teleport location, then press Interact (A).");
        }
    }

    private void HandleHudToggleInput() {
        if (IsRidingBalloon()) {
            return;
        }

        bool vrMinimapToggle = VrInputAdapter.ReadMinimapToggleDown(ref prevVrLeftPrimaryHud);
        if (vrMinimapToggle) {
            bool nextVisible = !minimapVisible;
            minimapVisible = nextVisible;
            if (nextVisible) checklistVisible = false;
            Debug.Log("Minimap toggled (VR X) -> " + (minimapVisible ? "ON" : "OFF"));
        }

        bool vrChecklistToggle = VrInputAdapter.ReadChecklistToggleDown(ref prevVrLeftSecondaryHud);
        if (vrChecklistToggle) {
            bool nextVisible = !checklistVisible;
            checklistVisible = nextVisible;
            if (nextVisible) minimapVisible = false;
            Debug.Log("Checklist toggled (VR Y) -> " + (checklistVisible ? "ON" : "OFF"));
        }
    }

    private void HandleWalking() {
        if (IsRidingBalloon()) {
            return;
        }

        Vector2 moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude <= 0.0001f) {
            SnapRigToGround();
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f) forward = rigRoot.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 moveDirection = (right * moveInput.x + forward * moveInput.y);
        if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

        MoveRigWithCollision(moveDirection * WalkSpeed * Time.deltaTime);
        SnapRigToGround();
    }

    private void MoveRigWithCollision(Vector3 worldDelta) {
        float distance = worldDelta.magnitude;
        if (distance <= 0.0001f) return;
        if (rigRoot == null) return;

        Vector3 dir = worldDelta / distance;
        Vector3 start = ClampRigPositionToWorldXZ(rigRoot.position);

        Vector3 probe = ClampRigPositionToWorldXZ(start);
        float groundY = SamplePlayerGroundHeight(probe);
        Vector3 feet = new Vector3(probe.x, groundY, probe.z);

        float capsuleHeight = Mathf.Max(PlayerCapsuleRadius * 2f, PlayerCapsuleHeight);
        Vector3 p1 = feet + Vector3.up * PlayerCapsuleRadius;
        Vector3 p2 = feet + Vector3.up * (capsuleHeight - PlayerCapsuleRadius);

        int count = Physics.CapsuleCastNonAlloc(
            p1,
            p2,
            PlayerCapsuleRadius,
            dir,
            movementHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float best = float.PositiveInfinity;
        Vector3 hitNormal = Vector3.zero;
        for (int i = 0; i < count; i++) {
            RaycastHit hit = movementHits[i];
            Collider c = hit.collider;
            if (c == null) continue;
            if (c is TerrainCollider) continue;
            if (rigRoot != null && c.transform != null && c.transform.IsChildOf(rigRoot)) continue;
            if (hit.distance < best) {
                best = hit.distance;
                hitNormal = hit.normal;
            }
        }

        float allowed = float.IsPositiveInfinity(best) ? distance : Mathf.Max(0f, best - PlayerCapsuleSkin);
        Vector3 nextPosition = start;
        if (allowed > 0.0001f) {
            nextPosition += dir * allowed;
        }

        float remaining = Mathf.Max(0f, distance - allowed);
        if (!float.IsPositiveInfinity(best) && remaining > 0.0001f && hitNormal.sqrMagnitude > 0.0001f) {
            Vector3 slide = Vector3.ProjectOnPlane(dir * remaining, hitNormal);
            slide.y = 0f;
            if (slide.sqrMagnitude > 0.0001f) {
                slide = slide.normalized * Mathf.Min(remaining, PlayerCapsuleRadius * 0.75f);
                nextPosition += slide;
            }
        }

        rigRoot.position = ClampRigPositionToWorldXZ(nextPosition);
    }

    private void SnapRigToGround() {
        Vector3 position = ClampRigPositionToWorldXZ(rigRoot.position);
        Vector3 probe = ClampRigPositionToWorldXZ(head.position + Vector3.up * 0.5f);
        float groundY;
        if (!TrySamplePlayerGroundHeight(probe, out groundY)) {
            rigRoot.position = position;
            return;
        }

        if (xrActive) {
            float desiredHeadY = groundY + GetCurrentHeadGroundOffset();
            float deltaY = desiredHeadY - head.position.y;
            position.y += deltaY;
        } else {
            float desiredHeadY = groundY + PlayerEyeHeight;
            float deltaY = desiredHeadY - head.position.y;
            position.y += deltaY;
        }

        rigRoot.position = position;
    }

    private Vector3 ClampRigPositionToWorldXZ(Vector3 position) {
        if (worldBounds.size.x <= 0.001f || worldBounds.size.z <= 0.001f) {
            return position;
        }

        float minX = worldBounds.min.x + WorldBoundsPadding;
        float maxX = worldBounds.max.x - WorldBoundsPadding;
        float minZ = worldBounds.min.z + WorldBoundsPadding;
        float maxZ = worldBounds.max.z - WorldBoundsPadding;

        if (minX > maxX) {
            float midX = (worldBounds.min.x + worldBounds.max.x) * 0.5f;
            minX = midX;
            maxX = midX;
        }

        if (minZ > maxZ) {
            float midZ = (worldBounds.min.z + worldBounds.max.z) * 0.5f;
            minZ = midZ;
            maxZ = midZ;
        }

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        return position;
    }

    private void HandleVrTurn() {
        if (!xrActive || IsRidingBalloon()) return;

        Vector2 turn = ReadVrTurnInput();
        if (Mathf.Abs(turn.x) <= 0.0001f) return;

        float yawDelta = turn.x * VrTurnSpeed * Time.deltaTime;

        Vector3 pivot = head != null ? head.position : rigRoot.position;
        pivot.y = rigRoot.position.y;
        rigRoot.RotateAround(pivot, Vector3.up, yawDelta);
    }

    private void UpdateTeleportCursor() {
        Vector2 input = ReadTeleportCursorInput();
        teleportCursor01 += input * TeleportCursorSpeed * Time.deltaTime;
        teleportCursor01.x = Mathf.Clamp01(teleportCursor01.x);
        teleportCursor01.y = Mathf.Clamp01(teleportCursor01.y);
    }

    private void UpdateFocusedFruit() {
        FruitPickup nextFruit = null;
        Ray ray = new Ray(head.position, head.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, FruitInteractDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) {
            nextFruit = hit.collider != null ? hit.collider.GetComponentInParent<FruitPickup>() : null;
            if (nextFruit != null && nextFruit.IsCollected) nextFruit = null;
        }

        if (focusedFruit == nextFruit) return;

        if (focusedFruit != null) focusedFruit.SetHovered(false);
        focusedFruit = nextFruit;
        if (focusedFruit != null) focusedFruit.SetHovered(true);
    }

    private void UpdateActiveZone() {
        activeZone = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < feedingZones.Count; i++) {
            FeedingZoneData zone = feedingZones[i];
            Vector3 playerPos = head != null ? head.position : rigRoot.position;
            Vector3 zoneCenter = zone.Center;
            playerPos.y = 0f;
            zoneCenter.y = 0f;

            float effectiveRadius = Mathf.Max(0.5f, Mathf.Min(zone.Radius - FeedingZoneInteractMargin, MaxFeedingZoneInteractRadius));
            float distance = Vector3.Distance(playerPos, zoneCenter);
            if (distance > effectiveRadius) continue;
            if (distance < bestDistance) {
                bestDistance = distance;
                activeZone = zone;
            }
        }
    }

    private void HandlePrimaryInteraction() {
        if (primaryInteractReleaseRequired) {
            if (!IsPrimaryInteractHeld()) {
                primaryInteractReleaseRequired = false;
            } else {
                return;
            }
        }

        if (!TryGetPrimaryInteractPressedThisFrame(out _)) return;
        primaryInteractReleaseRequired = true;

        if (IsRidingBalloon()) {
            ConfirmTeleportSelection();
            return;
        }

        if (heldFruitType >= 0) {
            SetStatus("Wait for the bird to take the fruit, or step back into the matching feeding zone.");
            return;
        }

        if (focusedFruit != null) {
            CollectFruit(focusedFruit);
            return;
        }

        if (activeZone != null) {
            PrepareFruitForFeeding(activeZone);
        }
    }

    private void CollectFruit(FruitPickup fruit) {
        if (fruit == null || fruit.IsCollected) return;
        if (Time.time < nextFruitPickupAllowedTime) return;

        collectedCounts[fruit.FruitTypeIndex]++;
        nextFruitPickupAllowedTime = Time.time + FruitPickupLockSeconds;
        fruit.Collect();
        SetStatus("Collected " + fruit.FruitTypeName + ".");
        UpdateChecklistText();
    }

    private void PrepareFruitForFeeding(FeedingZoneData zone) {
        if (zone == null) return;
        string fruitName = fruitDefinitions[zone.FruitTypeIndex].Name;

        if (heldFruitType >= 0) {
            SetStatus("You are already holding a fruit for a bird.");
            return;
        }

        if (AvailableFruitCount(zone.FruitTypeIndex) <= 0) {
            if (fedCounts[zone.FruitTypeIndex] >= requiredCounts[zone.FruitTypeIndex]) {
                SetStatus(zone.Label + " is complete.");
            } else {
                SetStatus("You need more " + fruitName + " fruit.");
            }
            return;
        }

        if (fruitLibrary == null || !fruitLibrary.HasFruitPrefab(zone.FruitTypeIndex)) return;

        heldFruitType = zone.FruitTypeIndex;
        heldFruitZone = zone;
        dispatchedBird = null;
        SpawnHeldFruitVisual(zone.FruitTypeIndex);

        if (heldFruitVisual == null) {
            ClearHeldFruit();
            return;
        }

        SetStatus("Hold still in the bubble and let the bird take the fruit.");
        UpdateChecklistText();
    }

    private void UpdateHeldFruitDispatchState() {
        if (heldFruitType < 0) return;
        if (dispatchedBird == null) return;

        if (activeZone == heldFruitZone) return;

        dispatchedBird.CancelFetch();
        dispatchedBird = null;
        SetStatus("Step back into the matching feeding zone so the bird can take the fruit.");
    }

    private void TryDispatchHeldFruitToBird() {
        FeedingZoneData zone = heldFruitZone;
        if (heldFruitType < 0 || zone == null || activeZone != zone) return;
        if (zone.FruitTypeIndex != heldFruitType) return;
        if (zone.Bird == null || zone.Bird.IsBusy) return;
        if (!zone.Bird.IsInsideZone(zone.Center, zone.Radius)) return;

        int fruitType = heldFruitType;
        if (zone.Bird.TryBeginFetch(heldFruitAnchor, delegate {
            dispatchedBird = null;
            CompleteFeed(fruitType);
        })) {
            dispatchedBird = zone.Bird;
            SetStatus("A bird is flying in to grab the fruit.");
        }
    }

    private void CompleteFeed(int fruitTypeIndex) {
        fedCounts[fruitTypeIndex]++;
        ClearHeldFruit();
        UpdateChecklistText();

        if (AllFeedingTargetsMet()) {
            SetStatus("All birds have been fed. Assignment 2 checklist complete.", 4f);
        } else {
            SetStatus("Fed the " + fruitDefinitions[fruitTypeIndex].Name + " bird.");
        }
    }

    private void SpawnHeldFruitVisual(int fruitTypeIndex) {
        ClearHeldFruitVisual();

        if (fruitLibrary == null) return;
        heldFruitVisual = fruitLibrary.InstantiateFruitPrefab(fruitTypeIndex, heldFruitAnchor);
        if (heldFruitVisual != null) {
            heldFruitVisual.transform.localScale *= HeldFruitScaleMultiplier;
        }
    }

    private void ClearHeldFruit() {
        heldFruitType = -1;
        heldFruitZone = null;
        dispatchedBird = null;
        ClearHeldFruitVisual();
    }

    private void ClearHeldFruitVisual() {
        if (heldFruitVisual != null) {
            Destroy(heldFruitVisual);
            heldFruitVisual = null;
        }
    }

    private void ConfirmTeleportSelection() {
        teleportCursor01 = BalloonRideController.ConfirmTeleportSelection(
            balloon,
            worldBounds,
            teleportCursor01,
            BalloonTeleportRetargetDistance,
            Map01ToWorld,
            samplePosition => {
                float groundY;
                bool ok = TrySampleBalloonLandingHeight(samplePosition, out groundY);
                return (ok, groundY);
            },
            message => SetStatus(message));
    }

}
