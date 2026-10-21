using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates the Hodaart cashier and scan lane in the playable store scenes.</summary>
internal static class CashierSceneBootstrap
{
    private const string CharacterResourceName = "CashierCharacter01";
    private const string RuntimeCashierName = "Cashier Character 01";
    private const float FallbackCounterHeight = 1.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallbacks()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInActiveScene()
    {
        Install(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

private static void Install(Scene scene)
    {
        if (!scene.IsValid() || (scene.name != "MainScene" && scene.name != "Superstore"))
            return;

        foreach (CashierScanAnimation existing in UnityEngine.Object.FindObjectsByType<CashierScanAnimation>())
        {
            if (existing.gameObject.scene == scene)
            {
                if (existing.GetComponent<ShopperRagdoll>() == null)
                    existing.gameObject.AddComponent<ShopperRagdoll>();
                return;
            }
        }

        GameObject characterPrefab = Resources.Load<GameObject>(CharacterResourceName);
        if (characterPrefab == null)
        {
            Debug.LogError($"Cashier character resource '{CharacterResourceName}' was not found.");
            return;
        }

        Transform station = FindStation(scene);
        if (station == null)
        {
            Debug.LogWarning($"No checkout station was found in scene '{scene.name}'.");
            return;
        }

        Transform belt = FindBelt(station);
        if (belt == null)
            belt = FindClosestBelt(scene, station.position);

        Bounds beltBounds = default;
        bool hasBeltBounds = belt != null && TryGetRendererBounds(belt, out beltBounds);
        if (!hasBeltBounds)
        {
            beltBounds = new Bounds(station.position + station.forward * 0.7f + Vector3.up * FallbackCounterHeight,
                new Vector3(1.8f, 0.25f, 1.1f));
        }

        // The checkout root faces the customer aisle. Keep the cashier placement
        // stable even while the player's camera is in the opening cutscene.
        Vector3 towardShopper = Vector3.ProjectOnPlane(station.forward, Vector3.up);
        if (towardShopper.sqrMagnitude < 0.001f && Camera.main != null)
            towardShopper = Vector3.ProjectOnPlane(Camera.main.transform.position - beltBounds.center, Vector3.up);
        if (towardShopper.sqrMagnitude < 0.001f)
            towardShopper = Vector3.forward;
        towardShopper.Normalize();

        Vector3 employeeSide = -towardShopper;
        Vector3 cashierPosition = station.position + employeeSide * 0.9f;
        cashierPosition.y = FindFloorHeight(cashierPosition, beltBounds.min.y, station);

        GameObject cashier = UnityEngine.Object.Instantiate(characterPrefab, cashierPosition,
            Quaternion.LookRotation(towardShopper, Vector3.up));
        cashier.name = RuntimeCashierName;
        SceneManager.MoveGameObjectToScene(cashier, scene);

        Animator animator = cashier.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.applyRootMotion = false;

        CashierScanAnimation scanAnimation = cashier.AddComponent<CashierScanAnimation>();
        cashier.AddComponent<ShopperRagdoll>();
        CreateScanZone(scene, beltBounds, towardShopper, scanAnimation);
        Debug.Log($"Cashier Character 01 installed at '{station.name}' in '{scene.name}'.");
    }

    private static Transform FindStation(Scene scene)
    {
        var stations = new List<Transform>();
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate.gameObject.scene != scene)
                continue;

            if (candidate.name == "Cashier1" || candidate.name.StartsWith("Cashier1 (", StringComparison.Ordinal))
                stations.Add(candidate);
        }

        if (stations.Count == 0)
        {
            Transform belt = FindClosestBelt(scene, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            return belt != null ? belt.parent : null;
        }

        Vector3 reference = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        Transform closest = stations[0];
        float closestDistance = Vector3.SqrMagnitude(closest.position - reference);
        for (int i = 1; i < stations.Count; i++)
        {
            float distance = Vector3.SqrMagnitude(stations[i].position - reference);
            if (distance < closestDistance)
            {
                closest = stations[i];
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static Transform FindBelt(Transform station)
    {
        foreach (Transform child in station.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf("checkout_belt", StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }

        return null;
    }

    private static Transform FindClosestBelt(Scene scene, Vector3 position)
    {
        Transform closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>())
        {
            if (candidate.gameObject.scene != scene ||
                candidate.name.IndexOf("checkout_belt", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            float distance = Vector3.SqrMagnitude(candidate.position - position);
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static float ProjectedBoundsRadius(Bounds bounds, Vector3 direction)
    {
        Vector3 extents = bounds.extents;
        return Mathf.Abs(direction.x) * extents.x +
               Mathf.Abs(direction.y) * extents.y +
               Mathf.Abs(direction.z) * extents.z;
    }

private static float FindFloorHeight(Vector3 position, float fallback, Transform ignoredRoot)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 4f, Vector3.down, 8f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float floor = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.55f)
                continue;

            Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
            if (ignoredRoot != null && hitTransform != null &&
                (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot)))
                continue;

            floor = Mathf.Max(floor, hit.point.y);
        }

        return float.IsNegativeInfinity(floor) ? Mathf.Min(0f, Mathf.Min(fallback, position.y) - 0.5f) : floor;
    }

    private static void CreateScanZone(Scene scene, Bounds beltBounds, Vector3 launchDirection,
        CashierScanAnimation scanAnimation)
    {
        var zone = new GameObject("Cashier Checkout Scan Zone");
        SceneManager.MoveGameObjectToScene(zone, scene);
        zone.transform.position = beltBounds.center + Vector3.up * 0.28f;

        BoxCollider trigger = zone.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(
            Mathf.Clamp(beltBounds.size.x * 0.9f, 1.1f, 3.2f),
            0.85f,
            Mathf.Clamp(beltBounds.size.z * 0.9f, 0.8f, 2.1f));

        Rigidbody body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        CashierCheckoutConveyor conveyor = zone.AddComponent<CashierCheckoutConveyor>();
        conveyor.Configure(-launchDirection, Mathf.Clamp(ProjectedBoundsRadius(beltBounds, launchDirection) * 2f + 1.5f, 1.8f, 4f), scanAnimation);
    }
}
