/// <summary>
/// Общий контракт «интерактивного объекта в мире» для камерного хвата (<c>HoverPickup3D/2D</c>).
/// Реализуют мировые пикапы (<see cref="WorldItemBase"/>) и статичные мировые контейнеры
/// (<see cref="WorldContainerBase"/>).
///
/// HoverPickup наводится по лучу/оверлапу, показывает world-space подсказку наведённой цели
/// и по клавише подбора вызывает <see cref="Interact"/>: для предмета это подбор, для ящика —
/// открытие окна содержимого.
/// </summary>
public interface IWorldInteractable
{
    /// <summary>Основное действие при наведении и нажатии клавиши: подобрать / открыть.</summary>
    void Interact();

    /// <summary>Показать/скрыть world-space подсказку при наведении камеры.</summary>
    void SetPromptVisible(bool visible);
}
