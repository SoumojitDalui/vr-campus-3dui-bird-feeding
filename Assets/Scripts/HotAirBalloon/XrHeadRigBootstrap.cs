using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
using System.Collections.Generic;

public static class XrHeadRigBootstrap {
    private static readonly List<XRDisplaySubsystem> xrDisplays = new List<XRDisplaySubsystem>(4);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureXrHeadTracking() {
#if UNITY_ANDROID && !UNITY_EDITOR
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) return;

        if (!IsXrRunning()) return;

        Transform camTransform = cam.transform;
        Vector3 desiredWorldPos = camTransform.position;
        float desiredYaw = camTransform.eulerAngles.y;

        Transform rigRoot = camTransform.parent;
        if (rigRoot == null) {
            GameObject root = new GameObject("XRRigRoot");
            rigRoot = root.transform;
            rigRoot.SetPositionAndRotation(desiredWorldPos, Quaternion.Euler(0f, desiredYaw, 0f));
            camTransform.SetParent(rigRoot, true);
        }

        TrackedPoseDriver tpd = cam.GetComponent<TrackedPoseDriver>();
        if (tpd == null) tpd = cam.gameObject.AddComponent<TrackedPoseDriver>();
        tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
        tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        XrRigAlignOnce align = rigRoot.gameObject.GetComponent<XrRigAlignOnce>();
        if (align == null) align = rigRoot.gameObject.AddComponent<XrRigAlignOnce>();
        align.Configure(camTransform, desiredWorldPos, desiredYaw);
#endif
    }

    private static bool IsXrRunning() {
        xrDisplays.Clear();
        SubsystemManager.GetSubsystems(xrDisplays);
        for (int i = 0; i < xrDisplays.Count; i++) {
            XRDisplaySubsystem d = xrDisplays[i];
            if (d != null && d.running) return true;
        }
        return false;
    }

    private sealed class XrRigAlignOnce : MonoBehaviour {
        private Transform head;
        private Vector3 desiredWorldPos;
        private float desiredYaw;
        private int framesLeft;

        public void Configure(Transform head, Vector3 desiredWorldPos, float desiredYaw) {
            this.head = head;
            this.desiredWorldPos = desiredWorldPos;
            this.desiredYaw = desiredYaw;
            framesLeft = 5;
        }

        private void LateUpdate() {
            if (head == null) {
                Destroy(this);
                return;
            }

            transform.rotation = Quaternion.Euler(0f, desiredYaw, 0f);
            transform.position = desiredWorldPos - (transform.rotation * head.localPosition);

            framesLeft--;
            if (framesLeft <= 0) Destroy(this);
        }
    }
}
