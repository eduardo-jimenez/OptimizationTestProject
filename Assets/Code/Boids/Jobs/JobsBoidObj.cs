using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class JobsBoidObj : BaseBoid
{
    #region Public Attributes
    #endregion

    #region Private Attributes

    protected int index;
    protected new BoidsControllerJobs boidsCtrl;

    #endregion

    #region Properties

    public int Index => index;

    public new BoidsControllerJobs BoidsCtrl => boidsCtrl;

    #endregion

    #region BaseBoid Methods

    public virtual void Init(BoidsControllerJobs boidsCtrl, int index)
    {
        if (initialized)
            return;

        // set the parameters
        this.boidsCtrl = boidsCtrl;
        this.index = index;

        // reset some attributes
        vel = new Vector2(0.0f, 0.0f);

        // mark as initialized
        initialized = true;
    }

    #endregion

    #region Methods

    public override Bounds GetBounds()
    {
        return boidsCtrl.bounds;
    }

    public override List<BaseBoid> FindBoidsInCircleBruteForce(Vector2 pos, float alignmentRadius)
    {
        return boidsCtrl.FindBoidsInCircleBruteForce(pos, alignmentRadius, this);
    }

    #endregion
}
