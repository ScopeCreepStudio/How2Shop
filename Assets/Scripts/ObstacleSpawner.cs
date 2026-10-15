using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("Lanes")]
    [SerializeField] private float laneDistance = 2.5f;

    [Header("Spawning")]
    [SerializeField] private float spawnDistance = 80f;
    [SerializeField] private float despawnDistance = 10f;
    [SerializeField] private float firstRowDistance = 30f;
    [SerializeField] private Vector2 rowSpacing = new Vector2(8f, 16f);

    [Range(1, 2)]
    [SerializeField] private int maxObstaclesPerRow = 2;

    private struct ActiveObstacle
    {
        public GameObject Instance;
        public GameObject Prefab;
    }

    private readonly List<ActiveObstacle> active = new();
    private readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
    private readonly int[] laneOrder = { 0, 1, 2 };

    private float playerZ;
    private float centerX;
    private float distanceToNextRow;

    private void Start()
    {
        playerZ = player.position.z;
        centerX = player.position.x;
        Spawner();
    }

    private void Update()
    {
        RunnerWorld world = RunnerWorld.Instance;
        if (world == null) return;

        float dz = world.Speed * Time.deltaTime;
        if (dz <= 0f) return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Transform t = active[i].Instance.transform;
            Vector3 p = t.position;
            p.z -= dz;
            t.position = p;

            if (p.z < playerZ - despawnDistance)
            {
                Release(active[i]);
                active.RemoveAt(i);
            }
        }

        distanceToNextRow -= dz;
        while (distanceToNextRow <= 0f)
        {
            SpawnRow(playerZ + spawnDistance + distanceToNextRow);
            distanceToNextRow += Random.Range(rowSpacing.x, rowSpacing.y);
        }
    }

    public void ResetSpawner()
    {
        foreach (ActiveObstacle item in active) Release(item);
        active.Clear();
        Spawner();
    }

    private void Spawner()
    {
        float spawnZ = playerZ + spawnDistance;
        float z = playerZ + firstRowDistance;

        while (z < spawnZ)
        {
            SpawnRow(z);
            z += Random.Range(rowSpacing.x, rowSpacing.y);
        }

        distanceToNextRow = z - spawnZ;
    }

    private void SpawnRow(float z)
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;

        for (int i = laneOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (laneOrder[i], laneOrder[j]) = (laneOrder[j], laneOrder[i]);
        }

        int count = Random.Range(1, maxObstaclesPerRow + 1);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            float x = centerX + (laneOrder[i] - 1) * laneDistance;

            GameObject obj = Get(prefab);
            obj.transform.SetPositionAndRotation(
                new Vector3(x, prefab.transform.position.y, z),
                prefab.transform.rotation);

            active.Add(new ActiveObstacle { Instance = obj, Prefab = prefab });
        }
    }


    private GameObject Get(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }

        if (stack.Count > 0)
        {
            GameObject pooled = stack.Pop();
            pooled.SetActive(true);
            return pooled;
        }

        return Instantiate(prefab, transform);
    }

    private void Release(ActiveObstacle item)
    {
        item.Instance.SetActive(false);
        pools[item.Prefab].Push(item.Instance);
    }
}
