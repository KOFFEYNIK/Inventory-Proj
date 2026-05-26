using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tarkov-подобная система здоровья: 7 частей тела с независимыми HP.
///
/// Правила:
/// - Голова и грудь (HealthConfig.criticalParts) — критические. HP = 0 → смерть, все остальные обнуляются.
/// - Конечности (живот, руки, ноги) при разрушении не убивают, но "чернеют": HP = 0 и помечается Destroyed.
/// - Overflow при разрушении конечности × damageSpreadMultiplier → распределяется поровну по spreadTargets.
/// - Любой урон по уже разрушенной конечности × damageSpreadMultiplier → также уходит в spreadTargets.
/// - Разрушенную конечность можно восстановить только хирургическим набором (RestoreLimb).
///
/// Подвеска: компонент-синглтон. Положить на любой GameObject (рекомендую тот же, что и HungerSystem/ThirstSystem).
/// Поле <see cref="config"/> обязательное — назначь HealthConfig SO в инспекторе или будет создан дефолтный.
/// </summary>
[DefaultExecutionOrder(-90)]
public class HealthSystem : MonoBehaviour, IHealthTarget, IPlayerModule
{
    public static HealthSystem Instance { get; private set; }

    public string ModuleName => "Health";

    // ── IHealthTarget ────────────────────────────────────────────────────────
    public bool IsAlive => !IsDead;

    public float DefaultSurgicalKitRestoreHp =>
        config != null ? config.defaultSurgicalKitRestoreHp : 30f;

    public bool TryRestoreFirstDestroyed(float restoreHp)
    {
        if (IsDead) return false;
        if (!TryGetFirstDestroyed(out var part)) return false;
        return RestoreLimb(part, restoreHp);
    }

    public bool TryHealAnyWoundedLimb(float amount)
    {
        if (IsDead || amount <= 0f) return false;
        foreach (var p in AllParts())
        {
            if (IsDestroyed(p)) continue;
            if (GetHp(p) >= GetMaxHp(p)) continue;
            if (HealPart(p, amount)) return true;
        }
        return false;
    }

    [Header("Config")]
    public HealthConfig config;

    public event Action OnHealthChanged;
    public event Action OnDeath;

    public bool IsDead { get; private set; }

    private readonly Dictionary<BodyPartType, float> currentHp  = new();
    private readonly HashSet<BodyPartType>           destroyed  = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null)
        {
            Debug.LogWarning("[HealthSystem] HealthConfig не назначен — создан дефолтный (без сохранения).");
            config = ScriptableObject.CreateInstance<HealthConfig>();
        }

        ResetFull();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Read API ─────────────────────────────────────────────────────────────

    public float GetHp(BodyPartType part)        => currentHp.TryGetValue(part, out var v) ? v : 0f;
    public float GetMaxHp(BodyPartType part)     => config != null ? config.GetMaxHp(part) : 0f;
    public bool  IsDestroyed(BodyPartType part)  => destroyed.Contains(part);

    public float GetNormalized(BodyPartType part)
    {
        float max = GetMaxHp(part);
        return max > 0f ? Mathf.Clamp01(GetHp(part) / max) : 0f;
    }

    /// <summary>Перечисляет все 7 частей в каноническом порядке enum.</summary>
    public static IEnumerable<BodyPartType> AllParts()
    {
        yield return BodyPartType.Head;
        yield return BodyPartType.Chest;
        yield return BodyPartType.Stomach;
        yield return BodyPartType.LeftArm;
        yield return BodyPartType.RightArm;
        yield return BodyPartType.LeftLeg;
        yield return BodyPartType.RightLeg;
    }

    // ── Write API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Нанести урон по конкретной части тела. Если часть уже разрушена — урон
    /// автоматически распределяется по spreadTargets с коэффициентом damageSpreadMultiplier.
    /// </summary>
    public void ApplyDamage(BodyPartType part, float damage)
    {
        if (IsDead || damage <= 0f) return;

        bool changed = ApplyDamageInternal(part, damage, depth: 0);
        if (changed) OnHealthChanged?.Invoke();
    }

    /// <summary>
    /// Полное восстановление: HP всех частей до max, снять разрушения, отменить смерть.
    /// Используется при респауне.
    /// </summary>
    public void ResetFull()
    {
        currentHp.Clear();
        destroyed.Clear();
        IsDead = false;
        foreach (var p in AllParts())
            currentHp[p] = config.GetMaxHp(p);
        OnHealthChanged?.Invoke();
    }

    /// <summary>
    /// Восстановить разрушенную конечность ("чёрная" → "красная"). Это единственный путь
    /// вернуть разрушенную конечность в работу — через хирургический набор.
    /// Возвращает true, если действие применилось (часть была разрушена и сейчас живая).
    /// </summary>
    public bool RestoreLimb(BodyPartType part, float restoreHp)
    {
        if (IsDead) return false;
        if (!destroyed.Contains(part)) return false;

        destroyed.Remove(part);
        float clamped = Mathf.Clamp(restoreHp, 1f, GetMaxHp(part));
        currentHp[part] = clamped;
        OnHealthChanged?.Invoke();
        return true;
    }

    /// <summary>Лечить живую часть (для будущих медицинских предметов).</summary>
    public bool HealPart(BodyPartType part, float amount)
    {
        if (IsDead || amount <= 0f) return false;
        if (destroyed.Contains(part)) return false; // разрушенные лечатся только хирургическим набором

        float max = GetMaxHp(part);
        float prev = GetHp(part);
        if (prev >= max) return false;
        currentHp[part] = Mathf.Min(max, prev + amount);
        OnHealthChanged?.Invoke();
        return true;
    }

    /// <summary>Найти первую разрушенную часть (для контекстного меню хирургического набора).</summary>
    public bool TryGetFirstDestroyed(out BodyPartType part)
    {
        foreach (var p in AllParts())
        {
            if (destroyed.Contains(p)) { part = p; return true; }
        }
        part = default;
        return false;
    }

    public bool HasAnyDestroyed()
    {
        foreach (var p in AllParts())
            if (destroyed.Contains(p)) return true;
        return false;
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Внутренний применятор урона. Возвращает true, если что-то изменилось.
    /// Параметр <paramref name="depth"/> ограничивает рекурсию (overflow → spread).
    /// </summary>
    private bool ApplyDamageInternal(BodyPartType part, float damage, int depth)
    {
        if (IsDead) return false;
        if (damage <= 0f) return false;
        if (depth > 4) return false; // защита от лавины (теоретически — невозможно при коэффициенте 0.15)

        // Если часть разрушена — весь входящий урон конвертируется в spread (минуя саму часть).
        if (destroyed.Contains(part))
        {
            SpreadDamage(damage, originator: part, depth);
            return true;
        }

        float prev = GetHp(part);
        float next = prev - damage;

        if (next > 0f)
        {
            currentHp[part] = next;
            return true;
        }

        // Часть только что разрушена.
        currentHp[part] = 0f;
        destroyed.Add(part);
        float overflow = -next; // damage exceeded remaining HP

        // Критическая часть → смерть.
        if (config.IsCritical(part))
        {
            TriggerDeath();
            return true;
        }

        // Overflow распределяется по spreadTargets с коэффициентом.
        if (overflow > 0f) SpreadDamage(overflow, originator: part, depth);
        return true;
    }

    private void SpreadDamage(float amount, BodyPartType originator, int depth)
    {
        if (config == null || config.spreadTargets == null || config.spreadTargets.Count == 0) return;

        float multiplied = amount * config.damageSpreadMultiplier;
        if (multiplied <= 0f) return;

        // Считаем число живых валидных целей (исключая originator и уже разрушенные).
        int valid = 0;
        foreach (var t in config.spreadTargets)
        {
            if (t == originator) continue;
            if (destroyed.Contains(t)) continue;
            valid++;
        }
        if (valid == 0) return;

        float perPart = multiplied / valid;
        foreach (var t in config.spreadTargets)
        {
            if (t == originator) continue;
            if (destroyed.Contains(t)) continue;
            ApplyDamageInternal(t, perPart, depth + 1);
            if (IsDead) return;
        }
    }

    private void TriggerDeath()
    {
        IsDead = true;
        foreach (var p in AllParts())
        {
            currentHp[p] = 0f;
            destroyed.Add(p);
        }
        Debug.Log("[HealthSystem] Player dead");
        OnDeath?.Invoke();
    }
}
