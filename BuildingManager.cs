using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Contains a building object and manages more Unity-specific functions
/// </summary>
public class BuildingManager : UnitManager
{
    const float BUILDING_LEVEL_THRESHOLD = 1f;

    public Building building = null;
    public GameObject AttackRangeIndicator;
    public GameObject crossFolder;
    public GameObject crosshair;
    [SerializeField] bool useConstructionHealthbar = true;

    [HideInInspector] public BuildingSoundManager buildingSoundManager;

    public override Unit Unit
    {
        get { return building; }
        set { building = value is Building building1 ? building1 : null; }
    }

    private int nCollisions = 0;

    protected override void Awake()
    {
        base.Awake();

        // Buildings have wider healthbars than units
        healthbarRenderer.GetPropertyBlock(HealthbarMatBlock);
        HealthbarMatBlock.SetFloat("_Width", 5f);
        healthbarRenderer.SetPropertyBlock(HealthbarMatBlock);

        buildingSoundManager = unitSoundManager as BuildingSoundManager;

        if (crosshair == null) return;
        crossFolder = GameObject.Find("Crosshair");

        if (crossFolder == null ) {
            crossFolder = new GameObject("Crosshair");
        }
    }

    protected override void Update()
    {
        base.Update();
        if (Input.GetMouseButtonDown(1))
        {
            if (!selected || crosshair == null || crossFolder == null || EventSystem.current.IsPointerOverGameObject()) return;
            foreach (Transform cross in crossFolder.transform)
            {
                Destroy(cross.gameObject);
            }

            Ray ray;
            RaycastHit raycastHit;
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out raycastHit, Globals.MAX_RAY_DIST, Globals.TERRAIN_LAYER_MASK))
            {
                GameObject newCross = Instantiate(crosshair, raycastHit.point, Quaternion.identity);
                newCross.transform.parent = crossFolder.transform;
            }
        }
    }

    protected override void PlaceThisOnStart()
    {
        building = new Building((BuildingData)unitData, owner, transform);

        building.Place();
        building.SetConstructionRatio(1);
    }

    /// <summary>
    /// A building is active if it is complete
    /// </summary>
    public override bool IsActive()
    {
        return building.IsComplete;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Terrain")) return;
        nCollisions++;
        CheckValidLocation();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Terrain")) return;
        nCollisions--;
        CheckValidLocation();
    }

    /// <summary>
    /// Sets the building's materials based on if the placement is valid
    /// </summary>
    public bool CheckValidLocation()
    {
        if (building == null) return false;
        if (building.IsFixed) return false;

        return HasValidPlacement();
    }

    public BuildingPlacement GetPlacement()
    {
        bool isValidPlacement = HasValidPlacement();
        if (!isValidPlacement)
        {
            building.SetMaterials(BuildingPlacement.INVALID);
            return BuildingPlacement.INVALID;
        }
        else if (!InRangeOfOutposts() && this is not AnchorBuildingManager)
        {
            building.SetMaterials(BuildingPlacement.OUT_OF_RANGE);
            return BuildingPlacement.OUT_OF_RANGE;
        }
        else
        {
            building.SetMaterials(BuildingPlacement.VALID);
            return BuildingPlacement.VALID;
        }
    }

    /// <summary>
    /// Checks if there are any obstructions or slopes
    /// </summary>
    public bool HasValidPlacement()
    {
        // Check for collisions with anything non-terrain
        if (nCollisions > 0) return false;

        // Check if we're placing inside fog of war
        if (!PosInView.Instance.IsInView(transform.position)) return false;

        // Check for steepness
        BoxCollider boxCollider = (BoxCollider)coll;

        Vector3 p = transform.position;
        Vector3 c = boxCollider.center;
        Vector3 e = boxCollider.size / 2f;

        float bottomHeight = c.y - e.y + 0.5f;
        Vector3[] bottomCorners = new Vector3[]
        {
            new Vector3(c.x-e.x, bottomHeight, c.z-e.z),
            new Vector3(c.x-e.x, bottomHeight, c.z+e.z),
            new Vector3(c.x+e.x, bottomHeight, c.z-e.z),
            new Vector3(c.x+e.x, bottomHeight, c.z+e.z),
        };
        // Cast short ray underneath. All four corners must be level enough
        int nInvalidCorners = 0;
        foreach (var corner in bottomCorners)
        {
            if (!Physics.Raycast(
                    p + corner,
                    Vector3.down,
                    BUILDING_LEVEL_THRESHOLD,
                    Globals.TERRAIN_LAYER_MASK
               ))
            {
                //return false;
                nInvalidCorners++;
            }
        }
        //return true;
        return nInvalidCorners <= 2;
    }

    public void PostCompleteInit()
    {
        StartCoroutine(PostCompleteInitCR());
    }

    protected virtual IEnumerator PostCompleteInitCR()
    {
        // Wait one frame so all outposts are fully initialized
        yield return null;

        // Perform decay check after the building is complete
        if (building.buildingData.canDecay)
        {
            building.CheckForDecay();
        } 
        
        if (AttackRangeIndicator != null){
            AttackRangeIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Select with a single click
    /// </summary>
    ///
    protected override void SelectUtil()
    {
        base.SelectUtil();
        if (crosshair == null) return;
        foreach (Transform cross in crossFolder.transform)
        {
            foreach (Transform sprite in crossFolder.transform)
            {
                sprite.gameObject.SetActive(true);
            }
        }
    }

    public override void Deselect()
    {
        base.Deselect();
        if (crosshair == null) return;
        foreach (Transform cross in crossFolder.transform)
        {
            foreach (Transform sprite in crossFolder.transform)
            {
                sprite.gameObject.SetActive(false);
            }
        }
    }

    protected override void SelectSingleClickAdditive()
    {
        if (!GameManager.Instance.selectedUnits.Contains(this))
        {
            UnitSelection.DeselectAll();
            SelectUtil();
        }
        else
        {
            Deselect();
        }
    }

    /// <summary>
    /// Take damage from an attacker
    /// </summary>
    public override void TakeDamage(int damage, UnitManager attacker)
    {
        // With no siblings, take the full amount of damage
        if (building.constructionSiblings.Count == 0)
        {
            TakeDamageDirect(damage, attacker);
        }
        // Deal damage to siblings as well
        // Make sure every building is dealt the same fraction of damage
        else
        {
            float newHealthRatio = (Unit.currentHealth / (float)Unit.MaxHP) - (damage / (float)building.totalSiblingHealth);
            int dividedDamage;

            // We use ToList() to stop null references if a building becomes null in the middle of the loop
            foreach (Building sibling in building.constructionSiblings.ToList())
            {
                dividedDamage = Mathf.CeilToInt(sibling.currentHealth - (newHealthRatio * sibling.MaxHP));
                sibling.buildingManager.TakeDamageDirect(dividedDamage, attacker);
            }

            // Deal damage to this
            dividedDamage = Mathf.CeilToInt(Unit.currentHealth - (newHealthRatio * Unit.MaxHP));
            TakeDamageDirect(dividedDamage, attacker);
        }
        
    }

    /// <summary>
    /// Take damage, bypassing siblings
    /// </summary>
    void TakeDamageDirect(int damage, UnitManager attacker)
    {
        base.TakeDamage(damage, attacker);
        building.SetConstructionRatio(Unit.currentHealth / (float)Unit.MaxHP, wasFromAttack: true);
    }

    /// <summary>
    /// Update Healthbar UI. While being constructed, the healthbar shows the construction ratio. At full health, disappears.
    /// </summary>
    public override void UpdateHealthbar()
    {
        // Normal health for complete building
        if (Unit.IsComplete)
        {
            base.UpdateHealthbar();
        }
        // Construction ratio for incomplete building
        else
        {
            if (useConstructionHealthbar)
            {
                healthbar.SetActive(true);
                healthbarRenderer.GetPropertyBlock(HealthbarMatBlock);
                HealthbarMatBlock.SetFloat("_Health", building.ConstructionRatio);
                healthbarRenderer.SetPropertyBlock(HealthbarMatBlock);
            }
        }
    }

    /// <summary>
    /// Remove this from siblings when dead
    /// </summary>
    protected override void PreDie()
    {
        base.PreDie();

        // Fire BuildingDestroyedEvent
        EventBus.Publish(new BuildingDestroyedEvent(this));

        // Award skill points if any
        SkillPointSystem.Instance.AddPoints(building.buildingData.skillPoints);

        // Remove this from siblings and recalculate construction ratio
        foreach (var sibling in building.constructionSiblings)
        {
            float prevRatio = sibling.ConstructionRatio;
            sibling.constructionNeeded -= ((BuildingData)building.Data).constructionNeeded;
            sibling.SetConstructionRatio(prevRatio);

            sibling.constructionSiblings.Remove(building);
        }

        // Lose population cap
        if (IsMyUnit)
        {
            GameManager.Instance.populationCap -= building.buildingData.populationCostGranted;
        }
    }

    /// <summary>
    /// Checks if building is near outpost or HQ
    /// </summary>
    public bool InRangeOfOutposts()
    {
        for (int i = 0; i < GameManager.Instance.allOutposts.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, GameManager.Instance.allOutposts[i].transform.position);

            float range = GameManager.Instance.allOutposts[i].building.buildingData.SafetyRange;
            if (distance <= range)
            {
                return true; // In range of an outpost or HQ
            }
        }

        return false; // No outpost nearby
    }

    /// <summary>
    /// decays building over time. 
    /// </summary>
    public IEnumerator Decay()
    {

        float totalHealth = building.buildingData.health;
        float decayDuration = 15f; 
        float decayAmountPerSecond = totalHealth / decayDuration;
        float accumulatedDamage = 0f;

        while (Unit.currentHealth > 0)
        {
            float damageThisFrame = decayAmountPerSecond * Time.deltaTime;

            accumulatedDamage += damageThisFrame;

            if (accumulatedDamage >= 1f)
            {
                int damageToApply = Mathf.FloorToInt(accumulatedDamage);
                accumulatedDamage -= damageToApply;
                TakeDamage(damageToApply, null);
            }
            if (Unit.currentHealth <= 0)
            {
                yield break;
            }

            yield return null;
        }
    }


    /// <summary>
    /// Fires when a building is destroyed
    /// </summary>
    public class BuildingDestroyedEvent
    {
        public BuildingManager buildingManager;

        public BuildingDestroyedEvent(BuildingManager destroyedBuilding)
        {
            this.buildingManager = destroyedBuilding;
        }
    }
}
