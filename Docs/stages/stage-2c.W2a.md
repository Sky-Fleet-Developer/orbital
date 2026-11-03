# Этап 2c.W2a: нулевая эпоха мира (сервер)

Статус: принят координатором 2026-11-03 (тесты перезапущены 281/281 и 577/577).

Решение пользователя: нулевая эпоха пишется в конфиг мира, клиент получает её с сервера и строит орбиты от неё.

Сделано:
- `Assets/_game/Config/world.json`: поле верхнего уровня `"epochZero": 0.0` (остальной текст не менялся).
- `orbital-server/OrbitServer/World.cs`: `WorldDef.EpochZero` (нет поля = 0); эпоха орбиты = `EpochZero + orbit.epoch`; `Body.RotationAngle(t) = Phase0 + Omega*(t - EpochZero)`; фаза приливно запертых тел берётся в `EpochZero`.
- `Net.cs`: `hello_ok.worldEpochZero`, `get_world.epochZero`.
- `Tools/convert_scene_world.py` пишет `epochZero: 0.0`; `README.md`, `Tools/README-world.md` дополнены.
- Тесты: `Stage2cW2Tests.cs` (мир с 1000 в момент t = мир с 0 в t-1000 по позициям 32 тел и углам вращения; нет поля = 0), `NetTests.cs` (`hello_ok` и `get_world` несут значение; отклонение: тест в NetTests, потому что сетевой стенд там).

Команды: `dotnet test` в `OrbitServer.Tests`: 281 пройден, 0 ошибок (было 278). `dotnet test` в `Tools/OrbitMathTests`: 577 пройден, 0 ошибок (Core-загрузчик принимает лишнее поле).

Не проверено: клиент (Editor) не трогался и не проверялся; значение epochZero != 0 по сети не проверялось (общий файл содержит 0); сохранения/крафты при ненулевой эпохе не тестировались.
