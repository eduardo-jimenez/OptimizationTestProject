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
    private BoidsControllerJobs boidsJobsCtrl;
    private int numBoidsInInfoString = -1;

    #endregion

    #region Properties
    #endregion

    #region MonoBehaviour Methods

    protected override void Update()
    {
        // call the base method
        base.Update();

        if (boidsCtrl != null)
        {
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
        else if (boidsJobsCtrl != null)
        {
            // update the boids info string only when necessary
            if (numBoidsInInfoString != boidsJobsCtrl.NumBoids)
            {
                string boidsInfoStr = $"Num Boids = {boidsJobsCtrl.NumBoids}";
                boidsInfoLabel.text = boidsInfoStr;
            }
        }
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
        boidsJobsCtrl = FindFirstObjectByType<BoidsControllerJobs>();
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
        if (boidsCtrl != null)
        {
            BoidType boidType = GetCurrBoidType();
            boidsCtrl.AddBoids(numBoids, boidType);
        }
        else if (boidsJobsCtrl != null)
        {
            boidsJobsCtrl.AddBoids(numBoids);
        }
    }

    public void OnClearBoids()
    {
        if (boidsCtrl != null)
            boidsCtrl.ClearBoids();
        else if (boidsJobsCtrl != null)
            boidsJobsCtrl.ClearBoids();
    }

    #endregion
}
