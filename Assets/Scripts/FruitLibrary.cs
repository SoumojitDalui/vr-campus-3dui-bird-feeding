using UnityEngine;

[DisallowMultipleComponent]
public sealed class FruitLibrary : MonoBehaviour {
    [SerializeField] private GameObject[] fruitPrefabs;

    public bool HasFruitPrefab(int fruitTypeIndex) {
        if (fruitPrefabs == null) return false;
        if (fruitTypeIndex < 0 || fruitTypeIndex >= fruitPrefabs.Length) return false;
        return fruitPrefabs[fruitTypeIndex] != null;
    }

    public GameObject InstantiateFruitPrefab(int fruitTypeIndex, Transform parent) {
        if (!HasFruitPrefab(fruitTypeIndex)) return null;
        GameObject prefab = fruitPrefabs[fruitTypeIndex];

        GameObject instance = Instantiate(prefab, parent, false);
        instance.name = "HeldFruit_" + prefab.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }
}
