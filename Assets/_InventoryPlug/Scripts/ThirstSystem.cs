using System;
using UnityEngine;

/// <summary>
/// Модуль жажды. Опциональный — удалить компонент с игрока, и жажда пропадёт
/// из UI и эффектов (через <see cref="IThirstTarget"/>).
/// </summary>
[DefaultExecutionOrder(-90)]
public class ThirstSystem : MonoBehaviour, IThirstTarget, IPlayerModule
{
    public static ThirstSystem Instance { get; private set; }

    public string ModuleName => "Thirst";

    [Header("Max value")]
    public float maxThirst = 100f;

    [Header("Decay (points per minute)")]
    [Range(0f, 50f)] public float decayPerMinute = 2f;

    [Header("Start")]
    [Tooltip("Если true — стартовать со 100%. Иначе — со значения Start Thirst.")]
    public bool startFull = true;
    [Range(0f, 100f)] public float startThirst = 100f;

    public float Thirst { get; private set; }
    public float ThirstNormalized => maxThirst > 0f ? Mathf.Clamp01(Thirst / maxThirst) : 0f;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Thirst = startFull ? maxThirst : Mathf.Clamp(startThirst, 0f, maxThirst);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (decayPerMinute <= 0f || Thirst <= 0f) return;
        Thirst = Mathf.Max(0f, Thirst - decayPerMinute / 60f * Time.deltaTime);
        OnChanged?.Invoke();
    }

    public void AddThirst(float amount)
    {
        Thirst = Mathf.Clamp(Thirst + amount, 0f, maxThirst);
        OnChanged?.Invoke();
    }

    public void SetThirst(float value)
    {
        Thirst = Mathf.Clamp(value, 0f, maxThirst);
        OnChanged?.Invoke();
    }
}
