using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A singleton class used to display a building "ghost" and place it.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    // Singleton
    public static BuildingPlacer Instance;

    // Serialized Fields
    [SerializeField] float rotationThreshold = 10f; // pixels

    // Public properties
    public bool justCanceled {get; protected set; } = false;
    public bool rotatingBuilding { get; protected set; } = false;

    // Private/protected variables
    protected EntityManager builder = null;
    Building placingBuilding = null;
    Ray ray;
    RaycastHit raycastHit;
    Vector3 lastPlacementPosition = Vector3.zero;
    Vector3 rotationStartPos;
    bool wentPastThreshold = false; // Went past rotation threshold

    private void Awake()
    {
        Instance = this;

        EventBus.Subscribe<UnitDestroyedEvent>(OnUnitKilled);
    }

    void Update()
    {
        justCanceled = false;

        // Are we currently placing a building?
        if (placingBuilding == null)
        {
            return;
        }

        // Cancel placing on right click
        if (Input.GetMouseButtonUp(1))
        {
            CancelPlacedBuilding();
            return;
        }

        // Update the phantom building location to the mouse if we aren't rotating
        if (!rotatingBuilding)
        {
            UpdateBuildingMouseLocation();
        }
        else
        {
            // Detect if we moved the mouse far enough
            if (!wentPastThreshold)
            {
                Vector3 delta = rotationStartPos - Input.mousePosition;
                if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > rotationThreshold)
                {
                    wentPastThreshold = true;
                }
            }

            // Start rotation if we want far enough
            if (wentPastThreshold)
            {
                UpdateBuildingRotation();
            }
        }

        // Left click and drag rotates building
        if (Input.GetMouseButtonDown(0)
            && placingBuilding.HasValidPlacement
            && !EventSystem.current.IsPointerOverGameObject()
            && placingBuilding.CanBuy)
        {
            rotatingBuilding = true;
            rotationStartPos = Input.mousePosition;
            wentPastThreshold = false;
        }

        // Try to place building
        if (rotatingBuilding
            && Input.GetMouseButtonUp(0)
            && placingBuilding.HasValidPlacement
            && !EventSystem.current.IsPointerOverGameObject()
            && placingBuilding.CanBuy)
        {
            PlaceBuilding();
        }
    }

    /// <summary>
    /// Updates the phantom building's location to the current mouse position
    /// </summary>
    protected void UpdateBuildingMouseLocation()
    {
        // Get building position
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out raycastHit, Globals.MAX_RAY_DIST, Globals.TERRAIN_LAYER_MASK))
        {
            placingBuilding.SetPosition(raycastHit.point);
            lastPlacementPosition = raycastHit.point;
            placingBuilding.CheckValidPlacement();
        }
        placingBuilding.SetMaterials();
    }

    /// <summary>
    /// Updates the building's rotation based on the mouse position
    /// </summary>
    void UpdateBuildingRotation()
    {
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out raycastHit, Globals.MAX_RAY_DIST, Globals.TERRAIN_LAYER_MASK))
        {
            float yRot = Vector3.SignedAngle(Vector3.right, raycastHit.point - lastPlacementPosition, Vector3.up);
            placingBuilding.SetRotation(yRot);
            placingBuilding.CheckValidPlacement();
        }
    }

    /// <summary>
    /// Starts the phantom building mode with the specified building index
    /// </summary>
    public void PreparePlacedBuilding(int buildingDataIdx)
    {
        PreparePlacedBuilding(Globals.BUILDING_DATA[buildingDataIdx]);
    }

    /// <summary>
    /// Initialize a phantom building, destroy the previous one
    /// </summary>
    public void PreparePlacedBuilding(BuildingData buildingData, EntityManager builder = null)
    {
        // Destroy previous phantom building
       ShowRangeIndicators(true);

        if (placingBuilding != null && !placingBuilding.IsFixed)
        {
            Destroy(placingBuilding.Transform.gameObject);
        }

        placingBuilding = new Building(buildingData, GameManager.Instance.gameParams.myPlayerId);
        if (builder != null) this.builder = builder;
        UpdateBuildingMouseLocation();

        if (placingBuilding.buildingData.attackRange > 0 && placingBuilding.buildingManager.AttackRangeIndicator != null)
        {
            float rangeSize = placingBuilding.buildingData.attackRange * 2 * 1.05f;
            Unit.ConfigureSelectionCircle(
                rangeSize,
                placingBuilding.buildingManager.AttackRangeIndicator.GetComponent<DecalProjector>(),
                GameManager.Instance.attackRangeColor,
                15);
            placingBuilding.buildingManager.AttackRangeIndicator.SetActive(true);
        }
    }

    /// <summary>
    /// Places the current phantom building
    /// </summary>
    protected void PlaceBuilding()
    {
        if (!placingBuilding.CanBuy) return;

        if (GameManager.Instance.requireResources) GameManager.Instance.RemoveResources(placingBuilding.Data.cost);
        placingBuilding.Place();
        placingBuilding.Transform.GetComponent<BuildingManager>().UpdateHealthbar();

        if (placingBuilding.buildingManager.buildingSoundManager)
        {
            placingBuilding.buildingManager.buildingSoundManager.PlaceAudio();
        }

        if (builder != null)
        {
            builder.entity.QueueTaskFromRightClick(TaskType.BuildRepair, placingBuilding.Transform.gameObject, null);
            builder = null;
        }
        placingBuilding = null;
        rotatingBuilding = false;

        ShowRangeIndicators(false);
    }

    /// <summary>
    /// Stop placing a building, destroy the current phantom building
    /// </summary>
    protected void CancelPlacedBuilding()
    {
        Destroy(placingBuilding.Transform.gameObject);
        placingBuilding = null;
        builder = null;
        rotatingBuilding = false;   
        justCanceled = true;
        ShowRangeIndicators(false);
    }

    /// <summary>
    /// Programmatically place a building
    /// </summary>
    public Building SpawnBuilding(BuildingData data, int owner, Vector3 position, float yRot)
    {
        // Instantiate building
        Building newBuilding = new Building(data, owner);
        newBuilding.SetPosition(position);
        newBuilding.SetRotation(yRot);

        // Shuffle around variables
        Building prevPlacingBuilding = placingBuilding;
        placingBuilding = newBuilding;
        PlaceBuilding();
        placingBuilding = prevPlacingBuilding;

        return newBuilding;
    }

    /// <summary>
    /// Cancels the placing building if the builder just died
    /// </summary>
    void OnUnitKilled(UnitDestroyedEvent e)
    {
        if (e.unitManager == builder)
        {
            CancelPlacedBuilding();
        }
    }

    private void SetAllRenderersToColor(Building building, Color color)
    {
        foreach (var renderer in building.buildingManager.renderedMesh.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.color = color;
        }
    }

    /// <summary>
    /// Enables and Disables anchor buidling Range indicators
    /// </summary>
    void ShowRangeIndicators(bool value)
    {
        for (int i = 0; i < GameManager.Instance.allOutposts.Count; i++)
        {
            GameManager.Instance.allOutposts[i].TurnRangeIndicator(value);
        }
    }
}
