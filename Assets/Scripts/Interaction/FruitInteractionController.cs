using System;
using System.Collections.Generic;
using UnityEngine;

public static class FruitInteractionController {
    public static FruitPickup UpdateFocusedFruit(Transform head, float fruitInteractDistance, FruitPickup currentFocused) {
        if (head == null) return currentFocused;
        FruitPickup nextFruit = null;
        Ray ray = new Ray(head.position, head.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, fruitInteractDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) {
            nextFruit = hit.collider != null ? hit.collider.GetComponentInParent<FruitPickup>() : null;
            if (nextFruit != null && nextFruit.IsCollected) nextFruit = null;
        }
        if (currentFocused == nextFruit) return currentFocused;
        if (currentFocused != null) currentFocused.SetHovered(false);
        if (nextFruit != null) nextFruit.SetHovered(true);
        return nextFruit;
    }

    public static FeedingZoneData UpdateActiveZone(Transform head, Transform rigRoot, List<FeedingZoneData> feedingZones, float interactMargin, float maxInteractRadius) {
        FeedingZoneData activeZone = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < feedingZones.Count; i++) {
            FeedingZoneData zone = feedingZones[i];
            Vector3 playerPos = head != null ? head.position : rigRoot.position;
            Vector3 zoneCenter = zone.Center;
            playerPos.y = 0f;
            zoneCenter.y = 0f;
            float effectiveRadius = Mathf.Max(0.5f, Mathf.Min(zone.Radius - interactMargin, maxInteractRadius));
            float distance = Vector3.Distance(playerPos, zoneCenter);
            if (distance > effectiveRadius) continue;
            if (distance < bestDistance) {
                bestDistance = distance;
                activeZone = zone;
            }
        }
        return activeZone;
    }

    public static bool CollectFruit(FruitPickup fruit, float time, ref float nextFruitPickupAllowedTime, int[] collectedCounts, float lockSeconds, Action<string> setStatus, Action updateChecklist) {
        if (fruit == null || fruit.IsCollected || time < nextFruitPickupAllowedTime) return false;
        collectedCounts[fruit.FruitTypeIndex]++;
        nextFruitPickupAllowedTime = time + lockSeconds;
        fruit.Collect();
        setStatus("Collected " + fruit.FruitTypeName + ".");
        updateChecklist();
        return true;
    }

    public static bool PrepareFruitForFeeding(FeedingZoneData zone, FruitDefinition[] fruitDefinitions, int[] collectedCounts, int[] fedCounts, int[] requiredCounts, ref int heldFruitType, ref FeedingZoneData heldFruitZone, ref BirdFetcher dispatchedBird, FruitLibrary fruitLibrary, Transform heldFruitAnchor, ref GameObject heldFruitVisual, float heldFruitScaleMultiplier, Action clearHeldFruit, Action<string> setStatus, Action updateChecklist) {
        if (zone == null) return false;
        string fruitName = fruitDefinitions[zone.FruitTypeIndex].Name;
        if (heldFruitType >= 0) {
            setStatus("You are already holding a fruit for a bird.");
            return false;
        }
        if (AvailableFruitCount(zone.FruitTypeIndex, heldFruitType, collectedCounts, fedCounts) <= 0) {
            if (fedCounts[zone.FruitTypeIndex] >= requiredCounts[zone.FruitTypeIndex]) {
                setStatus(zone.Label + " is complete.");
            } else {
                setStatus("You need more " + fruitName + " fruit.");
            }
            return false;
        }
        if (fruitLibrary == null || !fruitLibrary.HasFruitPrefab(zone.FruitTypeIndex)) return false;
        heldFruitType = zone.FruitTypeIndex;
        heldFruitZone = zone;
        dispatchedBird = null;
        SpawnHeldFruitVisual(zone.FruitTypeIndex, fruitLibrary, heldFruitAnchor, ref heldFruitVisual, heldFruitScaleMultiplier);
        if (heldFruitVisual == null) {
            clearHeldFruit();
            return false;
        }
        setStatus("Hold still in the bubble and let the bird take the fruit.");
        updateChecklist();
        return true;
    }

    public static void UpdateHeldFruitDispatchState(int heldFruitType, FeedingZoneData activeZone, FeedingZoneData heldFruitZone, ref BirdFetcher dispatchedBird, Action<string> setStatus) {
        if (heldFruitType < 0 || dispatchedBird == null || activeZone == heldFruitZone) return;
        dispatchedBird.CancelFetch();
        dispatchedBird = null;
        setStatus("Step back into the matching feeding zone so the bird can take the fruit.");
    }

    public static void TryDispatchHeldFruitToBird(int heldFruitType, FeedingZoneData activeZone, FeedingZoneData heldFruitZone, Transform heldFruitAnchor, ref BirdFetcher dispatchedBird, Action<int> completeFeed, Action<string> setStatus) {
        FeedingZoneData zone = heldFruitZone;
        if (heldFruitType < 0 || zone == null || activeZone != zone || zone.FruitTypeIndex != heldFruitType) return;
        if (zone.Bird == null || zone.Bird.IsBusy) return;
        if (!zone.Bird.IsInsideZone(zone.Center, zone.Radius)) return;
        int fruitType = heldFruitType;
        if (zone.Bird.TryBeginFetch(heldFruitAnchor, delegate { completeFeed(fruitType); })) {
            dispatchedBird = zone.Bird;
            setStatus("A bird is flying in to grab the fruit.");
        }
    }

    public static void CompleteFeed(int fruitTypeIndex, FruitDefinition[] fruitDefinitions, int[] fedCounts, int[] requiredCounts, Action clearHeldFruit, Action updateChecklist, Action<string, float> setStatusWithDuration) {
        fedCounts[fruitTypeIndex]++;
        clearHeldFruit();
        updateChecklist();
        if (AllFeedingTargetsMet(fedCounts, requiredCounts)) {
            setStatusWithDuration("All birds have been fed. Assignment 2 checklist complete.", 4f);
        } else {
            setStatusWithDuration("Fed the " + fruitDefinitions[fruitTypeIndex].Name + " bird.", 2.5f);
        }
    }

    public static void SpawnHeldFruitVisual(int fruitTypeIndex, FruitLibrary fruitLibrary, Transform heldFruitAnchor, ref GameObject heldFruitVisual, float heldFruitScaleMultiplier) {
        ClearHeldFruitVisual(ref heldFruitVisual);
        if (fruitLibrary == null) return;
        heldFruitVisual = fruitLibrary.InstantiateFruitPrefab(fruitTypeIndex, heldFruitAnchor);
        if (heldFruitVisual != null) heldFruitVisual.transform.localScale *= heldFruitScaleMultiplier;
    }

    public static void ClearHeldFruit(ref int heldFruitType, ref FeedingZoneData heldFruitZone, ref BirdFetcher dispatchedBird, ref GameObject heldFruitVisual) {
        heldFruitType = -1;
        heldFruitZone = null;
        dispatchedBird = null;
        ClearHeldFruitVisual(ref heldFruitVisual);
    }

    public static void ClearHeldFruitVisual(ref GameObject heldFruitVisual) {
        if (heldFruitVisual != null) {
            UnityEngine.Object.Destroy(heldFruitVisual);
            heldFruitVisual = null;
        }
    }

    public static bool AllFeedingTargetsMet(int[] fedCounts, int[] requiredCounts) {
        for (int i = 0; i < requiredCounts.Length; i++) {
            if (fedCounts[i] < requiredCounts[i]) return false;
        }
        return true;
    }

    public static int AvailableFruitCount(int fruitTypeIndex, int heldFruitType, int[] collectedCounts, int[] fedCounts) {
        int reserved = heldFruitType == fruitTypeIndex ? 1 : 0;
        return collectedCounts[fruitTypeIndex] - fedCounts[fruitTypeIndex] - reserved;
    }
}
