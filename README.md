# Anvelop HexGrid

Переиспользуемый Unity UPM package **`com.anvelop.hexgrid`**, автор **Anvelop**. Пространство имён: `Anvelop.HexGrid`.

Пакет реализует математические операции, обходы, хранение координат, видимость и поиск пути для hex-сеток. Рабочие операции не создают managed-объекты и не выделяют native-память; коллекции и буферы создаёт вызывающий код. Модели, меши и сцены не создаются.

Сопоставление с разделами [Hexagonal Grids, Red Blob Games](https://www.redblobgames.com/grids/hexagons/) находится в [Documentation~/coverage.md](Documentation~/coverage.md). Алгоритмы реализованы отдельно для Unity/Burst; статья не включена в пакет.

## Установка

В Unity откройте **Window → Package Manager → + → Install package from git URL** и вставьте:

```text
https://github.com/bomberest/HexGrid.git
```

Для локальной копии выберите **Install package from disk** и укажите `package.json` в корне репозитория. Для архива `com.anvelop.hexgrid-1.0.1.tgz` выберите **Install package from tarball**.

Архив `Anvelop.HexGrid-1.0.1.unitypackage` импортирует runtime в `Assets/Anvelop/HexGrid` и managed-примеры в `Assets/Anvelop/HexGrid/Samples/ManagedUsage`. До импорта установите зависимости Burst, Collections и Mathematics через Package Manager. Пакетные тесты и пример **Burst jobs** доступны при установке через UPM. Выберите один способ установки, чтобы сборка пакета не дублировалась.

Минимальная заявленная версия Unity — **2022.3**. Зависимости: Burst **1.8.25**, Collections **2.4.3**, Mathematics **1.3.2**. Для кода из собственной `.asmdef` добавьте ссылку на `Anvelop.HexGrid` и нужные сборки Unity.

## Обычный C# и MonoBehaviour

Все runtime API можно вызывать напрямую из обычного C# и `MonoBehaviour`, без Jobs. Координаты, геометрия и struct-обходы работают и без native-контейнеров. Для хранения значений можно использовать обычный заранее созданный `T[]` с индексами `HexStorage`.

При UPM-установке выберите в Package Manager **Anvelop HexGrid → Samples → Managed usage → Import**. В legacy `.unitypackage` эти примеры уже находятся в `Assets/Anvelop/HexGrid/Samples/ManagedUsage`. Sample содержит:

- `ManagedHexGeometry` — прямые вызовы координат, геометрии и обходов.
- `ManagedHexMap` — `IDisposable`-владелец карты и переиспользуемых буферов для синхронных запросов.
- `ManagedHexGridExample` — компонент с созданием карты в `OnEnable`, примерами запросов и освобождением в `OnDisable`.

```csharp
using Anvelop.HexGrid;
using Anvelop.HexGrid.Samples;

// Setup: creates the owner and allocates native storage once.
using (var map = new ManagedHexMap(radius: 4))
{
    map.SetCost(new HexCoord(1, 0), -1);
    HexSearchResult result = map.FindPath(new HexCoord(0, 0), new HexCoord(3, 0));
    if (result.Status == HexSearchStatus.Success)
    {
        for (int i = 0; i < map.ResultCount; i++)
        {
            HexCoord cell = map.GetResultCell(i);
            // Consume this result before starting the next query.
        }
    }
}
```

В игре сохраняйте `ManagedHexMap` на время жизни системы и повторно вызывайте его методы; создание владельца и буферов не входит в обещание нулевых аллокаций запросов. Sample использует native-память внутри, но вызывающему коду не нужно управлять отдельными контейнерами. Один экземпляр хранит один результат; следующий запрос пути, области или FOV заменяет его, успешный `SetCost` сбрасывает. `HasLineOfSight` текущий результат не меняет.

| Способ вызова | Где выполняется | Буферы и память |
| --- | --- | --- |
| Обычный C# / MonoBehaviour | Синхронно на вызывающем потоке; сам по себе не компилируется Burst | Геометрия — значения; для поиска sample один раз создаёт и переиспользует native-буферы |
| `[BurstCompile]` job | Через Unity Jobs с нативной Burst-компиляцией | Вызывающий код передаёт контейнеры, зависимости и отдельный scratch на конкурентные запросы |

Полные примеры геометрии, managed-массива, поиска и жизненного цикла: [Documentation~/managed-usage.md](Documentation~/managed-usage.md). Для своего `.asmdef`, использующего импортированный managed sample, добавьте ссылку `Anvelop.HexGrid.ManagedSamples`; для одних runtime-операций достаточно `Anvelop.HexGrid`.

## Координаты и геометрия

```csharp
using Anvelop.HexGrid;
using Unity.Mathematics;

var cell = new HexCoord(2, -1); // Q=2, R=-1, S=-1
var neighbor = cell.Neighbor(0);
int steps = cell.DistanceTo(neighbor);
var offset = HexCoordinates.ToOffset(cell, HexOffsetLayout.OddR);
var axial = HexCoordinates.FromOffset(offset, HexOffsetLayout.OddR);

var layout = new HexLayout(HexOrientation.Pointy,
    new float2(1f, 1f), new float2(10f, 20f));
float2 position = layout.ToPixel(cell);
HexCoord picked = layout.FromPixel(position);
float3 world = layout.ToWorldXZ(cell);
float2 corner = layout.Corner(cell, 0);
```

`HexCoord` хранит два `int`, третий cube-компонент вычисляется. Векторная арифметика выполняется в axial/cube; offset-адреса сначала преобразуйте. Поддерживаются OddR, EvenR, OddQ, EvenQ и DoubleWidth/DoubleHeight. Для doubled-адреса `Column` и `Row` должны иметь одинаковую чётность, в том числе при отрицательных значениях.

Направления `0…5`: `(1,0)`, `(1,-1)`, `(0,-1)`, `(-1,0)`, `(-1,1)`, `(0,1)`. Индексы направлений и углов нормализуются по модулю шести. `RotateClockwise` следует экранной системе статьи с осью Y вниз; при отображении второй компоненты вверх на Unity XY визуальный знак поворота меняется. `Size` задаёт независимые масштабы радиуса по осям; отрицательный масштаб отражает сетку. Нулевой и бесконечный масштаб недопустимы; проверяйте `layout.IsValid` при конфигурации.

`HexFractional` нужен для интерполяции и округления. `Round()` использует ties-to-even и исправляет cube-инвариант. При точном попадании на границу соседних клеток результат определяется этим правилом. `HexLine` и видимость используют фиксированный малый сдвиг для выбора одной из сторон границы; это не supercover.

## Обходы и хранение

`HexRegion`, `HexRing`, `HexSpiral` и `HexLine` предоставляют struct-enumerator: используйте их напрямую в `foreach`. Радиус ноль включает единственную центральную клетку. Отрицательные радиусы дают пустой обход. Не преобразуйте обход в LINQ, `IEnumerable` или managed-список в горячем коде.

```csharp
foreach (HexCoord cell in HexRegion.Around(new HexCoord(0, 0), 3))
{
    // Write into an existing buffer or process immediately.
}
```

`HexRegion.Intersect` пересекает ограничения cube-осей до обхода. `HexStorage.Hexagon(radius)`, `Rhombus(width, height)`, `Rectangle(width, height, layout)` и `Triangle(side, up)` создают описания плотного хранения; `Count`, `TryGetIndex` и `TryGetCoordinate` сопоставляют клетки и индексы. `side` — число клеток вдоль стороны; ноль даёт пустой треугольник. Значения храните отдельно в уже созданном `NativeArray<T>`. Обратное индексирование hexagon/triangle использует двоичный поиск строки, `O(log side)`; остальные отображения — `O(1)`. Разреженную карту представляйте как `NativeParallelHashMap<HexCoord, T>` либо `HexMap` для поиска. `HexWrap.Rectangle` оборачивает выбранные оси в offset-пространстве; `HexWrap.Hexagon` оборачивает hexagon-карту, `MirrorCenter` возвращает её зеркальные центры. Преобразование координаты через wrap само по себе не меняет соседей в `HexSearch`: его граф использует обычные шесть смежных клеток.

## Поиск пути, движение и видимость

`HexMap(cells, costs, indices)` — заимствованное представление трёх caller-owned контейнеров:

| Данные | Требование |
| --- | --- |
| `NativeArray<HexCoord> cells` | Уникальные координаты |
| `NativeArray<int> costs` | Та же длина; стоимость входа `>= 0`; `< 0` блокирует движение и видимость |
| `NativeParallelHashMap<HexCoord, int> indices` | Каждая координата сопоставлена точному индексу в `cells` |
| `NativeArray<HexSearchNode> nodes` | Scratch: не меньше числа клеток |
| `NativeArray<int> frontier` | Scratch: не меньше числа клеток |
| `NativeArray<HexCoord> output` | Переиспользуемый буфер результата |

Создайте контейнеры и заполните индекс на этапе подготовки. `HexMap` не владеет памятью и не освобождает её. Запросы не увеличивают ёмкость буферов. Выход и scratch не должны пересекаться друг с другом или с исходными буферами карты, включая reinterpret/slice одной памяти; это проверяет вызывающий код.

```csharp
var map = new HexMap(cells, costs, indices);
HexSearchResult result = HexSearch.FindPath(
    map, start, goal, nodes, frontier, output);
if (result.Status == HexSearchStatus.Success)
{
    for (int i = 0; i < result.Count; i++)
    {
        HexCoord routeCell = output[i];
    }
}
```

- `FindPath` возвращает маршрут вместе со стартом и целью. Стоимость старта равна нулю; остальные клетки оплачиваются при входе. A* автоматически использует минимальную допустимую стоимость; при нулевом минимуме получается Dijkstra.
- `Reachable` строит область по `long`-бюджету стоимости. `ReachableSteps` использует BFS и считает один шаг на любую проходимую клетку. Оба включают старт и возвращают клетки в порядке `map.Cells`.
- `HasLineOfSight` и `FieldOfView` проверяют луч между центрами. Пропуски в карте перекрывают видимость. Первая блокирующая целевая клетка видна по умолчанию; это меняется флагом `includeBlockingTarget` / `includeBlockingTargets`. Промежуточная стена перекрывает луч; начало луча всегда видно. Это простая модель видимости с ценой FOV порядка `O(cells × radius)`.

Всегда проверяйте `Status`: `Success`, `NoPath`, `BufferTooSmall`, `InvalidInput`. При нехватке выходного буфера `Count` сообщает нужную длину, результат не записывается. При нехватке scratch `Count == 0`. Только префикс `[0, Count)` успешного результата содержит результат текущего запроса; `Cost` содержит итоговую стоимость успешного маршрута. Проверка уникальности клеток и полноты индекса — обязанность построителя карты.

## Burst и память

Runtime-код состоит из unmanaged-значений и операций над Unity native-контейнерами. Обычный прямой вызов выполняется без автоматической Burst-компиляции. Для Burst вызывайте API из `[BurstCompile]` job; атрибут на вызывающем job обеспечивает компиляцию используемого графа вызовов.

```csharp
using Anvelop.HexGrid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct DistanceJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<HexCoord> Cells;
    public NativeArray<int> Distances;
    public HexCoord Center;

    public void Execute(int index)
    {
        Distances[index] = Center.DistanceTo(Cells[index]);
    }
}
```

Память для `Cells` и `Distances` создаётся заранее. Для search jobs помечайте исходные native-контейнеры `[ReadOnly]`, передавайте отдельные scratch/output на параллельные запросы и учитывайте зависимости jobs. Копия `NativeArray` или `HexMap` ссылается на ту же память. Освобождайте только принадлежащие вам контейнеры после завершения jobs.

Для больших карт выбирайте подходящий алгоритм: struct-обходы требуют `O(1)` памяти; BFS — `O(V+E)`, A*/Dijkstra с индексированной кучей — `O((V+E) log V)` и scratch `O(V)`. Проверки и очистка search scratch обходят карту, поэтому короткий запрос также имеет затраты `O(V)`.

Все целочисленные промежуточные значения обязаны помещаться в `Int32`, а используемые отдельными API `long`-вычисления — в `Int64`. Это относится к `-Q-R`, разностям координат, масштабированию и вычислению ёмкости. Не передавайте `int.MinValue` в операции, использующие модуль. Float-входы должны быть конечными и округляться в допустимый диапазон. Точность выбора через float снижается вдали от origin; храните абсолютные индексы отдельно и переносите локальное начало координат для больших миров.

## Проверки и skill

Для работы над пакетом откройте [Development~](Development~) как проект Unity. Он подключает пакет из корня репозитория.

Чтобы пакетные EditMode-тесты появились в Unity Test Runner, добавьте в корень манифеста проекта:

```json
"testables": ["com.anvelop.hexgrid"]
```

Тесты находятся в `Tests/Editor`; покрывают свойства геометрии, поиск, граничные условия и Burst-исполнение. Фактические результаты запуска зависят от среды; проверяйте итоговый test report текущего запуска.

Проверено 26 октября 2026: **48/48 EditMode-тестов пройдено** в macOS Editor **6000.6.0f1**, Burst **2.0.0**, Collections **6.6.0**, Mathematics **1.4.0**. Два job-теста подтверждают исполнение нативного Burst-кода через `BurstDiscard`; измеренные повторные вызовы геометрии и поиска после прогрева выделяют **0 managed bytes**. Примеры jobs также скомпилированы. Unity 2022.3 и player-сборки на других платформах в этой проверке не запускались.

Для managed sample дополнительно пройдены **4 smoke-теста**: прямые запросы и обход стен, срок жизни результата и `Dispose`, picking и обычный массив, **0 managed bytes** при повторных корректных запросах после прогрева. Вместе с основным набором это **52/52**; отчёт отдельной проверки примеров находится в `Artifacts/ManagedUsage-results.xml` проекта разработки. Эти smoke-тесты не входят в импортируемый sample.

Skill поставляется в [Skills~/anvelop-hexgrid/SKILL.md](Skills~/anvelop-hexgrid/SKILL.md). Для повторного использования в Codex скопируйте каталог `anvelop-hexgrid` в `~/.codex/skills/` или `$CODEX_HOME/skills/`; после обнаружения skill его можно вызывать как `$anvelop-hexgrid`. Он помогает выбирать API и сохранять правила Burst, владения памятью и нулевых аллокаций.

Архивы собираются стандартным Python без внешних зависимостей:

```sh
python3 Tools~/build_package.py --output Development~/Artifacts
```
