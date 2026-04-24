using UnityEngine;

[DisallowMultipleComponent]
public sealed class FeedingZone : MonoBehaviour {
    private const float DefaultRadius = 5f;

    [SerializeField] private int fruitTypeIndex;
    [SerializeField] private BirdFetcher bird;

    public int FruitTypeIndex => fruitTypeIndex;
    public float Radius => DefaultRadius;
    public BirdFetcher Bird => bird;

    public Vector3 GetCenterWorld() {
        return transform.position + transform.up * 1f;
    }
}
