using System.Collections.Generic;
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
public class BoidsControllerJobsOptimized3 : BoidsControllerJobsOptimized2
{
    #region Public Attributes
    #endregion

    #region Private Attributes

    protected TransformAccessArray boidTransforms;

    #endregion

    #region Properties
    #endregion

    #region MonoBehaviour Methods

    protected override void Update()
    {
        if (boids.Count == 0)
            return;

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

        // then copy the info to the transforms
        CopyBoidsToTransformJob copyBoidsJob = new CopyBoidsToTransformJob
        {
            boids = jobBoids,
        };
        JobHandle copyJobHandle = copyBoidsJob.Schedule(boidTransforms, jobHandle);

        copyJobHandle.Complete();

        Profiler.EndSample();
    }

    #endregion

    #region Initialization Methods
    #endregion

    #region Methods

    public override void AddBoids(int numBoidsToAdd)
    {
        base.AddBoids(numBoidsToAdd);

        // update the transforms array
        if (boidTransforms.isCreated)
            boidTransforms.Dispose();
        boidTransforms = new TransformAccessArray(boids.Count);
        for (int i = 0; i < boids.Count; ++i)
            boidTransforms.Add(boids[i].transform);
    }

    #endregion
}

[BurstCompile]
public struct CopyBoidsToTransformJob : IJobParallelForTransform
{
    [ReadOnly] public NativeList<JobsBoid> boids;

    public void Execute(int index, TransformAccess transform)
    {
        float2 pos = boids[index].Pos;
        float2 dir = boids[index].Dir;
        float angleRads = math.atan2(dir.y, dir.x);
        float angle = math.TODEGREES * angleRads;
        transform.position = new Vector3(pos.x, pos.y, 0.0f);
        transform.rotation = Quaternion.AngleAxis(angle, new Vector3(0.0f, 0.0f, 1.0f));
    }
}
