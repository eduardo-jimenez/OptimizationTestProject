using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


/// <summary>
/// The boids UI, showing the options to add, reset or change the boids
/// </summary>
public class BoidsUI : UI
{
    #region Public Attributes

    [Header("Boids UI")]
    public TextMeshProUGUI boidsInfoLabel;
    public TMP_Dropdown boidTypeComboBox;
	public TMP_InputField numBoidsInput;
	public Button addBoids;
	public Button clearBoids;

#if UNITY_EDITOR
    public TextMeshProUGUI boidsDebugInfoLabel;
#endif

    #endregion

    #region Private Attributes

    private BoidsController boidsCtrl;
    private int numBoidsInInfoString = -1;

    #endregion

    #region Properties
    #endregion

    #region MonoBehaviour Methods

    protected override void Update()
    {
        // call the base method
        base.Update();

        // update the boids info string only when necessary
        if (numBoidsInInfoString != boidsCtrl.NumBoids)
        {
            string boidsInfoStr = $"Num Boids = {boidsCtrl.NumBoids}";
            boidsInfoLabel.text = boidsInfoStr;
        }

#if UNITY_EDITOR
        boidsDebugInfoLabel.text = $"Avg Near Boids = {boidsCtrl.AvgNearbyBoids:0.0}";
#endif
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initialization
    /// </summary>
    public override void Init()
    {
        base.Init();

        // get the boids controller
        boidsCtrl = FindFirstObjectByType<BoidsController>();
        Assert.IsNotNull(boidsCtrl, "You need to have a valid boids controller if you have a boids UI!");

        // fill the boid types
        //...
    }

    /// <summary>
    /// Returns the number of boids to add based on the input
    /// </summary>
    private int GetNumBoidsToAdd()
    {
        int numBoids;

        string str = numBoidsInput.text;
        if (!int.TryParse(str, out numBoids))
            numBoids = 0;

        return numBoids;
    }

    /// <summary>
    /// Returns the boid type to add based on the combo box
    /// </summary>
    private BoidType GetCurrBoidType()
    {
        int comboBoxValue = boidTypeComboBox.value;
        BoidType boidType = (BoidType)comboBoxValue;

        return boidType;
    }

    #endregion

    #region Callbacks

    public void OnAddBoids()
    {
        int numBoids = GetNumBoidsToAdd();
        BoidType boidType = GetCurrBoidType();
        boidsCtrl.AddBoids(numBoids, boidType);
    }

    public void OnClearBoids()
    {
        boidsCtrl.ClearBoids();
    }

    #endregion
}
