using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// Version of the boids controller that uses multithreading with Tasks but also will wait for the completion of the tasks from the preivous ticks, 
/// instead of waiting in the same tick
/// </summary>
public class BoidsControllerTasksOverlap : BoidsControllerBasicMultithread
{
    #region Public Attributes

    [Header("Tasks")]
    public int numBoidsPerTask = 25;
    public int maxNumThreads = 256;

    #endregion

    #region Private Attributes

    private List<Task> tasks = new List<Task>();

    private int numBoidsToAddNextTick = 0;
    private BoidType boidType = BoidType.GridBoidBasicMultithreaded;

    #endregion

    #region Properties
    #endregion

    #region BoidsController Methods

    /// <summary>
    /// Initialization
    /// </summary>
    public override void Init()
    {
        // get the maximum number of threads to use it for the thread index
        ThreadPool.GetAvailableThreads(out numThreads, out int numCompletionPortThreads);
        numThreads = Mathf.Min(numThreads, maxNumThreads);

        // create the grid
        grid.Init(this, gridSize);

        // add the initial number of boids
        AddBoids(initialNumBoids, BoidType.Basic);

        // create the threads and start them
        threads.Clear();
        startEndIndexes.Clear();
        startCalcsForThreads.Clear();

        // clear the list of tasks
        tasks.Clear();
    }

    protected override void FixedUpdate()
    {
        // do nothing
    }

    /// <summary>
    /// Main update of the controller
    /// </summary>
    protected virtual /*async*/ void Update()
    {
        Profiler.BeginSample("Wait for Tasks to Finish");

        // wait for all the tasks to finish
        Task.WaitAll(tasks.ToArray());
        //await WaitAllTasks(tasks);

        Profiler.EndSample();

        Profiler.BeginSample("Synchronize Transforms");

        // sync the boids
        foreach (BaseBoid boid in boids)
        {
            MultithreadGridBoid multithreadBoid = boid as MultithreadGridBoid;
            multithreadBoid?.SyncTransform();
        }

        Profiler.EndSample();

        // set the data common to all threads
        deltaTime = Time.deltaTime;//Time.fixedDeltaTime;

        Profiler.BeginSample("Add New Boids");

        // add the boids we had pending
        if (numBoidsToAddNextTick > 0)
        {
            DoAddBoids(numBoidsToAddNextTick, boidType);
            numBoidsToAddNextTick = 0;
        }

        Profiler.EndSample();

        Profiler.BeginSample("Prepare for Update");

        // rebuild the grid
        RebuildGrid();

        //Profiler.BeginSample("Prepare boids for Update");

        //// prepare all the boids
        //foreach (BaseBoid boid in boids)
        //{
        //    MultithreadGridBoid multithreadBoid = boid as MultithreadGridBoid;
        //    multithreadBoid?.PrepareForUpdate();
        //}

        //Profiler.EndSample();

        Profiler.BeginSample("Add Tasks");

        // prepare the tasks
        int numBoids = 0;
        int index = 0;
        tasks.Clear();
        while (numBoids < boids.Count)
        {
            // prepare the info for the next task
            int boidsInTask = Mathf.Min(boids.Count - numBoids, numBoidsPerTask);
            (int index, int startIndex, int endIndex) info = (index % numThreads, numBoids, numBoids + boidsInTask);

            // update the info
            ++index;
            numBoids += boidsInTask;

            // run the task
            Task task = Task.Factory.StartNew(UpdateBoidsTask, info);
            tasks.Add(task);
        }

        Profiler.EndSample();

        Profiler.EndSample();
    }

    /// <summary>
    /// Adds the given number of boids
    /// </summary>
    public override void AddBoids(int numBoidsToAdd, BoidType boidType)
    {
        numBoidsToAddNextTick += numBoidsToAdd;
        this.boidType = boidType;
    }

    /// <summary>
    /// Adds the given number of boids
    /// </summary>
    public void DoAddBoids(int numBoidsToAdd, BoidType boidType)
    {
        Vector3 minPos = bounds.center - 0.9f * bounds.extents;
        Vector3 maxPos = bounds.center + 0.9f * bounds.extents;

        for (int i = 0; i < numBoidsToAdd; ++i)
        {
            // create the boid
            BaseBoid boid = CreateBoid(boidType);

            // find a random position and direction for the new boid
            float x = UnityEngine.Random.Range(minPos.x, maxPos.x);
            float y = UnityEngine.Random.Range(minPos.y, maxPos.y);
            float angle = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
            Vector2 pos = new Vector2(x, y);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // set the position and direction to the boid and initialize it
            boid.Pos = pos;
            boid.Dir = dir;
            boid.Init(this);
            boid.Vel = dir * boid.minSpeed;

            // add the boid to the list
            boids.Add(boid);
        }
    }

    #endregion

    #region Multithread Methods

    /// <summary>
    /// The method that each thread will execute to update the boids
    /// </summary>
    /// <param name="infoObj"></param>
    private void UpdateBoidsTask(object infoObj)
    {
        Profiler.BeginSample("UpdateBoidsTask");

        // get the indexes to iterate over from the given info
        (int threadIndex, int startIndex, int endIndexExclusive) info = ((int, int, int))infoObj;

        // let's iterate over the boids we are assigned to calculate
        for (int i = info.startIndex; i < info.endIndexExclusive; ++i)
        {
            // update the boid
            BaseBoid boid = boids[i];
            boid.ThreadIndex = info.threadIndex;
            boid.DoUpdate(deltaTime);
        }

        Profiler.EndSample();
    }

    /// <summary>
    /// Another way of waiting for all tasks to finish
    /// </summary>
    /// <param name="tasks"></param>
    /// <returns></returns>
    private async Task WaitAllTasks(List<Task> tasks)
    {
        foreach (Task task in tasks)
            await task;
    }

    #endregion
}
