using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


/// <summary>
/// Version of the boids controller that uses Jobs
/// </summary>
public class BoidsControllerJobsOptimized2 : BoidsControllerJobs
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

        // rebuild the grid
        RebuildGrid();

        Profiler.BeginSample("Jobs");

        // create a job to update the boids
        FullBoidsUpdateJob updateJob = new FullBoidsUpdateJob
        {
            deltaTime = dt,
            bounds = bounds,

            gridInfo = grid.Info,
            cells = grid.Cells,
            boidsInCells = grid.BoidsInCells,
            boids = jobBoids.AsArray(),
        };
        JobHandle jobHandle = updateJob.Schedule(jobBoids.Length, numBoidsPerJob);
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
public struct FullBoidsUpdateJob : IJobParallelFor
{
    public NativeArray<JobsBoid> boids;
    [ReadOnly] public JobsGrid.GridInfo gridInfo;
    [ReadOnly] public NativeArray<JobsGrid.Cell> cells;
    [ReadOnly] public SharedLists<JobsGrid.BoidInCellInfo> boidsInCells;

    public Bounds bounds;
    public float deltaTime;

    public void Execute(int i)
    {
        JobsBoid b = boids[i];
        b.FullUpdate(deltaTime, bounds, gridInfo, cells, boidsInCells);
        boids[i] = b;
    }
}
