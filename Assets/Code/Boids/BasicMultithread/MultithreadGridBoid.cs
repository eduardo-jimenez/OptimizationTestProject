using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This boid type uses the GridBoidLimitNums calculations but adds an extra buffer to support executing it multithreaded
/// </summary>
public class MultithreadGridBoid : GridBoidLimitNums
{
    #region Attributes

    public const float MinVelToUpdateDirSq = 0.01f;

    protected int threadIndex = 0;

    protected Vector2 dir = new Vector3(1.0f, 0.0f);

    #endregion

    #region Properties

    public override int ThreadIndex
    {
        get => threadIndex; 
        set => threadIndex = value;
    }

    public override Vector2 Pos
    {
        get => pos;
        set => base.Pos = value;
    }
    public override Vector2 Dir
    {
        get => dir;
        set => base.Dir = value;
    }

    #endregion

    #region GridBoid Methods

    /// <summary>
    /// Initialization
    /// </summary>
    /// <param name="boidsCtrl"></param>
    public override void Init(BoidsController boidsCtrl)
    {
        base.Init(boidsCtrl);

        // set the pos and dir values
        pos = new Vector2(transform.position.x, transform.position.y);
        dir = new Vector2(transform.right.x, transform.right.y);
    }

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    /// <param name="dt"></param>
    public override void DoUpdate(float dt)
    {
        Profiler.BeginSample("MultithreadBoid.DoUpdate()");

        // get all the boids that we need for the update methods
        float maxRadius = Mathf.Max(Mathf.Max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        boidsCtrl.FindNearestBoidsInCircleGrid(pos, maxRadius, this, maxBoidsToHandle, ref nearbyBoids, threadIndex);

        // call the base method
        BaseUpdateMultithread(dt);

        Profiler.EndSample();
    }

    /// <summary>
    /// Prepares the boid copying info so that we can use it multithreaded 
    /// </summary>
    //public virtual void PrepareForUpdate()
    //{
    //    pos = new Vector2(transform.position.x, transform.position.y);
    //    dir = new Vector2(transform.right.x, transform.right.y);
    //}

    /// <summary>
    /// This method should be called after the update of the boid has been done to synchronize the information calculated in the update to the boid's transform
    /// </summary>
    public virtual void SyncTransform()
    {
        transform.position = new Vector3(pos.x, pos.y, 0.0f);
        transform.right = new Vector3(dir.x, dir.y, 0.0f);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    protected void BaseUpdateMultithread(float dt)
    {
        Profiler.BeginSample("Boid Update Multithread");

        // generate the forces
        totalForce = new Vector2(0.0f, 0.0f);
        totalForce += UpdateCohesion();
        totalForce += UpdateAlignment();
        totalForce += UpdateSeparation();
        totalForce += UpdateBorderRepulsion();

        // apply the force to the velocity
        vel += totalForce * dt;

        // make sure the velocity is within the minimum and maximum
        float speed = vel.magnitude;
        if (speed < Mathf.Epsilon)
            vel = dir * minSpeed;
        else if (speed < minSpeed)
            vel *= minSpeed / speed;
        else if (speed > maxSpeed)
            vel *= maxSpeed / speed;

        // update the movement
        pos += vel * dt;

        // update the direction
        if (vel.sqrMagnitude > MinVelToUpdateDirSq)
            dir = vel.normalized;

        Profiler.EndSample();
    }

    #endregion
}
