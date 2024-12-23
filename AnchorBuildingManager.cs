using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AnchorBuildingManager : BuildingManager
{
    public GameObject RangeIndicator;
    public float healingInterval = 3f; // Time in seconds between healing checks
    public int healingAmount = 2; // Amount healed per interval

    private float healingRange;

    protected override void Start()
    {
        base.Start();

        float rangeSize = building.buildingData.SafetyRange * 2 * 1.05f;
        healingRange = building.buildingData.SafetyRange;
        Unit.ConfigureSelectionCircle(rangeSize, RangeIndicator.GetComponent<DecalProjector>(), GameManager.Instance.buildRangeColor, 15);

        StartCoroutine(HealingRoutine());
    }


    /// <summary>
    /// Couroutine that continuously does healing checks
    /// </summary>
    private IEnumerator HealingRoutine()
    {
        yield return new WaitUntil(() => building.IsComplete);

        while (true)
        {
            if (GameManager.Instance.DayPhase == DayPhase.Day) HealFriendliesInRange();
            yield return new WaitForSeconds(healingInterval); // Wait before the next check
        }
    }


    /// <summary>
    /// Enables and Disables the Range indicator
    /// </summary>
    private void HealFriendliesInRange()
    {
        // Detect all units in range
        Collider[] colliders = Physics.OverlapSphere(transform.position, healingRange, Globals.UNITS_LAYER_MASK);
        //Debug.Log($"Number of colliders detected: {colliders.Length}");

        List<EntityManager> friendliesInRange = new List<EntityManager>();

        foreach (var collider in colliders)
        {
            // Check if the unit is a friendly
            if (collider.TryGetComponent(out EntityManager entity))
            {
                // Null check for entity and entityData
                if (entity.entity == null || entity.entity.entityData == null)
                {
                    continue;
                }

                // If an enemy is found, don't heal anyone
                if (!entity.IsMyUnit)
                {
                    return;
                }
                else
                {
                    // Check if the unit needs healing
                    if (entity.entity.currentHealth < entity.entity.entityData.health)
                    {
                        friendliesInRange.Add(entity);
                    }
                }
            }
        }

        // Heal friendlies if no enemies are in range
        foreach (var friendly in friendliesInRange)
        {
            friendly.Heal(healingAmount);
        }
    }

    /// <summary>
    /// Enables and Disables the Range indicator
    /// </summary>
    public void TurnRangeIndicator(bool value)
    {
        RangeIndicator.SetActive(value);
    }

    /// <summary>
    /// Remove this from siblings when dead
    /// </summary>
    protected override void PreDie()
    {
        base.PreDie();

        GameManager.Instance.allOutposts.Remove(this);
    }

    protected override IEnumerator PostCompleteInitCR()
    {
        GameManager.Instance.allOutposts.Add(this);

        yield return base.PostCompleteInitCR();
    }
}
