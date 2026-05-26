using System;
using System.Collections.Generic;

/// <summary>
/// Дефолтная реализация <see cref="IItemEffectContext"/> на словаре. Сервисы регистрируются
/// по типу (обычно — по интерфейсу), и эффекты их же типом запрашивают.
///
/// Пример (game-side):
/// <code>
///   var ctx = new ItemEffectContext();
///   ctx.Register&lt;IHungerThirstTarget&gt;(PlayerVitals.Instance);
///   ctx.Register&lt;IHealthTarget&gt;(HealthSystem.Instance);
///   effect.TryApply(item, ctx);
/// </code>
/// </summary>
public class ItemEffectContext : IItemEffectContext
{
    private readonly Dictionary<Type, object> services = new();

    public void Register<T>(T service) where T : class
    {
        if (service == null) return;
        services[typeof(T)] = service;
    }

    public void Unregister<T>() where T : class => services.Remove(typeof(T));

    public T Get<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? (T)s : null;
}
