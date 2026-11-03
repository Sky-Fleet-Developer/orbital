# Этап 3.1. Core: таблица тел и правило SOI

План: `Docs/Stage3-Plan.md`, разделы «3.1 Одно правило SOI» и подэтап 3.1. Выполнен координатором сам (2026-11-03, без исполнителя: решение пользователя).

## Журнал
- `Assets/Orbital/Core/Flight/SoiFrames.cs` (новый, engine-free, сервер подключает автоматически):
  - `BodyFrameSource`: тело числами, как в `world.json`.
  - `BodyFrame`: родитель, nu, радиус, SOI (у корня +inf), орбита `StaticOrbit`, `SurfaceData`.
  - `BodyFrameTable.Build`: правила `ServerWorld.Build`. Эпоха орбиты = epochZero + epoch, nu орбиты по умолчанию от родителя, SOI считает `TrajectoryUtility`, поверхность `SurfaceBuilder`.
  - Запросы `LocalState`, `GlobalState`, `Convert`. `Convert` идёт через ближайшего общего предка, без аллокаций.
  - `SoiRule.Check`. Константы `EntryFactor = 1.0` и `ExitFactor = 1.005` живут в одном месте. Выход к деду, потом вход в детей, несколько уровней за один вызов.
- `Assets/Orbital/Core/Worlds/WorldFrames.cs` (новый, только клиент): таблица из `WorldFileDef`.
- Сервер:
  - `Propagator.ExitHysteresis = SoiRule.ExitFactor`.
  - `ServerWorld.BuildFrames()` (`World.cs`) строит ту же таблицу из своего определения, пригодится в 3.3.
- Тесты:
  - `Tools/OrbitMathTests/Stage3SoiTests.cs` (5 тестов):
    - SOI Земли, Фобоса (R·1.5), Плутона и Харона через nu барицентра;
    - `Convert` туда и обратно сохраняет глобальное состояние;
    - вход и выход, полоса гистерезиса;
    - нет дребезга на 1000 точках у границы;
    - вложенные сферы Солнце → барицентр → Плутон.
  - `OrbitServer.Tests/Stage3SoiTests.cs` (3 теста):
    - таблица совпадает с `ServerWorld` по всем телам (SOI, глобальные состояния в 3 моментах, вращение);
    - константы;
    - правило Core у времени выхода, найденного сервером (гипербола от Луны).
- Прогоны: OrbitMathTests 640/640, сервер 294/294.

## Не проверено
- Компиляция в Unity: batch-прогон `Tools/unity-build.sh test EditMode` после закрытия Editor, весь проект собрался, EditMode 834/834 (лог `Build/logs/test-editmode-20261103-213302.log`).

## Открытые вопросы
- Нет. Вопрос 3 плана (смысл локаций) ждёт ответа пользователя, касается только 3.7.
