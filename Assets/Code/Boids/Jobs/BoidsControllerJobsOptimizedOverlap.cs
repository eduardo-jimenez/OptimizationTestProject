using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


/// <summary>
/// Version of the boids controller that uses Jobs
/// </summary>
public class BoidsControllerJobsOptimizedOverlap : BoidsControllerJobsOptimized3
{
    #region Public Attributes
    #endregion

    #region Private Attributes

    private JobHandle jobHandle = default(JobHandle);

    #endregion

    #region Properties
    #endregion

    #region MonoBehaviour Methods

    protected override void Update()
    {
        if (boids.Count == 0)
            return;

        // then copy the info to the transforms
        CopyBoidsToTransformJob copyBoidsJob = new CopyBoidsToTransformJob
        {
            boids = jobBoids,
        };
        JobHandle copyJobHandle = copyBoidsJob.Schedule(boidTransforms, jobHandle);

        copyJobHandle.Complete();

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
        jobHandle = updateJob.Schedule(jobBoids.Length, numBoidsPerJob);

        Profiler.EndSample();
    }

    #endregion

    #region Initialization Methods
    #endregion

    #region Methods

    public override void AddBoids(int numBoidsToAdd)
    {
        // wait for the job to finish
        jobHandle.Complete();

        // now add the new boids
        base.AddBoids(numBoidsToAdd);
    }

    #endregion
}
