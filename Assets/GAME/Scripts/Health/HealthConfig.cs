using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Конфигурация системы здоровья: максимальные HP по частям тела + параметры
/// передачи урона при разрушении конечности. Дефолтные значения — канон EFT.
///
/// Создать ассет: Assets → Create → Inventory → Health Config.
/// </summary>
[CreateAssetMenu(fileName = "HealthConfig", menuName = "Inventory/Health Config")]
public class HealthConfig : ScriptableObject
{
    [Header("Max HP per body part")]
    public float maxHpHead     = 35f;
    public float maxHpChest    = 85f;
    public float maxHpStomach  = 70f;
    public float maxHpLeftArm  = 60f;
    public float maxHpRightArm = 60f;
    public float maxHpLeftLeg  = 65f;
    public float maxHpRightLeg = 65f;

    [Header("Damage spread")]
    [Tooltip("Множитель overflow-урона, который уходит на другие части при разрушении конечности " +
             "и при последующих попаданиях по уже разрушенной конечности. " +
             "0.15 = 15% (по умолчанию, по описанию ТЗ).")]
    [Range(0f, 1f)] public float damageSpreadMultiplier = 0.15f;

    [Tooltip("Целевые части тела, в которые распределяется overflow-урон (равными долями). " +
             "По умолчанию — только грудь.")]
    public List<BodyPartType> spreadTargets = new() { BodyPartType.Chest };

    [Header("Critical parts (death on HP = 0)")]
    [Tooltip("Если HP любой из этих частей опускается до 0 — персонаж мёртв.")]
    public List<BodyPartType> criticalParts = new() { BodyPartType.Head, BodyPartType.Chest };

    [Header("Surgical kit defaults")]
    [Tooltip("Сколько HP получает разрушенная конечность после применения хирургического набора. " +
             "Используется как значение по умолчанию, если в ConsumableEffect не задано иное.")]
    public float defaultSurgicalKitRestoreHp = 30f;

    public float GetMaxHp(BodyPartType part) => part switch
    {
        BodyPartType.Head     => maxHpHead,
        BodyPartType.Chest    => maxHpChest,
        BodyPartType.Stomach  => maxHpStomach,
        BodyPartType.LeftArm  => maxHpLeftArm,
        BodyPartType.RightArm => maxHpRightArm,
        BodyPartType.LeftLeg  => maxHpLeftLeg,
        BodyPartType.RightLeg => maxHpRightLeg,
        _ => 0f
    };

    public bool IsCritical(BodyPartType part) =>
        criticalParts != null && criticalParts.Contains(part);

    private void OnValidate()
    {
        maxHpHead     = Mathf.Max(1f, maxHpHead);
        maxHpChest    = Mathf.Max(1f, maxHpChest);
        maxHpStomach  = Mathf.Max(1f, maxHpStomach);
        maxHpLeftArm  = Mathf.Max(1f, maxHpLeftArm);
        maxHpRightArm = Mathf.Max(1f, maxHpRightArm);
        maxHpLeftLeg  = Mathf.Max(1f, maxHpLeftLeg);
        maxHpRightLeg = Mathf.Max(1f, maxHpRightLeg);

        if (criticalParts == null || criticalParts.Count == 0)
            criticalParts = new List<BodyPartType> { BodyPartType.Head, BodyPartType.Chest };

        if (spreadTargets == null)
            spreadTargets = new List<BodyPartType>();

        defaultSurgicalKitRestoreHp = Mathf.Max(0f, defaultSurgicalKitRestoreHp);
    }
}
