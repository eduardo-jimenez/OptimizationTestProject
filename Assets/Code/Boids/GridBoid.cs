using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This is a boid that uses the grid structure to speed up the finding of nearby boids
/// </summary>
public class GridBoid : BaseBoid
{
    #region Public Attributes
    #endregion

    #region Private Attributes

    private Vector2 pos = new Vector2(0.0f, 0.0f);
    private List<BaseBoid> nearbyBoids = new List<BaseBoid>();

    #endregion

    #region Properties
    #endregion

    #region BaseBoid Methods

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    /// <param name="dt"></param>
    public override void DoUpdate(float dt)
    {
        // update some params
        pos = Pos;

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

        // find all the boids within the cohesion radius
        boidsCtrl.FindBoidsInCircleGrid(pos, cohesionRadius, this, ref nearbyBoids);

        if (nearbyBoids.Count > 0)
        {
            // calculate the center of the boids around
            Vector2 flockCenter = new Vector2(0.0f, 0.0f);
            foreach (BaseBoid boid in nearbyBoids)
                flockCenter += boid.Pos;
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

        // find all the boids within the cohesion radius
        boidsCtrl.FindBoidsInCircleGrid(pos, maxSeparationRadius, this, ref nearbyBoids);

        if (nearbyBoids.Count > 0)
        {
            // go adding the forces to separate the boid from nearby boids
            foreach (BaseBoid boid in nearbyBoids)
            {
                // calculate the direction and distance to this boid
                Vector2 boidPos = boid.Pos;
                Vector2 dirToBoid = boidPos - pos;
                float distToBoid = dirToBoid.magnitude;

                // if at the exact same position we don't do calcs since they become unstable
                if (distToBoid > Mathf.Epsilon)
                {
                    // calculate the force to apply 
                    dirToBoid *= 1.0f / distToBoid;
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

        // find all the boids within the cohesion radius
        boidsCtrl.FindBoidsInCircleGrid(pos, alignmentRadius, this, ref nearbyBoids);

        if (nearbyBoids.Count > 0)
        {
            // average the direction of the nearby boids
            Vector2 avgDir = new Vector2(0.0f, 0.0f);
            foreach (BaseBoid boid in nearbyBoids)
                avgDir += boid.Dir;
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
