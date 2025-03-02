using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


/// <summary>
/// Structure holding the information of a boid for the jobs system
/// </summary>
public struct JobsBoid
{
    #region Public Attributes

    // Generic Params
    private float minSpeed;
    private float maxSpeed;

    // Cohesion
    private float cohesionRadius;
    private float maxCohesionForce;

    // Separation
    private float maxSeparationRadius;
    private float radiusForMaxSeparationForce;
    private float maxSeparationForce;

    // Alignment
    private float alignmentRadius;
    private float defaultAlignmentForce;
    private int numBoidsForMaxAligmentForce;

    // Border Repulsion
    private float distToStartRepulsion;
    private float distForMaxRepulsion;
    private float maxRepulsionForce;


    // State
    private bool initialized;
    private int index;

    private float2 pos;
    private float2 dir;
    private float2 vel;

    private float2 totalForce;
    private float2 cohesionForce;
    private float2 separationForce;
    private float2 alingmentForce;
    private float2 repulsionForce;

    #endregion

    #region Properties

    public bool Initialized => initialized;

    public float MinSpeed => minSpeed;
    public float MaxSpeed => maxSpeed;
    public float CohesionRadius => cohesionRadius;
    public float MaxCohesionForce => maxCohesionForce;
    public float MaxSeparationRadius => maxSeparationRadius;
    public float RadiusForMaxSeparationForce => radiusForMaxSeparationForce;
    public float MaxSeparationForce => maxSeparationForce;
    public float AlignmentRadius => alignmentRadius;
    public float DefaultAlignmentForce => defaultAlignmentForce;
    public int NumBoidsForMaxAlignmentForce => numBoidsForMaxAligmentForce;
    public float DistToStartRepulsion => distToStartRepulsion;
    public float DistForMaxRepulsion => distForMaxRepulsion;
    public float MaxRepulsionForce => maxRepulsionForce;

    public int Index => index;

    public float2 Vel
    {
        get => vel;
        set => vel = value;
    }
    public float2 Pos
    {
        get => pos;
        set => pos = value;
    }
    public float2 Dir
    {
        get => dir;
        set => dir = value;
    }
    public float2 TotalForce
    {
        get => totalForce;
        set => totalForce = value;
    }
    public float2 CohesionForce
    {
        get => cohesionForce;
        set => cohesionForce = value;
    }
    public float2 SeparationForce
    {
        get => separationForce;
        set => separationForce = value;
    }
    public float2 AlingmentForce
    {
        get => alingmentForce;
        set => alingmentForce = value;
    }
    public float2 RepulsionForce
    {
        get => repulsionForce;
        set => repulsionForce = value;
    }

    #endregion

    #region Initialization

    public void Init(BaseBoid boid, int index)
    {
        // assign the index
        this.index = index;

        // assign the public parameters of the boid first
        minSpeed = boid.minSpeed;
        maxSpeed = boid.maxSpeed;
        cohesionRadius = boid.cohesionRadius;
        maxCohesionForce = boid.maxCohesionForce;
        maxSeparationRadius = boid.maxSeparationRadius;
        radiusForMaxSeparationForce = boid.radiusForMaxSeparationForce;
        maxSeparationForce = boid.maxSeparationForce;
        alignmentRadius = boid.alignmentRadius;
        defaultAlignmentForce = boid.defaultAlignmentForce;
        numBoidsForMaxAligmentForce = boid.numBoidsForMaxAligmentForce;
        distToStartRepulsion = boid.distToStartRepulsion;
        distForMaxRepulsion = boid.distForMaxRepulsion;
        maxRepulsionForce = boid.maxRepulsionForce;

        // set the position and orientation
        pos = boid.Pos;
        dir = boid.Dir;
        vel = boid.Vel;

        // reset the state
        totalForce = new float2(0.0f, 0.0f);
        cohesionForce = new float2(0.0f, 0.0f);
        separationForce = new float2(0.0f, 0.0f);
        alingmentForce = new float2(0.0f, 0.0f);
        repulsionForce = new float2(0.0f, 0.0f);

        // mark as initialized
        initialized = true;
    }

    #endregion

    #region Methods
    #endregion
}
