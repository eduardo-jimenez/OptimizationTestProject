using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using static JobsGrid;


/// <summary>
/// Version of the boids controller that uses Jobs
/// </summary>
public class BoidsControllerJobs : MonoBehaviour
{
    #region Public Attributes

    public const float MaxTimeWaiting = 0.25f;

    public const int InitialMaxCapacityBoidsList = 32 * 1024;
    public const int MaxCellsInRadiusPerBoid = 64;
    public const int MaxNearbyBoids = 64;

    [Header("Zone Parameters")]
    public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(320.0f / 9.0f, 20.0f, 0.0f));

    [Header("Grid Config")]
    public Vector2Int gridSize = new Vector2Int(64, 36);

    [Header("Jobs Boid Prefabs")]
    public JobsBoidObj boidPrefab = null;
    public int numBoidsPerJob = 8;

    #endregion

    #region Private Attributes

    protected List<JobsBoidObj> boids = new List<JobsBoidObj>(InitialMaxCapacityBoidsList);
    protected NativeList<JobsBoid> jobBoids;

    protected JobsGrid grid = new JobsGrid();
    protected int maxBoidsToHandlePerBoid = 10;

    protected NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist> nearbyBoidsPerBoid;
    protected NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo> cellsInRadiusPerBoid;

    #endregion

    #region Properties

    public int NumBoids => (boids != null) ? boids.Count : 0;

    #endregion

    #region MonoBehaviour Methods

    protected virtual void Start()
    {
        Init();
    }

    protected virtual void Update()
    {
        float dt = Time.deltaTime;

        Profiler.BeginSample("Clear HashMaps");

        bool newCollections = false;
        int nearbyBoidsSize = maxBoidsToHandlePerBoid * math.max(1, jobBoids.Length);
        if (nearbyBoidsPerBoid.Capacity != nearbyBoidsSize)
        {
            nearbyBoidsPerBoid.Dispose();
            nearbyBoidsPerBoid = new NativeParallelMultiHashMap<int, BoidInCellPlusDist>(nearbyBoidsSize, AllocatorManager.Persistent);
            cellsInRadiusPerBoid.Dispose();
            int cellsInRadiusSize = MaxCellsInRadiusPerBoid * jobBoids.Length;
            cellsInRadiusPerBoid = new NativeParallelMultiHashMap<int, CellInRadiusInfo>(cellsInRadiusSize, AllocatorManager.Persistent);
            newCollections = true;
        }

        ClearHashMapsJob clearHashmapsJob = new ClearHashMapsJob
        {
            mustClear = !newCollections,
            nearbyBoidsInfoPerBoid = nearbyBoidsPerBoid,
            cellsInRadiusPerBoid = cellsInRadiusPerBoid,
        };
        JobHandle clearHashmapsJobHandle = clearHashmapsJob.Schedule();

        Profiler.EndSample();

        // rebuild the grid
        RebuildGrid();

        Profiler.BeginSample("Jobs");

        // start by finding the cells in radius
        FindCellsInRadiusJob findCellsJob = new FindCellsInRadiusJob
        {
            gridInfo = grid.Info,
            cells = grid.Cells,
            boidsInCells = grid.BoidsInCells,
            boids = jobBoids,
            cellsInRadiusPerBoid = cellsInRadiusPerBoid.AsParallelWriter(),
        };
        JobHandle cellsJobHandle = findCellsJob.Schedule(jobBoids.Length, numBoidsPerJob, clearHashmapsJobHandle);

        FillNearbyBoidsListsJob fillNearbyBoidsJob = new FillNearbyBoidsListsJob
        {
            gridInfo = grid.Info,
            cells = grid.Cells,
            boidsInCells = grid.BoidsInCells,
            boids = jobBoids,
            cellsInRadiusPerBoid = cellsInRadiusPerBoid,
            nearbyBoidsInfoLists = nearbyBoidsPerBoid.AsParallelWriter(),
        };
        JobHandle nearbyBoidsJobHandle = fillNearbyBoidsJob.Schedule(jobBoids.Length, numBoidsPerJob, cellsJobHandle);

        // create a job to update the boids
        UpdateForcesJob updateJob = new UpdateForcesJob
        {
            deltaTime = dt,
            bounds = bounds,

            gridInfo = grid.Info,
            cells = grid.Cells,
            boidsInCells = grid.BoidsInCells,
            boids = jobBoids.AsArray(),

            nearbyBoidsInfoLists = nearbyBoidsPerBoid,
        };
        JobHandle jobHandle = updateJob.Schedule(jobBoids.Length, numBoidsPerJob, nearbyBoidsJobHandle);
        jobHandle.Complete();

        Profiler.EndSample();

        Profiler.BeginSample("Copy Results to Unity");

        // finally let's copy the data back to the boid objects
        for (int i = 0; i < boids.Count; ++i)
            boids[i].UpdateFromJob(jobBoids[i]);

        Profiler.EndSample();
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.7f, 0.9f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    #endregion

    #region Initialization Methods

    public virtual void Init()
    {
        // create the shared lists
        maxBoidsToHandlePerBoid = boidPrefab.maxBoidsToHandle;
        nearbyBoidsPerBoid = new NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>(1 * maxBoidsToHandlePerBoid, AllocatorManager.Persistent);
        cellsInRadiusPerBoid = new NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo>(1 * MaxCellsInRadiusPerBoid, AllocatorManager.Persistent);

        // initialize the grid
        grid.Init(this, gridSize.x, gridSize.y);

        // create the lists of boids
        boids.Clear();
        if (jobBoids.IsCreated)
            jobBoids.Dispose();
        jobBoids = new NativeList<JobsBoid>(InitialMaxCapacityBoidsList, AllocatorManager.Persistent);
    }

    protected virtual JobsBoidObj CreateBoid()
    {
        JobsBoidObj boid = GameObject.Instantiate<JobsBoidObj>(boidPrefab);
        boid.name = $"Boid {boids.Count}";
        boid.transform.SetParent(transform);

        return boid;
    }

    public void ClearBoids()
    {
        // destroy all the boids
        foreach (BaseBoid boid in boids)
            Destroy(boid.gameObject);

        // clear the list of boids
        boids.Clear();
        jobBoids.Clear();
    }

    #endregion

    #region Methods

    public virtual void AddBoids(int numBoidsToAdd)
    {
        Vector3 minPos = bounds.center - 0.9f * bounds.extents;
        Vector3 maxPos = bounds.center + 0.9f * bounds.extents;

        for (int i = 0; i < numBoidsToAdd; ++i)
        {
            // create the boid
            JobsBoidObj boid = CreateBoid();

            // find a random position and direction for the new boid
            float x = UnityEngine.Random.Range(minPos.x, maxPos.x);
            float y = UnityEngine.Random.Range(minPos.y, maxPos.y);
            float angle = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            Vector2 pos = new Vector2(x, y);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // set the position and direction to the boid and initialize it
            int index = boids.Count;
            boid.Pos = pos;
            boid.Dir = dir;
            boid.Init(this, index);
            boid.Vel = dir * boid.minSpeed;

            // create the jobs boid
            JobsBoid jobsBoid = new JobsBoid();
            jobsBoid.Init(boid, index);

            // add the boid to the list
            boids.Add(boid);
            jobBoids.Add(jobsBoid);
        }
    }

    public virtual void RebuildGrid()
    {
        Profiler.BeginSample("RebuildGrid");

        grid.BuildGrid(jobBoids);

        Profiler.EndSample();
    }

    #endregion
}


[BurstCompile]
public struct ClearHashMapsJob : IJob
{
    public bool mustClear;
    public NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo> cellsInRadiusPerBoid;
    public NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist> nearbyBoidsInfoPerBoid;

    public void Execute()
    {
        if (mustClear)
        {
            cellsInRadiusPerBoid.Clear();
            nearbyBoidsInfoPerBoid.Clear();
        }
    }
}

[BurstCompile]
public struct FindCellsInRadiusJob : IJobParallelFor
{
    [ReadOnly] public NativeList<JobsBoid> boids;
    [ReadOnly] public JobsGrid.GridInfo gridInfo;
    [ReadOnly] public NativeArray<JobsGrid.Cell> cells;
    [ReadOnly] public SharedLists<JobsGrid.BoidInCellInfo> boidsInCells;

    public NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo>.ParallelWriter cellsInRadiusPerBoid;

    public void Execute(int i)
    {
        boids[i].FindCellsInRadius(gridInfo, cells, boidsInCells, cellsInRadiusPerBoid);
    }
}

[BurstCompile]
public struct FillNearbyBoidsListsJob : IJobParallelFor
{
    [ReadOnly] public NativeList<JobsBoid> boids;
    [ReadOnly] public JobsGrid.GridInfo gridInfo;
    [ReadOnly] public NativeArray<JobsGrid.Cell> cells;
    [ReadOnly] public SharedLists<JobsGrid.BoidInCellInfo> boidsInCells;
    [ReadOnly] public NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo> cellsInRadiusPerBoid;

    public NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsInfoLists;

    public void Execute(int i)
    {
        boids[i].FindBoidsInRadius(gridInfo, cells, boidsInCells, cellsInRadiusPerBoid, nearbyBoidsInfoLists);
    }
}

[BurstCompile]
public struct UpdateForcesJob : IJobParallelFor
{
    public NativeArray<JobsBoid> boids;
    [ReadOnly] public JobsGrid.GridInfo gridInfo;
    [ReadOnly] public NativeArray<JobsGrid.Cell> cells;
    [ReadOnly] public SharedLists<JobsGrid.BoidInCellInfo> boidsInCells;
    [ReadOnly] public NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist> nearbyBoidsInfoLists;

    public Bounds bounds;
    public float deltaTime;

    public void Execute(int i)
    {
        JobsBoid b = boids[i];
        b.UpdateForces(deltaTime, bounds, gridInfo, cells, boidsInCells, nearbyBoidsInfoLists);
        boids[i] = b;
    }
}

