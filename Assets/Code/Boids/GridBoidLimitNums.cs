using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This is a boid that uses the grid structure to speed up the finding of nearby boids but limiting the maximum number of boids we get
/// </summary>
public class GridBoidLimitNums : GridBoidSharedDist
{
    #region Public Attributes

    [Header("Limits")]
    public int maxBoidsToHandle = 20;

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
        Profiler.BeginSample("Find Nearest Boids in Biggest Radius");

        // update some params
        pos = Pos;

        // get all the boids that we need for the update methods
        float maxRadius = Mathf.Max(Mathf.Max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        boidsCtrl.FindNearestBoidsInCircleGrid(pos, maxRadius, this, maxBoidsToHandle, ref nearbyBoids);

        Profiler.EndSample();

        // call the base method
        BaseUpdate(dt);
    }

    #endregion

    #region Methods
    #endregion
}
