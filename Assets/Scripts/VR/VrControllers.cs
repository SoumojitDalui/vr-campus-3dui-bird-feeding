using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;

public static class VrInputAdapter {
    public static Vector2 ReadMoveInput(bool xrActive, bool ridingBalloon, float deadzone) {
        if (ridingBalloon || !xrActive) return Vector2.zero;
        Vector2 input = Vector2.zero;
        Vector2 questMove = ReadQuestMoveStick(deadzone);
        if (questMove.sqrMagnitude > 0f || HasQuestTouchControllers()) {
            input += questMove;
        } else {
            input += ReadXrNode2DAxis(XRNode.RightHand, deadzone);
        }
        return Vector2.ClampMagnitude(input, 1f);
    }

    public static Vector2 ReadTeleportCursorInput(bool xrActive, float deadzone) {
        if (!xrActive) return Vector2.zero;
        Vector2 input = Vector2.zero;
        Vector2 questMove = ReadQuestMoveStick(deadzone);
        if (questMove.sqrMagnitude > 0f || HasQuestTouchControllers()) {
            input += questMove;
        } else {
            input += ReadXrNode2DAxis(XRNode.RightHand, deadzone);
        }
        return Vector2.ClampMagnitude(input, 1f);
    }

    public static Vector2 ReadVrTurnInput(bool xrActive, float deadzone) {
        if (!xrActive) return Vector2.zero;
        Vector2 questTurn = ReadQuestTurnStick(deadzone);
        if (questTurn.sqrMagnitude > 0f || HasQuestTouchControllers()) {
            return questTurn;
        }
        return ReadXrNode2DAxis(XRNode.LeftHand, deadzone);
    }

    public static bool TryGetPrimaryInteractPressedThisFrame(
        bool xrActive,
        ref bool prevVrRightPrimary,
        ref bool prevVrRightTrigger,
        ref bool prevVrLeftPrimaryInteract,
        ref bool prevVrLeftTriggerInteract,
        out string source) {
        source = string.Empty;
        if (!xrActive) return false;
        if (ReadQuestInteractDown()) {
            source = "Quest A/Right Trigger";
            return true;
        }
        if (WasInputSystemXrButtonPressedThisFrame(true, "primaryButton")) {
            source = "XR Right Primary";
            return true;
        }
        if (WasInputSystemXrButtonPressedThisFrame(true, "triggerPressed")) {
            source = "XR Right Trigger";
            return true;
        }
        if (WasInputSystemXrButtonPressedThisFrame(false, "primaryButton")) {
            source = "XR Left Primary";
            return true;
        }
        if (WasInputSystemXrButtonPressedThisFrame(false, "triggerPressed")) {
            source = "XR Left Trigger";
            return true;
        }
        if (ReadXrNodeButtonDown(XRNode.RightHand, UnityEngine.XR.CommonUsages.primaryButton, ref prevVrRightPrimary)) {
            source = "VR A";
            return true;
        }
        if (ReadXrNodeTriggerDown(XRNode.RightHand, ref prevVrRightTrigger)) {
            source = "VR Trigger";
            return true;
        }
        if (ReadXrNodeButtonDown(XRNode.LeftHand, UnityEngine.XR.CommonUsages.primaryButton, ref prevVrLeftPrimaryInteract)) {
            source = "VR Left Primary";
            return true;
        }
        if (ReadXrNodeTriggerDown(XRNode.LeftHand, ref prevVrLeftTriggerInteract)) {
            source = "VR Left Trigger";
            return true;
        }
        return false;
    }

    public static bool IsPrimaryInteractHeld(bool xrActive) {
        if (!xrActive) return false;
        if (ReadQuestInteractHeld()) return true;
        if (IsInputSystemXrButtonHeld(true, "primaryButton")) return true;
        if (IsInputSystemXrButtonHeld(true, "triggerPressed")) return true;
        if (IsInputSystemXrButtonHeld(false, "primaryButton")) return true;
        if (IsInputSystemXrButtonHeld(false, "triggerPressed")) return true;
        if (ReadXrNodeButtonHeld(XRNode.RightHand, UnityEngine.XR.CommonUsages.primaryButton)) return true;
        if (ReadXrNodeTriggerHeld(XRNode.RightHand)) return true;
        if (ReadXrNodeButtonHeld(XRNode.LeftHand, UnityEngine.XR.CommonUsages.primaryButton)) return true;
        if (ReadXrNodeTriggerHeld(XRNode.LeftHand)) return true;
        return false;
    }

    public static bool ReadRideToggleDown(ref bool prevVrRightSecondaryRide) {
        return ReadQuestButtonDown(OVRInput.RawButton.B) ||
               ReadXrNodeButtonDown(XRNode.RightHand, UnityEngine.XR.CommonUsages.secondaryButton, ref prevVrRightSecondaryRide);
    }

    public static bool ReadMinimapToggleDown(ref bool prevVrLeftPrimaryHud) {
        return ReadQuestButtonDown(OVRInput.RawButton.X) ||
               ReadXrNodeButtonDown(XRNode.LeftHand, UnityEngine.XR.CommonUsages.primaryButton, ref prevVrLeftPrimaryHud);
    }

    public static bool ReadChecklistToggleDown(ref bool prevVrLeftSecondaryHud) {
        return ReadQuestButtonDown(OVRInput.RawButton.Y) ||
               ReadXrNodeButtonDown(XRNode.LeftHand, UnityEngine.XR.CommonUsages.secondaryButton, ref prevVrLeftSecondaryHud);
    }

    public static bool HasQuestTouchControllers() {
        OVRInput.Controller connected = OVRInput.GetConnectedControllers();
        return (connected & OVRInput.Controller.RTouch) == OVRInput.Controller.RTouch ||
               (connected & OVRInput.Controller.LTouch) == OVRInput.Controller.LTouch ||
               connected == OVRInput.Controller.Touch;
    }

    private static bool WasInputSystemXrButtonPressedThisFrame(bool rightHand, string controlName) {
        UnityEngine.InputSystem.XR.XRController controller = rightHand
            ? UnityEngine.InputSystem.XR.XRController.rightHand
            : UnityEngine.InputSystem.XR.XRController.leftHand;
        if (controller == null) return false;
        ButtonControl button = controller.TryGetChildControl<ButtonControl>(controlName);
        return button != null && button.wasPressedThisFrame;
    }

    private static bool IsInputSystemXrButtonHeld(bool rightHand, string controlName) {
        UnityEngine.InputSystem.XR.XRController controller = rightHand
            ? UnityEngine.InputSystem.XR.XRController.rightHand
            : UnityEngine.InputSystem.XR.XRController.leftHand;
        if (controller == null) return false;
        ButtonControl button = controller.TryGetChildControl<ButtonControl>(controlName);
        return button != null && button.isPressed;
    }

    private static bool ReadQuestInteractDown() {
        return ReadQuestButtonDown(OVRInput.RawButton.A) || ReadQuestButtonDown(OVRInput.RawButton.RIndexTrigger);
    }

    private static bool ReadQuestInteractHeld() {
        return ReadQuestButtonHeld(OVRInput.RawButton.A) || ReadQuestButtonHeld(OVRInput.RawButton.RIndexTrigger);
    }

    private static bool ReadQuestButtonDown(OVRInput.RawButton button) {
        return OVRInput.GetDown(button);
    }

    private static bool ReadQuestButtonHeld(OVRInput.RawButton button) {
        return OVRInput.Get(button);
    }

    private static Vector2 ReadQuestMoveStick(float deadzone) {
        return ReadQuestThumbstick(OVRInput.RawAxis2D.RThumbstick, deadzone);
    }

    private static Vector2 ReadQuestTurnStick(float deadzone) {
        return ReadQuestThumbstick(OVRInput.RawAxis2D.LThumbstick, deadzone);
    }

    private static Vector2 ReadQuestThumbstick(OVRInput.RawAxis2D axis, float deadzone) {
        Vector2 value = OVRInput.Get(axis);
        value = Vector2.ClampMagnitude(value, 1f);
        if (value.sqrMagnitude < deadzone * deadzone) return Vector2.zero;
        return value;
    }

    private static bool ReadXrNodeButtonDown(XRNode node, InputFeatureUsage<bool> usage, ref bool prevPressed) {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) {
            prevPressed = false;
            return false;
        }
        bool pressed;
        if (!device.TryGetFeatureValue(usage, out pressed)) pressed = false;
        bool down = pressed && !prevPressed;
        prevPressed = pressed;
        return down;
    }

    private static bool ReadXrNodeTriggerDown(XRNode node, ref bool prevPressed) {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) {
            prevPressed = false;
            return false;
        }
        bool pressed;
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out pressed)) {
            bool down = pressed && !prevPressed;
            prevPressed = pressed;
            return down;
        }
        float trigger;
        bool hasAxis = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out trigger);
        pressed = hasAxis && trigger >= 0.75f;
        bool axisDown = pressed && !prevPressed;
        prevPressed = pressed;
        return axisDown;
    }

    private static bool ReadXrNodeButtonHeld(XRNode node, InputFeatureUsage<bool> usage) {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        bool pressed;
        return device.TryGetFeatureValue(usage, out pressed) && pressed;
    }

    private static bool ReadXrNodeTriggerHeld(XRNode node) {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        bool pressed;
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out pressed)) {
            return pressed;
        }
        float trigger;
        return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out trigger) && trigger >= 0.75f;
    }

    private static Vector2 ReadXrNode2DAxis(XRNode node, float deadzone) {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return Vector2.zero;
        Vector2 axis;
        if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out axis) &&
            !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondary2DAxis, out axis)) {
            axis = Vector2.zero;
        }
        axis = Vector2.ClampMagnitude(axis, 1f);
        if (axis.sqrMagnitude < deadzone * deadzone) return Vector2.zero;
        return axis;
    }
}

public static class VrRigVisuals {
    public static void SpawnVrControllerModels(
        bool xrActive,
        Transform xrTrackingSpace,
        Transform rigRoot,
        Camera headCamera,
        ref GameObject vrControllerLeft,
        ref GameObject vrControllerRight) {
        if (!xrActive) return;
        Transform fallbackParent = xrTrackingSpace != null ? xrTrackingSpace : rigRoot;
        Transform searchRoot = rigRoot != null ? rigRoot : fallbackParent;
        if (fallbackParent == null) return;
        Transform leftParent = ResolveTrackedControllerParent(rigRoot, null, XRNode.LeftHand);
        Transform rightParent = ResolveTrackedControllerParent(rigRoot, null, XRNode.RightHand);
        if (headCamera != null && headCamera.nearClipPlane > 0.05f) {
            headCamera.nearClipPlane = 0.05f;
        }
        Transform existingLeft = FindDeepChild(searchRoot, "VrHandLeft");
        if (existingLeft != null) vrControllerLeft = existingLeft.gameObject;
        Transform existingRight = FindDeepChild(searchRoot, "VrHandRight");
        if (existingRight != null) vrControllerRight = existingRight.gameObject;
        Material controllerMat = new Material(Shader.Find("Unlit/Color"));
        controllerMat.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        if (vrControllerLeft == null) {
            GameObject wrapper = new GameObject("VrHandLeft");
            wrapper.transform.SetParent(leftParent != null ? leftParent : fallbackParent, false);
            GameObject controllerModel = BuildControllerPlaceholder("ControllerMeshLeft", controllerMat, true);
            controllerModel.transform.SetParent(wrapper.transform, false);
            vrControllerLeft = wrapper;
        }
        if (vrControllerRight == null) {
            GameObject wrapper = new GameObject("VrHandRight");
            wrapper.transform.SetParent(rightParent != null ? rightParent : fallbackParent, false);
            GameObject controllerModel = BuildControllerPlaceholder("ControllerMeshRight", controllerMat, false);
            controllerModel.transform.SetParent(wrapper.transform, false);
            vrControllerRight = wrapper;
        }
        if (vrControllerLeft != null) {
            vrControllerLeft.transform.SetParent(leftParent != null ? leftParent : fallbackParent, false);
            vrControllerLeft.transform.localPosition = Vector3.zero;
            vrControllerLeft.transform.localRotation = Quaternion.identity;
        }
        if (vrControllerRight != null) {
            vrControllerRight.transform.SetParent(rightParent != null ? rightParent : fallbackParent, false);
            vrControllerRight.transform.localPosition = Vector3.zero;
            vrControllerRight.transform.localRotation = Quaternion.identity;
        }
        ConfigureVrControllerVisuals(rigRoot, null, null, vrControllerLeft, vrControllerRight);
    }

    public static void UpdateVrControllerModels(
        bool xrActive,
        Transform rigRoot,
        Transform head,
        Transform heldFruitAnchor,
        GameObject vrControllerLeft,
        GameObject vrControllerRight) {
        if (!xrActive) return;
        ReattachVrControllerWrapper(rigRoot, head, XRNode.LeftHand, vrControllerLeft);
        ReattachVrControllerWrapper(rigRoot, head, XRNode.RightHand, vrControllerRight);
        UpdateVrControllerModel(XRNode.LeftHand, vrControllerLeft);
        UpdateVrControllerModel(XRNode.RightHand, vrControllerRight);
        ConfigureVrControllerVisuals(rigRoot, head, heldFruitAnchor, vrControllerLeft, vrControllerRight);
    }

    public static Transform ResolveVrHeldFruitMount(GameObject vrControllerRight) {
        if (vrControllerRight == null) return null;
        Transform root = vrControllerRight.transform;
        Transform skel = FindDeepChild(root, "right_touch_controller_model_skel");
        if (skel != null) return skel;
        Transform world = FindDeepChild(root, "rctrl:right_touch_controller_world");
        if (world != null) return world;
        return root;
    }

    public static Transform FindDeepChild(Transform root, string name) {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) {
            Transform child = children[i];
            if (child != null && child.name == name) return child;
        }
        return null;
    }

    private static void ConfigureVrControllerVisuals(
        Transform rigRoot,
        Transform head,
        Transform heldFruitAnchor,
        GameObject vrControllerLeft,
        GameObject vrControllerRight) {
        ConfigureVrControllerVisual(rigRoot, head, heldFruitAnchor, vrControllerLeft, true);
        ConfigureVrControllerVisual(rigRoot, head, heldFruitAnchor, vrControllerRight, false);
        HideTrackedControllerMeshes(rigRoot, head, heldFruitAnchor, XRNode.LeftHand, vrControllerLeft);
        HideTrackedControllerMeshes(rigRoot, head, heldFruitAnchor, XRNode.RightHand, vrControllerRight);
    }

    private static void ConfigureVrControllerVisual(Transform rigRoot, Transform head, Transform heldFruitAnchor, GameObject controllerRoot, bool leftHand) {
        if (controllerRoot == null) return;
        Transform trackedParent = ResolveTrackedControllerParent(rigRoot, head, leftHand ? XRNode.LeftHand : XRNode.RightHand);
        Transform promotedHandRoot = PromoteDirectVrHandVisual(controllerRoot.transform, trackedParent, leftHand);
        Transform[] preferredRoots = new Transform[] {
            promotedHandRoot,
            trackedParent != null ? FindDeepChild(trackedParent, leftHand ? "OVRLeftHandVisual" : "OVRRightHandVisual") : null,
            trackedParent != null ? FindDeepChild(trackedParent, leftHand ? "OculusHand_L" : "OculusHand_R") : null,
            trackedParent != null ? FindDeepChild(trackedParent, leftHand ? "l_handMeshNode" : "r_handMeshNode") : null,
            FindDeepChild(controllerRoot.transform, leftHand ? "OVRLeftHandVisual" : "OVRRightHandVisual"),
            FindDeepChild(controllerRoot.transform, leftHand ? "OculusHand_L" : "OculusHand_R"),
            FindDeepChild(controllerRoot.transform, leftHand ? "l_handMeshNode" : "r_handMeshNode")
        };
        Renderer[] renderers = controllerRoot.GetComponentsInChildren<Renderer>(true);
        bool foundPreferredHandRenderer = false;
        for (int j = 0; j < preferredRoots.Length; j++) {
            Transform preferredRoot = preferredRoots[j];
            if (preferredRoot == null) continue;
            Renderer[] preferredRenderers = preferredRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < preferredRenderers.Length; i++) {
                Renderer renderer = preferredRenderers[i];
                if (renderer == null) continue;
                renderer.enabled = true;
                foundPreferredHandRenderer = true;
            }
        }
        if (!foundPreferredHandRenderer) {
            GameObject fallbackHand = EnsureFallbackVrHandModel(controllerRoot.transform, leftHand);
            Renderer[] fallbackRenderers = controllerRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < fallbackRenderers.Length; i++) {
                Renderer renderer = fallbackRenderers[i];
                if (renderer == null) continue;
                bool keepVisible = fallbackHand != null &&
                    (renderer.transform == fallbackHand.transform || renderer.transform.IsChildOf(fallbackHand.transform));
                if (!keepVisible && !ShouldKeepVrAttachmentVisible(heldFruitAnchor, renderer.transform)) {
                    renderer.enabled = false;
                }
            }
            return;
        }
        for (int i = 0; i < renderers.Length; i++) {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            bool keepVisible = false;
            for (int j = 0; j < preferredRoots.Length; j++) {
                Transform preferredRoot = preferredRoots[j];
                if (preferredRoot == null) continue;
                if (renderer.transform == preferredRoot || renderer.transform.IsChildOf(preferredRoot)) {
                    keepVisible = true;
                    break;
                }
            }
            if (!keepVisible && !ShouldKeepVrAttachmentVisible(heldFruitAnchor, renderer.transform)) {
                renderer.enabled = false;
            }
        }
    }

    private static Transform PromoteDirectVrHandVisual(Transform controllerRoot, Transform trackedParent, bool leftHand) {
        if (controllerRoot == null) return null;
        string handMeshName = leftHand ? "OculusHand_L" : "OculusHand_R";
        Transform handMeshRoot = FindDeepChild(controllerRoot, handMeshName);
        if (handMeshRoot == null) return null;
        Transform desiredParent = trackedParent != null ? trackedParent : controllerRoot;
        if (handMeshRoot.parent != desiredParent) {
            Vector3 localScale = handMeshRoot.localScale;
            handMeshRoot.SetParent(desiredParent, false);
            handMeshRoot.localScale = localScale;
        }
        return handMeshRoot;
    }

    private static GameObject EnsureFallbackVrHandModel(Transform wrapper, bool isLeft) {
        if (wrapper == null) return null;
        Transform existing = wrapper.Find("FallbackVrHand");
        if (existing != null) return existing.gameObject;
        Material handMat = new Material(Shader.Find("Unlit/Color"));
        handMat.color = new Color(0.87f, 0.72f, 0.58f, 1f);
        GameObject hand = BuildHandModel("FallbackVrHand", handMat, isLeft);
        hand.transform.SetParent(wrapper, false);
        hand.transform.localScale = Vector3.one * 0.9f;
        hand.transform.localPosition = new Vector3(isLeft ? -0.015f : 0.015f, -0.03f, 0.08f);
        hand.transform.localRotation = Quaternion.Euler(12f, isLeft ? 90f : -90f, isLeft ? 78f : -78f);
        return hand;
    }

    private static void UpdateVrControllerModel(XRNode node, GameObject modelRoot) {
        if (modelRoot == null) return;
        Transform trackedParent = ResolveTrackedControllerParentStatic(modelRoot.transform.root, node);
        if (trackedParent != null && modelRoot.transform.parent == trackedParent) {
            if (!modelRoot.activeSelf) modelRoot.SetActive(true);
            Transform anchored = modelRoot.transform;
            anchored.localPosition = Vector3.zero;
            anchored.localRotation = Quaternion.identity;
            return;
        }
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) {
            if (modelRoot.activeSelf) modelRoot.SetActive(false);
            return;
        }
        Vector3 localPos;
        Quaternion localRot;
        bool okPose = TryGetDevicePose(device, out localPos, out localRot);
        if (!okPose) {
            localPos = UnityEngine.XR.InputTracking.GetLocalPosition(node);
            localRot = UnityEngine.XR.InputTracking.GetLocalRotation(node);
            okPose = localRot != Quaternion.identity || localPos.sqrMagnitude > 0.000001f;
        }
        if (!okPose) {
            if (modelRoot.activeSelf) modelRoot.SetActive(false);
            return;
        }
        if (!modelRoot.activeSelf) modelRoot.SetActive(true);
        Transform t = modelRoot.transform;
        t.localPosition = localPos;
        t.localRotation = localRot;
    }

    private static void ReattachVrControllerWrapper(Transform rigRoot, Transform head, XRNode node, GameObject wrapper) {
        if (wrapper == null) return;
        Transform trackedParent = ResolveTrackedControllerParent(rigRoot, head, node);
        if (trackedParent == null) return;
        if (wrapper.transform.parent == trackedParent) return;
        wrapper.transform.SetParent(trackedParent, false);
        wrapper.transform.localPosition = Vector3.zero;
        wrapper.transform.localRotation = Quaternion.identity;
    }

    private static Transform ResolveTrackedControllerParent(Transform rigRoot, Transform head, XRNode node) {
        Transform searchRoot = rigRoot != null ? rigRoot : (head != null ? head.root : null);
        return ResolveTrackedControllerParentStatic(searchRoot, node);
    }

    private static Transform ResolveTrackedControllerParentStatic(Transform searchRoot, XRNode node) {
        if (searchRoot == null) return null;
        string expectedName = node == XRNode.LeftHand ? "Left Controller" : "Right Controller";
        Transform[] candidates = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < candidates.Length; i++) {
            Transform candidate = candidates[i];
            if (candidate != null && candidate.name == expectedName) return candidate;
        }
        return null;
    }

    private static void HideTrackedControllerMeshes(Transform rigRoot, Transform head, Transform heldFruitAnchor, XRNode node, GameObject handWrapper) {
        Transform trackedParent = ResolveTrackedControllerParent(rigRoot, head, node);
        if (trackedParent == null) return;
        Renderer[] renderers = trackedParent.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (handWrapper != null && renderer.transform.IsChildOf(handWrapper.transform)) continue;
            if (IsTrackedVrHandVisual(renderer.transform, node)) continue;
            if (ShouldKeepVrAttachmentVisible(heldFruitAnchor, renderer.transform)) continue;
            if (renderer is LineRenderer) continue;
            renderer.enabled = false;
        }
    }

    private static bool IsTrackedVrHandVisual(Transform candidate, XRNode node) {
        if (candidate == null) return false;
        string[] rootNames = node == XRNode.LeftHand
            ? new[] { "OVRLeftHandVisual", "OculusHand_L", "l_handMeshNode" }
            : new[] { "OVRRightHandVisual", "OculusHand_R", "r_handMeshNode" };
        for (int i = 0; i < rootNames.Length; i++) {
            string rootName = rootNames[i];
            if (candidate.name == rootName) return true;
            Transform current = candidate.parent;
            while (current != null) {
                if (current.name == rootName) return true;
                current = current.parent;
            }
        }
        return false;
    }

    private static bool ShouldKeepVrAttachmentVisible(Transform heldFruitAnchor, Transform candidate) {
        if (candidate == null) return false;
        if (heldFruitAnchor != null && (candidate == heldFruitAnchor || candidate.IsChildOf(heldFruitAnchor))) {
            return true;
        }
        string objectName = candidate.name;
        return !string.IsNullOrEmpty(objectName) &&
               (objectName.StartsWith("HeldFruit_") || objectName.StartsWith("Held"));
    }

    private static bool TryGetDevicePose(UnityEngine.XR.InputDevice device, out Vector3 pos, out Quaternion rot) {
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos) &&
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot)) {
            return true;
        }
        pos = Vector3.zero;
        rot = Quaternion.identity;
        return false;
    }

    private static void DestroyCollider(GameObject target) {
        if (target == null) return;
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.Destroy(collider);
    }

    private static GameObject BuildHandModel(string name, Material mat, bool isLeft) {
        GameObject hand = new GameObject(name);
        float mirror = isLeft ? 1f : -1f;
        GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        palm.name = "Palm";
        palm.transform.SetParent(hand.transform, false);
        palm.transform.localPosition = Vector3.zero;
        palm.transform.localScale = new Vector3(0.09f, 0.025f, 0.12f);
        palm.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(palm);
        float fingerWidth = 0.019f;
        float fingerHeight = 0.022f;
        float fingerLength = 0.08f;
        float startX = -0.03f;
        float spacing = 0.02f;
        for (int i = 0; i < 4; i++) {
            GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finger.name = "Finger" + i;
            finger.transform.SetParent(hand.transform, false);
            finger.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0.06f + fingerLength * 0.5f);
            finger.transform.localScale = new Vector3(fingerWidth, fingerHeight, fingerLength);
            finger.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            finger.GetComponent<Renderer>().sharedMaterial = mat;
            DestroyCollider(finger);
        }
        GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thumb.name = "Thumb";
        thumb.transform.SetParent(hand.transform, false);
        thumb.transform.localPosition = new Vector3(0.05f * mirror, 0.005f, 0.02f);
        thumb.transform.localScale = new Vector3(fingerHeight, fingerHeight, 0.055f);
        thumb.transform.localRotation = Quaternion.Euler(-5f, -30f * mirror, 0f);
        thumb.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(thumb);
        return hand;
    }

    private static GameObject BuildControllerPlaceholder(string name, Material mat, bool isLeft) {
        GameObject root = new GameObject(name);
        float mirror = isLeft ? -1f : 1f;
        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(root.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.03f, -0.005f);
        grip.transform.localScale = new Vector3(0.035f, 0.11f, 0.04f);
        grip.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(grip);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        body.transform.localScale = new Vector3(0.05f, 0.035f, 0.09f);
        body.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(body);
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "Trigger";
        trigger.transform.SetParent(root.transform, false);
        trigger.transform.localPosition = new Vector3(0f, -0.01f, 0.065f);
        trigger.transform.localScale = new Vector3(0.022f, 0.025f, 0.03f);
        trigger.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(trigger);
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Ring";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0.03f * mirror, 0.03f, 0.03f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 28f * mirror);
        ring.transform.localScale = new Vector3(0.07f, 0.006f, 0.07f);
        ring.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyCollider(ring);
        return root;
    }
}
