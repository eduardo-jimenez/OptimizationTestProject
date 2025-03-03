using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class JobsBoidObj : BaseBoid
{
    #region Public Attributes

    [Header("Optimization Parameters")]
    public int maxBoidsToHandle = 10;

    #endregion

    #region Private Attributes

    protected int index;
    protected BoidsControllerJobs jobBoidsCtrl;

    #endregion

    #region Properties

    public int Index => index;

    //public new BoidsControllerJobs BoidsCtrl => boidsCtrl;

    #endregion

    #region BaseBoid Methods

    public virtual void Init(BoidsControllerJobs jobBoidsCtrl, int index)
    {
        if (initialized)
            return;

        // set the parameters
        this.jobBoidsCtrl = jobBoidsCtrl;
        this.index = index;

        // reset some attributes
        vel = new Vector2(0.0f, 0.0f);

        // mark as initialized
        initialized = true;
    }

    public override Bounds GetBounds()
    {
        return jobBoidsCtrl.bounds;
    }

    #endregion

    #region Methods

    public void UpdateFromJob(JobsBoid jobsBoid)
    {
        Pos = jobsBoid.Pos;
        vel = jobsBoid.Vel;
        Dir = jobsBoid.Dir;

#if UNITY_EDITOR
        totalForce = jobsBoid.TotalForce;
        cohesionForce = jobsBoid.CohesionForce;
        separationForce = jobsBoid.SeparationForce;
        alignmentForce = jobsBoid.AlignmentForce;
        repulsionForce = jobsBoid.RepulsionForce;
#endif
    }

    #endregion
}
