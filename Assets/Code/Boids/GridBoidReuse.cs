using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This is a boid that uses the grid structure to speed up the finding of nearby boids
/// </summary>
public class GridBoidReuse : ReuseBoid
{
    #region Public Attributes
    #endregion

    #region Private Attributes
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
        Profiler.BeginSample("Get All Boids in Biggest Radius");

        // update some params
        pos = Pos;

        // get all the boids that we need for the update methods
        float maxRadius = Mathf.Max(Mathf.Max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        boidsCtrl.FindBoidsInCircleGrid(pos, maxRadius, this, ref nearbyBoids);

        Profiler.EndSample();

        // call the base method
        base.DoUpdate(dt);
    }

    #endregion

    #region Methods
    #endregion
}
