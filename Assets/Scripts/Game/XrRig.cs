using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

public sealed partial class GameManager {
    private void RefreshHeadCameraIfNeeded() {
        Camera best = FindBestHeadCamera();
        if (best == null) return;
        if (best == headCamera) return;

        headCamera = best;
        head = headCamera.transform;
        rigRoot = ResolveRigRootFromHead(head);
        TryBindXrDeviceSimulatorCameraTransform();
        ResolveXrTrackingSpace();
        hudController.RefreshCamera(headCamera, head, IsVrViewActive());
    }

    private void RefreshXrState() {
        bool nowXrActive = DetermineXrActive();
        if (nowXrActive == xrActive) return;

        xrActive = nowXrActive;
        Debug.Log("XR mode changed -> " + (xrActive ? "ON" : "OFF"));
        ResolveXrTrackingSpace();

        if (xrActive) {
            SpawnVrControllerModels();
            heldFruitAnchor = null;
            EnsureHeldFruitAnchor();
        } else {
            if (vrControllerLeft != null) Destroy(vrControllerLeft);
            if (vrControllerRight != null) Destroy(vrControllerRight);
            vrControllerLeft = null;
            vrControllerRight = null;

            heldFruitAnchor = null;
            EnsureHeldFruitAnchor();
        }

        hudController.RefreshCamera(headCamera, head, IsVrViewActive());
        SetXrRigLocomotionEnabled(false);
        SetXrRigControllerInteractionsEnabled(false);
    }

    private bool DetermineXrActive() {
        UnityEngine.XR.InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (headDevice.isValid) return true;
        UnityEngine.XR.InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftDevice.isValid) return true;
        UnityEngine.XR.InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.isValid) return true;

        if (XRSettings.isDeviceActive) return true;
        if (!string.IsNullOrEmpty(XRSettings.loadedDeviceName) && XRSettings.loadedDeviceName != "None") return true;

        xrDisplays.Clear();
        SubsystemManager.GetSubsystems(xrDisplays);
        for (int i = 0; i < xrDisplays.Count; i++) {
            UnityEngine.XR.XRDisplaySubsystem display = xrDisplays[i];
            if (display != null && display.running) return true;
        }

        XRGeneralSettings general = XRGeneralSettings.Instance;
        if (general != null && general.Manager != null && general.Manager.activeLoader != null) return true;

        return false;
    }

    private void ResolveXrTrackingSpace() {
        xrTrackingSpace = rigRoot;
        if (head == null) return;

        XROrigin origin = head.GetComponentInParent<XROrigin>();
        if (origin != null) {
            xrTrackingSpace = origin.transform;
        }
    }

    private void CacheSceneReferences() {
        headCamera = FindBestHeadCamera();
        if (headCamera == null) {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            headCamera = cameraGo.AddComponent<Camera>();
            headCamera.transform.position = new Vector3(-50f, 1.6f, 0f);
        }

        head = headCamera.transform;
        xrActive = DetermineXrActive();

        bool headIsInXrOrigin = head != null && head.GetComponentInParent<XROrigin>() != null;

        if (!headIsInXrOrigin && head.parent == null) {
            GameObject root = new GameObject("PlayerRigRoot");
            root.transform.SetPositionAndRotation(head.position, Quaternion.Euler(0f, head.eulerAngles.y, 0f));
            head.SetParent(root.transform, true);
        }

        rigRoot = ResolveRigRootFromHead(head);
        TryBindXrDeviceSimulatorCameraTransform();

        ResolveXrTrackingSpace();

        balloon = FindFirstObjectByType<BalloonController>();
        rigOriginalParent = rigRoot != null ? rigRoot.parent : null;

        UnityEngine.XR.InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        UnityEngine.XR.InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        UnityEngine.XR.InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        Debug.Log(
            "Rig startup: headCam=" + (headCamera != null ? headCamera.name : "NULL") +
            " rigRoot=" + (rigRoot != null ? rigRoot.name : "NULL") +
            " trackingSpace=" + (xrTrackingSpace != null ? xrTrackingSpace.name : "NULL") +
            " xrActive=" + xrActive +
            " xrHead=" + (headDevice.isValid ? "OK" : "MISSING") +
            " xrLeft=" + (leftDevice.isValid ? "OK" : "MISSING") +
            " xrRight=" + (rightDevice.isValid ? "OK" : "MISSING"));
    }

    private static Transform ResolveRigRootFromHead(Transform headTransform) {
        if (headTransform == null) return null;

        XROrigin origin = headTransform.GetComponentInParent<XROrigin>();
        if (origin != null) return origin.transform;

        return headTransform.parent;
    }

    private void TryBindXrDeviceSimulatorCameraTransform() {
        if (headCamera == null) return;

        UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator sim =
            FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        if (sim == null) return;

        if (sim.cameraTransform == null) {
            sim.cameraTransform = headCamera.transform;
        }
    }

    private static Camera FindBestHeadCamera() {
        XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
        if (origin != null) {
            Camera originCam = origin.GetComponentInChildren<Camera>(true);
            if (originCam != null && originCam.enabled && originCam.gameObject.activeInHierarchy) {
                return originCam;
            }
        }

        bool wantXr = IsVrActiveLikeAssignment1();

        Camera bestStereoBoth = null;
        Camera bestStereoAny = null;
        Camera bestMain = null;
        Camera bestAny = null;

        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++) {
            Camera c = cams[i];
            if (c == null || !c.enabled) continue;

            if (wantXr && c.stereoEnabled) {
                if (c.stereoTargetEye == StereoTargetEyeMask.Both) {
                    bestStereoBoth = c;
                    break;
                }
                if (bestStereoAny == null) bestStereoAny = c;
            }

            if (bestMain == null && c.CompareTag("MainCamera")) bestMain = c;
            if (bestAny == null) bestAny = c;
        }

        if (bestStereoBoth != null) return bestStereoBoth;
        if (bestStereoAny != null) return bestStereoAny;
        if (bestMain != null) return bestMain;
        return bestAny;
    }

    private static bool IsVrActiveLikeAssignment1() {
        UnityEngine.XR.InputDevice head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
        if (head.isValid) return true;

        xrDisplays.Clear();
        SubsystemManager.GetSubsystems(xrDisplays);
        for (int i = 0; i < xrDisplays.Count; i++) {
            UnityEngine.XR.XRDisplaySubsystem d = xrDisplays[i];
            if (d != null && d.running) return true;
        }

        return XRSettings.isDeviceActive;
    }

    private void DisableXrBalloonControls() {
        SetXrRigLocomotionEnabled(false);
        SetXrRigControllerInteractionsEnabled(false);
    }

    private void SetXrRigLocomotionEnabled(bool enabled) {
        XROrigin origin = rigRoot != null ? rigRoot.GetComponent<XROrigin>() : null;
        if (origin == null && head != null) {
            origin = head.GetComponentInParent<XROrigin>();
        }
        if (origin == null) return;

        if (enabled) {
            for (int i = 0; i < suppressedXrLocomotionBehaviours.Count; i++) {
                Behaviour behaviour = suppressedXrLocomotionBehaviours[i];
                if (behaviour != null) behaviour.enabled = true;
            }
            suppressedXrLocomotionBehaviours.Clear();
            return;
        }

        suppressedXrLocomotionBehaviours.Clear();
        Behaviour[] behaviours = origin.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++) {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled) continue;
            if (!ShouldSuppressXrLocomotionBehaviour(behaviour)) continue;

            behaviour.enabled = false;
            suppressedXrLocomotionBehaviours.Add(behaviour);
        }
    }

    private void SetXrRigControllerInteractionsEnabled(bool enabled) {
        XROrigin origin = rigRoot != null ? rigRoot.GetComponent<XROrigin>() : null;
        if (origin == null && head != null) {
            origin = head.GetComponentInParent<XROrigin>();
        }
        if (origin == null) return;

        if (enabled) {
            for (int i = 0; i < suppressedXrControllerInteractionBehaviours.Count; i++) {
                Behaviour behaviour = suppressedXrControllerInteractionBehaviours[i];
                if (behaviour != null) behaviour.enabled = true;
            }
            suppressedXrControllerInteractionBehaviours.Clear();
            return;
        }

        suppressedXrControllerInteractionBehaviours.Clear();
        Behaviour[] behaviours = origin.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++) {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled) continue;
            if (!ShouldSuppressXrControllerInteractionBehaviour(behaviour)) continue;

            behaviour.enabled = false;
            suppressedXrControllerInteractionBehaviours.Add(behaviour);
        }
    }

    private static bool ShouldSuppressXrControllerInteractionBehaviour(Behaviour behaviour) {
        string typeName = behaviour.GetType().Name;
        switch (typeName) {
            case "XRInteractorReticleVisual":
            case "XRInteractorLineVisual":
                return true;
            default:
                return false;
        }
    }

    private static bool ShouldSuppressXrLocomotionBehaviour(Behaviour behaviour) {
        string typeName = behaviour.GetType().Name;
        string fullName = behaviour.GetType().FullName ?? string.Empty;
        switch (typeName) {
            case "LocomotionMediator":
            case "LocomotionSystem":
            case "DynamicMoveProvider":
            case "ContinuousMoveProvider":
            case "DeviceBasedContinuousMoveProvider":
            case "ActionBasedContinuousMoveProvider":
            case "ContinuousTurnProvider":
            case "DeviceBasedContinuousTurnProvider":
            case "ActionBasedContinuousTurnProvider":
            case "SnapTurnProvider":
            case "DeviceBasedSnapTurnProvider":
            case "ActionBasedSnapTurnProvider":
            case "CharacterControllerDriver":
            case "GrabMoveProvider":
            case "TwoHandedGrabMoveProvider":
            case "ClimbProvider":
            case "JumpProvider":
            case "GravityProvider":
            case "TeleportationProvider":
                return true;
            default:
                return fullName.Contains(".Locomotion.");
        }
    }

    private void SpawnVrControllerModels() {
        VrRigVisuals.SpawnVrControllerModels(
            xrActive,
            xrTrackingSpace,
            rigRoot,
            headCamera,
            ref vrControllerLeft,
            ref vrControllerRight);
    }

    private void UpdateVrControllerModels() {
        VrRigVisuals.UpdateVrControllerModels(
            xrActive,
            rigRoot,
            head,
            heldFruitAnchor,
            vrControllerLeft,
            vrControllerRight);
    }
}
