using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This is a boid that uses the grid structure to speed up the finding of nearby boids but limiting the maximum number of boids we get
/// </summary>
public class GridBoidSharedDist : BaseBoid
{
    #region Public Attributes
    #endregion

    #region Private Attributes

    protected Vector2 pos = new Vector2(0.0f, 0.0f);
    protected List<(BaseBoid, float)> nearbyBoids = new List<(BaseBoid, float)>();

    #endregion

    #region Properties

    public List<(BaseBoid, float)> NearbyBoids => nearbyBoids;
    public override int NumNearbyBoids => nearbyBoids.Count;

    #endregion

    #region BaseBoid Methods

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    /// <param name="dt"></param>
    public override void DoUpdate(float dt)
    {
        Profiler.BeginSample("Get All Boids in Biggest Radius");

        // update some params
        pos = Pos;

        // get all the boids that we need for the update methods
        float maxRadius = Mathf.Max(Mathf.Max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        boidsCtrl.FindBoidsInCircleGrid(pos, maxRadius, this, ref nearbyBoids);

        Profiler.EndSample();

        // call the base method
        BaseUpdate(dt);
    }

    /// <summary>
    /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
    /// </summary>
    protected override Vector2 UpdateCohesion()
    {
        Profiler.BeginSample("UpdateCohesion");

        cohesionForce = new Vector2(0.0f, 0.0f);

        if (nearbyBoids.Count > 0)
        {
            // calculate the center of the boids around
            Vector2 flockCenter = new Vector2(0.0f, 0.0f);
            foreach ((BaseBoid boid, float dist) boidInfo in nearbyBoids)
            {
                if (boidInfo.dist <= cohesionRadius)
                    flockCenter += boidInfo.boid.Pos;
            }
            flockCenter *= 1.0f / (float)nearbyBoids.Count;

            // create a force towards the flock center
            Vector2 dirToCenter = flockCenter - pos;
            if (dirToCenter.magnitude > 1.0f)
                dirToCenter.Normalize();
            cohesionForce = dirToCenter * maxCohesionForce;
        }

        Profiler.EndSample();

        return cohesionForce;
    }

    /// <summary>
    /// Returns a force to keep boids from colliding with each other
    /// </summary>
    protected override Vector2 UpdateSeparation()
    {
        Profiler.BeginSample("UpdateSeparation");

        separationForce = new Vector2(0.0f, 0.0f);

        if (nearbyBoids.Count > 0)
        {
            // go adding the forces to separate the boid from nearby boids
            foreach ((BaseBoid boid, float dist) boidInfo in nearbyBoids)
            {
                // if at the exact same position we don't do calcs since they become unstable
                float distToBoid = boidInfo.dist;
                if (distToBoid > Mathf.Epsilon && distToBoid < maxSeparationRadius)
                {
                    // calculate the direction and distance to this boid
                    Vector2 boidPos = boidInfo.boid.Pos;
                    Vector2 dirToBoid = (boidPos - pos).normalized;

                    // calculate the force to apply 
                    float forceT = 1.0f - Mathf.Clamp01((distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce));
                    float forceAmount = maxSeparationForce * forceT;
                    Vector2 force = -dirToBoid * forceAmount;

                    // add it to the total amount
                    separationForce += force;
                }
            }
        }

        Profiler.EndSample();

        return separationForce;
    }

    /// <summary>
    /// Returns a force to try to get all the boids looking in the same direction
    /// </summary>
    protected override Vector2 UpdateAlignment()
    {
        Profiler.BeginSample("UpdateAlignment");

        if (nearbyBoids.Count > 0)
        {
            // average the direction of the nearby boids
            Vector2 avgDir = new Vector2(0.0f, 0.0f);
            foreach ((BaseBoid boid, float dist) boidInfo in nearbyBoids)
            {
                if (boidInfo.dist < alignmentRadius)
                    avgDir += boidInfo.boid.Dir;
            }
            avgDir.Normalize();

            // add a force in that direction
            alingmentForce = avgDir * defaultAlignmentForce;
        }

        Profiler.EndSample();

        return alingmentForce;
    }

    #endregion

    #region Methods
    #endregion
}
