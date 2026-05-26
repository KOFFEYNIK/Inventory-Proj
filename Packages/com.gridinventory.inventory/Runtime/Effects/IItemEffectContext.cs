/// <summary>
/// Контейнер game-side сервисов, которые <see cref="ItemEffect"/>-эффекты используют для
/// своей работы (например, рестор HP, добавление голода/жажды, активация баффа и т.п.).
///
/// Конкретные интерфейсы сервисов — game-specific и НЕ определяются плагином: их вводит
/// сам проект (например, IHungerThirstTarget, IHealthTarget, IMagicTarget). Плагин знает
/// только про <see cref="Get{T}"/> — получение сервиса по типу.
/// </summary>
public interface IItemEffectContext
{
    /// <summary>Получить зарегистрированный сервис. Возвращает null, если сервис не зарегистрирован.</summary>
    T Get<T>() where T : class;
}
