using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;


/// <summary>
/// Version of the boids controller that uses multithreading with Thread and locks for synchronization
/// </summary>
public class BoidsControllerBasicMultithread : BoidsController
{
    #region Public Attributes

    public const float MaxTimeWaiting = 0.25f;

    [Header("Multithread Attributes")]
    public int numThreads = 4;

    [Header("Multithread Boid Prefabs")]
    public BaseBoid gridBoidBasicMultithreadedPrefab = null;

    #endregion

    #region Private Attributes

    protected List<Thread> threads = new List<Thread>();
    protected List<(int startIndex, int endIndexExclusive)> startEndIndexes = new List<(int, int)>();
    protected volatile List<(bool mustStartCalcs, bool calcsFinished)> startCalcsForThreads = new List<(bool mustStartCalcs, bool calcsFinished)>();
    protected float deltaTime = 0.0f;

    #endregion

    #region Properties
    #endregion

    #region BoidsController Methods

    public override void Init()
    {
        // call the base method
        base.Init();

        // create the threads and start them
        threads.Clear();
        startEndIndexes.Clear();
        startCalcsForThreads.Clear();
        for (int i = 0; i < numThreads; ++i)
        {
            // create the thread
            Thread thread = new Thread(UpdateBoids);
            thread.Name = $"Boids Thread {i + 1}";
            thread.IsBackground = true;
            threads.Add(thread);

            // calculate the start and end indexes of the boids this thread needs to calculate
            int start = (i * NumBoids) / numThreads;
            int end = ((i + 1) * NumBoids) / numThreads;
            startEndIndexes.Add((start, end));

            // create the object to decide when to start the boid calcs and when they are finished
            startCalcsForThreads.Add((false, false));
        }

        // start the threads
        for (int i = 0; i < numThreads; ++i)
            threads[i].Start(i);

        Assert.IsTrue(threads.Count == numThreads);
        Assert.IsTrue(startEndIndexes.Count == numThreads);
        Assert.IsTrue(startCalcsForThreads.Count == numThreads);
    }

    protected override void FixedUpdate()
    {
        // set the data common to all threads
        deltaTime = Time.fixedDeltaTime;

        Profiler.BeginSample("Prepare for Update");

        // rebuild the grid
        RebuildGrid();

        //// prepare all the boids
        //foreach (BaseBoid boid in boids)
        //{
        //    MultithreadGridBoid multithreadBoid = boid as MultithreadGridBoid;
        //    multithreadBoid?.PrepareForUpdate();
        //}

        // tell all threads they are ready to start the boid updates
        for (int i = 0; i < startCalcsForThreads.Count; ++i)
            startCalcsForThreads[i] = (true, false);

        Profiler.EndSample();

        Profiler.BeginSample("Multithreaded Update");

        // wait for all threads to finish
        float timeWaiting = 0.0f;
        bool allThreadsFinished = false;
        while (!allThreadsFinished && timeWaiting < MaxTimeWaiting)
        {
            // wait a little bit
            long timeBefore = DateTime.Now.Ticks;
            Thread.Sleep(0);

            // update the time waiting
            long timeAfter = DateTime.Now.Ticks;
            long timeDiff = timeAfter - timeBefore;
            float timeDiffSeconds = (float)timeDiff / (float)TimeSpan.TicksPerSecond;
            timeWaiting += timeDiffSeconds;

            // check if all threads are finished
            allThreadsFinished = true;
            for (int i = 0; i < startCalcsForThreads.Count && allThreadsFinished; ++i)
                allThreadsFinished = startCalcsForThreads[i].calcsFinished;
        }

        Profiler.EndSample();

        Profiler.BeginSample("Synchronize Transforms");

        // sync the boids
        foreach (BaseBoid boid in boids)
        {
            MultithreadGridBoid multithreadBoid = boid as MultithreadGridBoid;
            multithreadBoid?.SyncTransform();
        }

        Profiler.EndSample();
    }

    public override void AddBoids(int numBoidsToAdd, BoidType boidType)
    {
        // if there are no boids to add, return
        if (numBoidsToAdd <= 0)
            return;

        // call the base method
        base.AddBoids(numBoidsToAdd, boidType);

        // recalculate the indexes of the boids each thread needs to calculate
        RecalculateStartEndIndexes();
    }

    #endregion

    #region Multithread Methods

    /// <summary>
    /// The method that each thread will execute to update the boids
    /// </summary>
    /// <param name="indexObj"></param>
    private void UpdateBoids(object indexObj)
    {
        int index = (int)indexObj;

        while (true)
        {
            // check if we have to start the calculations
            if (startCalcsForThreads[index].mustStartCalcs)
            {
                // get the indexes to iterate over
                int startIndex = startEndIndexes[index].startIndex;
                int endIndex = startEndIndexes[index].endIndexExclusive;

                // let's iterate over the boids we are assigned to calculate
                for (int i = startIndex; i < endIndex; ++i)
                {
                    // update the boid
                    BaseBoid boid = boids[i];
                    boid.ThreadIndex = index;
                    boid.DoUpdate(deltaTime);
                }

                // mark as finished
                startCalcsForThreads[index] = (false, true);
            }
            else
            {
                // wait to be told to start the calculations
                Thread.Sleep(0);
            }
        }
    }

    /// <summary>
    /// Recalculates the start/end indexes each thread needs to calculate
    /// </summary>
    protected virtual void RecalculateStartEndIndexes()
    {
        for (int i = 0; i < numThreads; ++i)
        {
            // calculate the start and end indexes of the boids this thread needs to calculate
            int start = (i * NumBoids) / numThreads;
            int end = ((i + 1) * NumBoids) / numThreads;
            startEndIndexes[i] = (start, end);
        }
    }

    /// <summary>
    /// Call Abort on all threads
    /// </summary>
    public virtual void AbortAllThreads()
    {
        for (int i = 0; i < threads.Count; ++i)
            threads[i].Abort();
    }

    /// <summary>
    /// Creates and returns a boid of the given type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected override BaseBoid CreateBoid(BoidType type)
    {
        BaseBoid boid;

        switch (type)
        {
            case BoidType.GridBoidBasicMultithreaded:
                boid = GameObject.Instantiate<BaseBoid>(gridBoidBasicMultithreadedPrefab);
                break;
            default:
                Debug.Log("We can only create multithread boids in a multithread boids controller!");
                boid = null;
                break;
        }

        if (boid != null)
        {
            boid.name = $"Boid {boidCreationCount++}";
            boid.transform.SetParent(transform);
        }

        return boid;
    }

    #endregion
}
