using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Модуль голода. Опциональный — удалить компонент с игрока, и голод пропадёт
/// из UI и эффектов (через <see cref="IHungerTarget"/>).
///
/// API: <see cref="AddHunger"/>, <see cref="SetHunger"/>.
/// Подписка: <see cref="OnChanged"/>.
/// </summary>
[DefaultExecutionOrder(-90)]
[MovedFrom(true, sourceNamespace:null, sourceAssembly:"Assembly-CSharp", sourceClassName:"PlayerVitals")]
public class HungerSystem : MonoBehaviour, IHungerTarget, IPlayerModule
{
    public static HungerSystem Instance { get; private set; }

    public string ModuleName => "Hunger";

    [Header("Max value")]
    public float maxHunger = 100f;

    [Header("Decay (points per minute)")]
    [Range(0f, 50f)] public float decayPerMinute = 2f;

    [Header("Start")]
    [Tooltip("Если true — стартовать со 100%. Иначе — со значения Start Hunger.")]
    public bool startFull = true;
    [Range(0f, 100f)] public float startHunger = 100f;

    public float Hunger { get; private set; }
    public float HungerNormalized => maxHunger > 0f ? Mathf.Clamp01(Hunger / maxHunger) : 0f;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Hunger = startFull ? maxHunger : Mathf.Clamp(startHunger, 0f, maxHunger);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (decayPerMinute <= 0f || Hunger <= 0f) return;
        Hunger = Mathf.Max(0f, Hunger - decayPerMinute / 60f * Time.deltaTime);
        OnChanged?.Invoke();
    }

    public void AddHunger(float amount)
    {
        Hunger = Mathf.Clamp(Hunger + amount, 0f, maxHunger);
        OnChanged?.Invoke();
    }

    public void SetHunger(float value)
    {
        Hunger = Mathf.Clamp(value, 0f, maxHunger);
        OnChanged?.Invoke();
    }
}
