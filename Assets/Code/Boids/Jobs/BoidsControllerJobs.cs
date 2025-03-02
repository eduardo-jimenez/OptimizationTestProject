using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
using UnityEngine.Profiling;


/// <summary>
/// Version of the boids controller that uses Jobs
/// </summary>
public class BoidsControllerJobs : MonoBehaviour
{
    #region Public Attributes

    public const float MaxTimeWaiting = 0.25f;

    public const int InitialMaxCapacityBoidsList = 32 * 1024;

    [Header("Zone Parameters")]
    public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(320.0f / 9.0f, 20.0f, 0.0f));

    [Header("Grid Config")]
    public Vector2Int gridSize = new Vector2Int(64, 36);

    [Header("Jobs Boid Prefabs")]
    public JobsBoidObj boidPrefab = null;

    #endregion

    #region Private Attributes

    protected List<JobsBoidObj> boids = new List<JobsBoidObj>(InitialMaxCapacityBoidsList);
    protected NativeList<JobsBoid> jobBoids;

    protected JobsGrid grid = new JobsGrid();

    protected List<JobsGrid.BoidInCellInfo> nearbyBoidInfos = new List<JobsGrid.BoidInCellInfo>();
    protected List<BaseBoid> nearbyBoids = new List<BaseBoid>();

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

        // rebuild the grid
        RebuildGrid();

        // update all the boids
        foreach (BaseBoid boid in boids)
            boid.DoUpdate(dt);
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

    public void AddBoids(int numBoidsToAdd)
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
            jobBoids.Add(jobsBoid);

            // add the boid to the list
            boids.Add(boid);
            jobBoids.Add(jobsBoid);
        }
    }

    public void RebuildGrid()
    {
        Profiler.BeginSample("RebuildGrid");

        grid.BuildGrid(jobBoids);

        Profiler.EndSample();
    }

    public List<BaseBoid> FindBoidsInCircleBruteForce(float2 pos, float radius, JobsBoidObj boidToIgnore)
    {
        Profiler.BeginSample("FindBoidsInCircle Grid");

        // find the boids
        nearbyBoidInfos.Clear();
        JobsBoid jobsBoid = jobBoids[boidToIgnore.Index];
        grid.FindBoidsInRadius(pos, radius, jobsBoid, ref nearbyBoidInfos);

        // convert the list to a list of actual voids
        nearbyBoidInfos.Clear();
        for (int i = 0; i < nearbyBoidInfos.Count; ++i)
        {
            JobsGrid.BoidInCellInfo boidInfo = nearbyBoidInfos[i];
            JobsBoidObj boid = boids[boidInfo.index];
            nearbyBoids.Add(boid);
        }

        Profiler.EndSample();

        return nearbyBoids;
    }

    #endregion
}

