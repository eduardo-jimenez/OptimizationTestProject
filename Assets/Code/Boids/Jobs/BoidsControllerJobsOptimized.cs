using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using static JobsGrid;


/// <summary>
/// Version of the boids controller that uses Jobs
/// </summary>
public class BoidsControllerJobsOptimized : BoidsControllerJobs
{
    #region Public Attributes
    #endregion

    #region Private Attributes
    #endregion

    #region Properties
    #endregion

    #region MonoBehaviour Methods

    protected override void Update()
    {
        float dt = Time.deltaTime;

        Profiler.BeginSample("Clear HashMaps");

        bool newCollections = false;
        int nearbyBoidsSize = maxBoidsToHandlePerBoid * math.max(1, jobBoids.Length);
        if (nearbyBoidsPerBoid.Capacity != nearbyBoidsSize)
        {
            nearbyBoidsPerBoid.Dispose();
            nearbyBoidsPerBoid = new NativeParallelMultiHashMap<int, BoidInCellPlusDist>(nearbyBoidsSize, AllocatorManager.Persistent);
            newCollections = true;
        }

        ClearBoidsHashMapsJob clearHashmapsJob = new ClearBoidsHashMapsJob
        {
            mustClear = !newCollections,
            nearbyBoidsInfoPerBoid = nearbyBoidsPerBoid,
        };
        JobHandle clearHashmapsJobHandle = clearHashmapsJob.Schedule();

        Profiler.EndSample();

        // rebuild the grid
        RebuildGrid();

        Profiler.BeginSample("Jobs");

        // find the nearby boids
        FullFindNearbyBoidsListsJob fillNearbyBoidsJob = new FullFindNearbyBoidsListsJob
        {
            gridInfo = grid.Info,
            cells = grid.Cells,
            boidsInCells = grid.BoidsInCells,
            boids = jobBoids,
            nearbyBoidsInfoPerBoid = nearbyBoidsPerBoid.AsParallelWriter(),
        };
        JobHandle nearbyBoidsJobHandle = fillNearbyBoidsJob.Schedule(jobBoids.Length, numBoidsPerJob, clearHashmapsJobHandle);

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

    #endregion

    #region Initialization Methods

    public override void Init()
    {
        base.Init();
    }

    #endregion

    #region Methods
    #endregion
}

[BurstCompile]
public struct FullFindNearbyBoidsListsJob : IJobParallelFor
{
    [ReadOnly] public NativeList<JobsBoid> boids;
    [ReadOnly] public JobsGrid.GridInfo gridInfo;
    [ReadOnly] public NativeArray<JobsGrid.Cell> cells;
    [ReadOnly] public SharedLists<JobsGrid.BoidInCellInfo> boidsInCells;

    public NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsInfoPerBoid;

    public void Execute(int i)
    {
        boids[i].FullFindBoidsInRadius(gridInfo, cells, boidsInCells, nearbyBoidsInfoPerBoid);
    }
}

[BurstCompile]
public struct ClearBoidsHashMapsJob : IJob
{
    public bool mustClear;
    public NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist> nearbyBoidsInfoPerBoid;

    public void Execute()
    {
        if (mustClear)
        {
            nearbyBoidsInfoPerBoid.Clear();
        }
    }
}

