# Использование без Jobs

`Anvelop.HexGrid` можно вызывать из обычных C#-классов и `MonoBehaviour`. Прямые вызовы выполняются синхронно на текущем потоке. Jobs и Burst для этого не требуются; прямой вызов из `MonoBehaviour` автоматически Burst-кодом не становится.

Runtime API и правила координат остаются общими для обоих способов вызова. Геометрия и обходы работают со значениями и не требуют `NativeArray`. Для поиска пути пакет использует native-контейнеры; sample `ManagedHexMap` владеет ими внутри обычного managed-класса и переиспользует после однократного создания.

## Импорт примеров

При UPM-установке откройте в Package Manager **Anvelop HexGrid → Samples → Managed usage → Import**. Unity скопирует пример в `Assets/Samples/Anvelop HexGrid/1.0.1/Managed usage`.

Пример содержит `ManagedHexGeometry.cs`, `ManagedHexMap.cs` и `ManagedHexGridExample.cs`, пространство имён `Anvelop.HexGrid.Samples`. Для своего `.asmdef` добавьте ссылку `Anvelop.HexGrid.ManagedSamples`, если используете классы sample. Прямым runtime-вызовам достаточно ссылки `Anvelop.HexGrid` и используемых ими сборок Unity.

Добавьте `ManagedHexGridExample` на GameObject: он создаёт карту при включении компонента и освобождает при выключении. Метод `DemonstrateQueries` запускается при включении и доступен через контекстное меню **Run managed hex examples**. Снимки результатов видны в Inspector: длина и стоимость пути, размеры областей движения и видимости, выбранная клетка и проверка видимости цели. Сцены, модели и меши для математических операций не нужны.

Legacy `.unitypackage` включает эти же managed-примеры в `Assets/Anvelop/HexGrid/Samples/ManagedUsage`; отдельный импорт sample не нужен. Tests и sample **Burst jobs** доступны в UPM-архиве и исходном пакете. Выбирайте один способ установки пакета, чтобы не создавать дубликаты сборок.

## Координаты, выбор клетки и обходы

Этот класс не использует native-контейнеры, Jobs или Unity lifecycle. Методы работают с value types, и их повторные вызовы не создают managed-объекты.

```csharp
using Anvelop.HexGrid;
using Unity.Mathematics;

public static class PlainHexGeometry
{
    public static HexCoord Pick(float3 worldPosition)
    {
        var layout = new HexLayout(HexOrientation.Pointy,
            new float2(1f, 1f), new float2(0f, 0f));
        return layout.FromWorldXZ(worldPosition);
    }

    public static int Inspect(HexCoord center, HexCoord target)
    {
        int checksum = center.DistanceTo(target);
        HexCoord neighbor = center.Neighbor(0);
        HexCoord rotated = neighbor.RotateAround(center, 1);
        HexOffset address = HexCoordinates.ToOffset(rotated, HexOffsetLayout.OddR);
        checksum += HexCoordinates.FromOffset(address, HexOffsetLayout.OddR).Length;

        foreach (HexCoord cell in new HexRing(center, 2))
            checksum += center.DistanceTo(cell);

        foreach (HexCoord cell in new HexLine(center, target))
            checksum += center.DistanceTo(cell);

        return checksum;
    }
}
```

Для сетки на XY используйте `FromWorldXY` / `ToWorldXY`. `HexLayout` также поддерживает flat-ориентацию, ненулевой origin и отдельный масштаб каждой оси. Преобразуйте мировую позицию в локальную плоскость вашей сетки перед вызовом, если GameObject сетки дополнительно повёрнут или масштабирован.

Готовый `ManagedHexGeometry` из sample предоставляет `PickXZ`, `ToWorldXZ`, `SumDistancesInRange` и `SumLineDistances` для тех же прямых вызовов.

## Обычный массив для данных карты

`HexStorage` только рассчитывает индекс и не владеет памятью. Поэтому значения клеток можно хранить в обычном managed-массиве, созданном один раз при подготовке:

```csharp
using Anvelop.HexGrid;

var storage = HexStorage.Hexagon(radius: 4);
var terrainCosts = new int[storage.Count]; // One setup allocation.
for (int i = 0; i < terrainCosts.Length; i++)
    terrainCosts[i] = 1;

var wall = new HexCoord(1, 0);
if (storage.TryGetIndex(wall, out int wallIndex))
    terrainCosts[wallIndex] = -1;

var selected = new HexCoord(2, -1);
if (storage.TryGetIndex(selected, out int selectedIndex))
{
    int selectedCost = terrainCosts[selectedIndex];
    // Read or update an existing array slot; no collection creation.
}

if (storage.TryGetCoordinate(0, out HexCoord firstCell))
{
    int distanceFromCenter = firstCell.Length;
}
```

После подготовки `TryGetIndex`, `TryGetCoordinate` и чтение/запись массива не выделяют память. Для других форм используйте `HexStorage.Rectangle`, `Rhombus` или `Triangle`. В sample есть краткие helper-методы `ManagedHexGeometry.CreateTerrainCosts(radius)` и `TryReadTerrainCost(costs, radius, coordinate, out value)`; первый создаёт массив и предназначен для подготовки данных.

Обычные `List<T>` и `Dictionary<HexCoord,T>` тоже допустимы в managed-коде. Их создание и расширение ёмкости относятся к аллокациям вызывающего кода. Для горячего пути заранее выделяйте достаточную ёмкость и избегайте создания коллекций, LINQ, строк и логов при каждом запросе. Массив выше — пользовательское хранилище значений; он не подменяет native-буферы, ожидаемые runtime-поиском.

## Поиск пути и результат запроса

`ManagedHexMap` — обычный `sealed` C#-класс с `IDisposable`. Конструктор `ManagedHexMap(int radius, int defaultCost = 1, HexCoord center = default)` создаёт hexagon-карту и нужные native-буферы. `defaultCost` должен быть неотрицательным. Создайте один экземпляр на время работы системы:

```csharp
using Anvelop.HexGrid;
using Anvelop.HexGrid.Samples;

using (var map = new ManagedHexMap(radius: 4))
{
    map.SetCost(new HexCoord(1, 0), -1); // A negative enter cost makes a wall.
    HexSearchResult result = map.FindPath(new HexCoord(0, 0), new HexCoord(3, 0));
    int checksum = 0;
    if (result.Status == HexSearchStatus.Success)
    {
        for (int i = 0; i < map.ResultCount; i++)
        {
            HexCoord cell = map.GetResultCell(i);
            checksum += cell.Length;
        }
    }
    // Use checksum or route cells before another result-producing query.
}
```

Создание класса, native-памяти и индекса выделяет память один раз. Корректные повторные запросы используют уже подготовленные буферы. Для живой игровой системы храните экземпляр в поле; не создавайте его заново в `Update`, для каждого пути или каждой проверки видимости.

| Метод | Результат |
| --- | --- |
| `FindPath(start, goal)` | Путь, включая старт и цель; `Cost` содержит сумму стоимостей входа |
| `Reachable(start, budget)` | Достижимые клетки по `long`-бюджету стоимости |
| `ReachableSteps(start, maxSteps)` | Достижимые клетки по числу шагов, независимо от величины неотрицательной стоимости |
| `FieldOfView(origin, radius, includeBlockingTargets = true)` | Видимые клетки в геометрическом радиусе |
| `HasLineOfSight(from, to, includeBlockingTarget = true)` | `bool`; не меняет текущий выходной результат |
| `SetCost(cell, cost)` | `bool`: `false` для клетки вне карты; успешное изменение сбрасывает `ResultCount` |
| `GetResultCell(index)` | Клетка из текущего результата; индекс должен быть меньше `ResultCount` |

`FindPath`, `Reachable`, `ReachableSteps` и `FieldOfView` возвращают `HexSearchResult` и используют один общий выходной буфер. Читайте `[0, ResultCount)` сразу после успешного запроса. Следующий такой запрос заменит результат; при неуспехе `ResultCount == 0`. Для сохранения нескольких результатов скопируйте нужные клетки в заранее выделенные собственные массивы. Доступ к одному экземпляру wrapper должен быть последовательным.

`CellCount` сообщает размер карты. `Dispose()` освобождает все принадлежащие wrapper контейнеры; повторный вызов допустим. После освобождения другие методы использовать нельзя: wrapper выбрасывает `ObjectDisposedException`. Ошибочные вызовы и создание исключений не входят в гарантию нулевых аллокаций корректных запросов.

## Жизненный цикл MonoBehaviour

Ниже — отдельный небольшой компонент, который создаёт владельца при включении и потребляет путь синхронно. Внутри запроса нет создания коллекций, строк или логов:

```csharp
using Anvelop.HexGrid;
using Anvelop.HexGrid.Samples;
using UnityEngine;

public sealed class PlainHexPathExample : MonoBehaviour
{
    private ManagedHexMap map;

    public int RouteLength { get; private set; }
    public int RouteChecksum { get; private set; }

    private void OnEnable()
    {
        map = new ManagedHexMap(radius: 5);
    }

    public bool TryBuildRoute(HexCoord target)
    {
        RouteLength = 0;
        RouteChecksum = 0;
        if (map == null) return false;

        HexSearchResult result = map.FindPath(new HexCoord(0, 0), target);
        if (result.Status != HexSearchStatus.Success) return false;

        RouteLength = map.ResultCount;
        for (int i = 0; i < RouteLength; i++)
            RouteChecksum += map.GetResultCell(i).Length;
        return true;
    }

    private void OnDisable()
    {
        map?.Dispose();
        map = null;
    }
}
```

Для перехода к параллельным запросам используйте runtime API с отдельными scratch/output-буферами и jobs из sample **Burst jobs**. `ManagedHexMap` остаётся managed-владельцем для последовательного использования; его экземпляр нельзя передавать в Burst job.
