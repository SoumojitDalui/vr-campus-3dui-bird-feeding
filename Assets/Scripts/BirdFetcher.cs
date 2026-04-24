using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BirdFetcher : MonoBehaviour {
    private enum State {
        Looping = 0,
        Fetching = 1,
        Returning = 2
    }

    [SerializeField] private int fruitTypeIndex;
    private float fetchSpeed = 5f;
    private float arriveDistance = 0.3f;

    private State state = State.Looping;
    private Transform target;
    private Action onFetched;

    private Behaviour[] disabledDuringFetch;
    private Vector3 cachedPosition;
    private Quaternion cachedRotation;

    public int FruitTypeIndex => fruitTypeIndex;
    public bool IsBusy => state != State.Looping;

    public bool IsInsideZone(Vector3 zoneCenter, float zoneRadius) {
        return Vector3.Distance(transform.position, zoneCenter) <= zoneRadius;
    }

    public bool TryBeginFetch(Transform target, Action onFetchCompleted) {
        if (state != State.Looping || target == null) return false;

        this.target = target;
        onFetched = onFetchCompleted;
        cachedPosition = transform.position;
        cachedRotation = transform.rotation;

        DisableLoopingMotion();
        state = State.Fetching;
        return true;
    }

    public void CancelFetch() {
        if (state != State.Fetching) return;

        target = null;
        onFetched = null;
        state = State.Returning;
    }

    private void Update() {
        float dt = Mathf.Max(0f, Time.deltaTime);

        if (state == State.Fetching) {
            UpdateMoveTowards(target != null ? target.position : cachedPosition, dt);
            if (target == null || Vector3.Distance(transform.position, target.position) <= arriveDistance) {
                Action cb = onFetched;
                onFetched = null;
                target = null;
                state = State.Returning;
                cb?.Invoke();
            }
            return;
        }

        if (state == State.Returning) {
            UpdateMoveTowards(cachedPosition, dt);
            if (Vector3.Distance(transform.position, cachedPosition) <= arriveDistance) {
                transform.SetPositionAndRotation(cachedPosition, cachedRotation);
                EnableLoopingMotion();
                state = State.Looping;
            }
        }
    }

    private void UpdateMoveTowards(Vector3 destination, float dt) {
        Vector3 delta = destination - transform.position;
        if (delta.sqrMagnitude <= 0.00001f) return;

        float step = Mathf.Max(0.05f, fetchSpeed) * dt;
        Vector3 next = Vector3.MoveTowards(transform.position, destination, step);
        transform.position = next;

        Vector3 look = destination - next;
        if (look.sqrMagnitude > 0.0001f) {
            Quaternion q = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, q, 1f - Mathf.Exp(-10f * dt));
        }
    }

    private void DisableLoopingMotion() {
        BirdFlightPath path = GetComponent<BirdFlightPath>();
        if (path != null) {
            disabledDuringFetch = new Behaviour[] { path };
            path.enabled = false;
        } else {
            disabledDuringFetch = Array.Empty<Behaviour>();
        }
    }

    private void EnableLoopingMotion() {
        if (disabledDuringFetch == null) return;
        for (int i = 0; i < disabledDuringFetch.Length; i++) {
            if (disabledDuringFetch[i] != null) disabledDuringFetch[i].enabled = true;
        }
    }
}
