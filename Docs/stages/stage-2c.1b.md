<!-- Журнал этапа вынесен дословно из Docs/GameplayPrototype.md 2026-11-03; вход и статус: Docs/GameplayPrototype.md -->

#### 2c.1b Сервер: игровое время стоит, пока сервер лежит: принят координатором 2026-10-30 (тесты перезапущены 256/256 и 571/571; `Heartbeat.cs`, `StartClock`, детектор остановки и запас после старта прочитаны)

Проект: `Docs/Stage2c-Downtime-Retro.md`, раздел A, и решения пользователя от 2026-10-30 в конце того файла (п. 4: `freeze` по умолчанию, `continue` - отдельная настройка для тестов; п. 5: `bootGraceSeconds` 60 в конфиге; п. 7: сон хоста и пауза ВМ - простой).

Журнал (исполнитель). Git-коммитов нет, файлы Unity и `Assets/Orbital/Core` не трогались, окно W и grace удара не менялись (это 2c.3b), файл секрета админа не читался.
- `dotnet test` в `E:\Projects\orbital-server\OrbitServer.Tests`: пройдено 256, не пройдено 0 (было 238; +18 в новом `Stage2c1bTests.cs`). Из старых тестов поправлены только рестарт-тесты 2c.1 в `Stage2cTests.cs` (`Restart_ContinuesTheSavedFormula_ThroughTheDowntime`, `Restart_WithReset_KeepsGameTime_AndReturnsToScaleOne`, `Restart_ClampsASavedScaleOutsideTheConfiguredRange` и помощник `ScaledAndSaved`): они проверяют формулу сквозь простой, поэтому явно ставят `downtimePolicy: continue` (с комментарием) и удаляют за собой файлы `.alive.*`.
- `dotnet test` в `E:\Projects\orbital\Tools\OrbitMathTests`: пройдено 571, не пройдено 0 (Core не менялся, прогон для контроля).
- Ручной запуск собранного `OrbitServer.exe` (данные во временной папке scratchpad, порт 18765): через 4 с в папке `state.json` (414 байт) и слоты `state.json.alive.0`/`.1` (117 и 116 байт, `seq` 4 и 3: таймер пишет раз в секунду); `taskkill /F` (аналог kill -9), повторный запуск печатает `Clock (freeze): resuming at game time 26077143,328 s from the heartbeat mark; downtime 4,0 real s; time scale 1` и новый boot id; время после старта 26077143,4, а не 26077147.

Устройство (`E:\Projects\orbital-server\OrbitServer\`):
- **Настройки** (`Config.cs`, `serverconfig.json`): `downtimePolicy` (`freeze` по умолчанию в коде и в файле | `continue`; другое значение, в том числе `Freeze` с заглавной, - отказ старта), `heartbeatIntervalMs` (1000, больше 0), `heartbeatStallFactor` (5, больше 1; коэффициент детектора остановки сделан настройкой), `bootGraceSeconds` (60, 0 выключает, меньше 0 - отказ). Проверка `ValidateDowntime` идёт и в `ServerConfig.Load`, и в конструкторе `GameServer` (для конфигов из кода). `Program.Main` ловит `InvalidDataException` из загрузки конфига, мира и `GameServer` и возвращает код 2.
- **Источники времени** (`ServerClock.cs`): `TimeSources {Wall, Monotonic}`; в продакшене `Wall` = `UtcNow - gameEpochUtc`, `Monotonic` = `Stopwatch`. `ServerClock.ForPolicy(src, freeze)`: при `continue` часы идут по `Wall`; при `freeze` `real = Wall в момент старта + (Monotonic - Monotonic при старте)`: те же единицы (секунды от эпохи), но перевод системных часов игровое время не двигает. Тесты подставляют две `ManualClock` (`new TimeSources(wall.Read, mono.Read)`) или одну на обе роли (`TimeSources.Single`; так работают старые конструкторы `GameServer(world, cfg, Func<double>)`, поведение старых тестов не изменилось). Конструктор с готовым `ServerClock` остался.
- **Монотонность.** `ServerClock` хранит последнее выданное значение; `Now`, `Read`, `SetScale`, `RealSecondsUntil` никогда не дают меньше. Это действует и при `continue` (стенные часы назад: время стоит, пока они не догонят).
- **Сердцебиение** (`Heartbeat.cs`, новый). Слоты `<dataFile>.alive.0` и `.alive.1`, слот = `seq mod 2`, запись `FileMode.Create` + `FileOptions.WriteThrough` + `Flush(true)`. Запись около 120 байт; `crc` - CRC-32 (IEEE, своя таблица, без пакетов) текста до поля `crc`. Чтение: лучший целый слот по наибольшему `seq`; пустой, порванный, с неверным CRC или нечисловой слот отбрасывается. `seq` продолжается с прочитанного при старте. Таймер (`GameServer.StartHeartbeat`, вызывает `Program`) пишет раз в `heartbeatIntervalMs`; главный JSON сердцебиение не трогает. Без файла данных всё это (и детектор остановки) - пустые операции.
- **Отметка P.** Каждый `Persist()` (только при файле данных) записывает в `ServerState.Alive {gameTime, bootId, real}` текущее игровое время и заодно текущий якорь часов (`ServerState.Clock`). Поле необязательное, формат 4. `real` (стенные секунды) добавлен сверх проекта: по нему считается длительность простоя, если сердцебиения нет.
- **Старт** (`GameServer.StartClock`, заменил `RestoreClock`). `freeze` и есть хотя бы одна отметка: `resumeAt = max(H, P)`, `ServerClock.Resume(resumeAt, scale)`, где `scale` - сохранённый (или 1 при `resetTimeScaleOnStart`, или зажатый в диапазон), затем сразу `Persist` и сердцебиение, в консоль строка `Clock (freeze): resuming at game time ... from the heartbeat|save mark; downtime N real s; time scale S`. `continue` или `freeze` без отметок: прежняя логика 2c.1 (сохранённый якорь и формула сквозь простой, затем сброс или зажим масштаба); при `freeze` после этого сразу `Persist` и сердцебиение, так что следующий старт уже замораживает. Без отметок, но с непустым состоянием печатается `WARNING: the save has no alive marks (written before stage 2c.1b) ...`; пустое состояние (новый мир) - без предупреждения, `source: new`.
- **Штатная остановка.** `GameServer.Shutdown()` (идемпотентно): останавливает таймер, `Persist`, последнее сердцебиение. `Program` вызывает его в `finally` после сетевого цикла (Ctrl+C отменяет цикл) и в `AppDomain.ProcessExit`.
- **Детектор остановки.** В режиме `freeze` с файлом данных часы взводятся (`ArmStallDetector(interval × factor)`); опорная точка - последний `Heartbeat()` (сердцебиение таймера, не запись при старте). Проверка стоит в каждом чтении часов, а не только в таймере: первый же запрос после пробуждения получает остановленное время. Если с последнего сердцебиения прошло больше `heartbeatIntervalMs × heartbeatStallFactor` реальных секунд, якорь переносится: игровое время = последнее выданное значение (оно не меньше последней отметки), реальное = сейчас. В консоль: `Clock: process stall of N real s detected (host sleep or VM pause?): counted as downtime, game time held at T s`. Сохранения при этом нет (следующее сердцебиение пишет отметку). При `continue` детектора нет.
- **`bootGraceSeconds`.** Действует, если сервер стартовал с файлом данных и настройка больше 0 (в обоих режимах; сервер в памяти - нет). (а) Удар, ждавший grace в момент старта (`удар ≤ старт < удар + grace`): `destroyAt = max(удар + grace, старт + bootGrace × max(1, scale))`, масштаб - в момент проверки, как у `impactGraceSeconds`; то же в `NextDestruction` (таймер reaper). (б) Пока с момента старта прошло меньше `bootGraceSeconds` реальных (монотонных) секунд, нижняя граница начала сегмента `maneuver` и времени отчёта `attitude` - `min(now - W, resumeAt - W)`; `attitude` добавлен сверх проекта, потому что у него с 2c.3 та же граница, что у сегмента.
- **Протокол** (`Net.cs`): `hello_ok.clock`, `pong.bootId`; `admin_status.status` дополнительно `bootId`, `resumedAt`, `policy`. `bootId` - 8 байт из `RandomNumberGenerator`, hex, новый при каждом запуске процесса.

Примеры:

```json
{"type":"hello_ok","id":1,"nickname":"Eagle","craftId":"craft-1","token":"...","created":false,"serverTime":1030,
 "timeScale":{"scale":1,"since":1030},
 "clock":{"bootId":"b099bf545220d64d","resumedAt":1030,"downtimeReal":3970,"policy":"freeze","source":"save"},
 "snapshot":{...},"state":{...}}
{"type":"pong","id":2,"time":1030.5,"scale":1,"bootId":"b099bf545220d64d"}
```

`source`: `heartbeat` или `save` (`freeze`, какая отметка новее), `formula` (`continue`, или `freeze` со старым сохранением без отметок), `new` (`freeze`, пустой мир без отметок). `downtimeReal` - реальные секунды от последней отметки по стенным часам, `null`, если отметок нет.

Слот сердцебиения (`state.json.alive.0`, одна строка, из ручного запуска):

```json
{"seq":4,"bootId":"c38f5f9a1d160481","gameTime":26077143.328080602,"real":26077143.328253,"scale":1,"crc":"3a8b03ff"}
```

Фрагмент главного файла:

```json
"clock": {"anchorGameTime": 1050, "anchorReal": 5000, "scale": 1},
"alive": {"gameTime": 1053, "bootId": "c38f5f9a1d160481", "real": 5003}
```

Настройки (`OrbitServer/serverconfig.json`):

```json
"resetTimeScaleOnStart": false,
"downtimePolicy": "freeze",
"heartbeatIntervalMs": 1000,
"heartbeatStallFactor": 5,
"bootGraceSeconds": 60
```

Тесты `OrbitServer.Tests/Stage2c1bTests.cs` (18; все файлы во временных папках, удаляются после теста; «kill -9» - объект `GameServer` бросается без `Shutdown`):
- `freeze` против `continue`: рестарт через 3950 с продолжает с последнего сердцебиения, `downtimeReal` 3950, новый `bootId`; те же файлы с `continue` идут по формуле сохранённого якоря;
- `freeze` по умолчанию в коде и в поставляемом `serverconfig.json`;
- kill -9: сердцебиение в 1050, принятый сегмент в 1053 -> `resumeAt` 1053 (`source: save`), ни одно сохранённое время (снапшоты, конец жжения, ориентация, создание аккаунта) не позже `resumeAt`;
- штатная остановка пишет обе отметки, повторный `Shutdown` ничего не делает;
- масштаб 10 сохраняется через простой и идёт только после старта; `resetTimeScaleOnStart` - 1 с момента возобновления, сброс сразу сохранён;
- удар, запланированный на время простоя, происходит после старта (при `continue` аппарат уничтожен первым же запросом);
- удар в grace в момент остановки ждёт `старт + 60`; сегмент, начатый за 3 с до удара и пришедший через 23 с, принят, аппарат летит; с `bootGraceSeconds: 0` тот же сегмент не помогает и аппарат уничтожен в `удар + grace`;
- граница возраста `resumeAt - W` действует для `maneuver` и `attitude` первые 60 с, через 61 с снова `now - W`;
- радио в пути: задержка около 30 с, остановка через 10 с, после старта сообщение приходит ровно через оставшиеся около 20 с;
- порванный слот -> второй слот; испорченная цифра -> CRC отбрасывает слот; оба слота пусты -> P из главного файла;
- старое сохранение без отметок: один раз по формуле (`source: formula`), сразу сохранено с отметкой, следующий старт замораживает;
- 20 сердцебиений не меняют байты и время записи главного файла, слот меньше 512 байт и разбирается;
- сервер в памяти: сердцебиения и отметок нет;
- стенные часы назад и вперёд при `freeze` не двигают время; при `continue` `Now` не убывает; то же у `ServerClock` через смену масштаба;
- детектор остановки: 4,9 с - не остановка, 100 с - остановка (время держится, потом идёт дальше), `heartbeatStallFactor: 20` - 10 с не остановка, при `continue` детектора нет;
- плохой конфиг: `pause`, `Freeze`, `heartbeatIntervalMs` 0 и -5, `bootGraceSeconds` -1, `heartbeatStallFactor` 1 - `ServerConfig.Load` бросает, `Program.Main` возвращает 2; конфиг из кода отклоняет конструктор `GameServer`;
- WebSocket: `hello_ok.clock` и `pong.bootId`; спавн (сохранён) в 1030, эфемерный `pong` в 1040, kill -9, рестарт: новый `bootId`, `serverTime` = 1030 (не меньше последнего сохранённого ответа), `downtimeReal` 3970, аппарат из сохранения на месте.

Что изменено:
- `E:\Projects\orbital-server\OrbitServer\Heartbeat.cs` - новый (`AliveMark`, `HeartbeatRecord`, `AliveFile`, `Crc32`).
- `OrbitServer/ServerClock.cs`: `TimeSources`, `ClockStall`, `ServerClock.ForPolicy`, стенные часы, монотонность, `Resume`, детектор остановки (`ArmStallDetector`, `Beat`, событие `Stalled`).
- `OrbitServer/GameServer.cs`: `BootInfo` и `HelloResult.Clock`, конструктор с `TimeSources`, `StartClock` вместо `RestoreClock`, `Heartbeat`/`StartHeartbeat`/`Shutdown`, `Persist` с отметкой, `ImpactDestroyTime` и `PastBound` (boot grace).
- `OrbitServer/Storage.cs`: `ServerState.Alive`.
- `OrbitServer/Config.cs`: четыре настройки, `ValidateDowntime`, `DowntimePolicies`.
- `OrbitServer/Net.cs`: `hello_ok.clock`, `pong.bootId`, поля `admin_status`.
- `OrbitServer/Program.cs`: `TimeSources.System`, запуск сердцебиения, `Shutdown` при выходе, код 2 при плохом конфиге.
- `OrbitServer/serverconfig.json`: четыре настройки.
- `OrbitServer.Tests/Stage2c1bTests.cs` - новый; `OrbitServer.Tests/Stage2cTests.cs` - рестарт-тесты 2c.1 на `continue`.
- `README.md` сервера: настройки, протокол, что происходит при рестарте, число тестов.
- В репозитории orbital: этот файл (подраздел 2c.1b и числа тестов; строку статуса 2c не трогал - её обновит координатор), `Docs/Stage2c-Downtime-Retro.md` (раздел A отмечен выполненным).

Не проверено:
- Штатная остановка в реальном процессе: Ctrl+C и `ProcessExit` (закрытие консоли, SIGTERM на Linux) не запускались; `Shutdown` проверен тестами напрямую. Ручной запуск проверил только kill -9 (`taskkill /F`).
- Настоящий сон хоста или пауза ВМ: детектор проверен на подставных часах.
- Гарантия `WriteThrough` на конкретной файловой системе (сетевой диск, Linux) не проверялась; порванная запись смоделирована усечением файла.
- Клиентская часть (`ClockDiscipline`, откат при смене `bootId`, баннер) - этап 2c.4b.

Открытые вопросы:
1. Детектор остановки держит игровое время на последнем выданном значении, а не строго на последней отметке: оно не меньше отметки (так `Now` не убывает), разница не больше одного интервала сердцебиения. Если таймер сердцебиения не получит поток дольше 5 с (перегрузка пула потоков), это тоже засчитается как простой. Устраивает ли?
2. `bootGraceSeconds` сейчас действует при любом старте с файлом данных, в том числе с `continue`. Ограничить только режимом `freeze`?
3. Граница возраста первые `bootGraceSeconds` после старта применена и к `attitude`. Подтвердить.
4. Горячая группа (`_hotUntil`) по-прежнему не сохраняется: после рестарта частых push нет до следующего маневра (как до 2c.1b).
5. При `freeze` сохранённый `clock.anchorReal` - монотонное время, приведённое к секундам от эпохи в момент старта; если потом переключить сервер на `continue`, формула пойдёт от него (простой засчитается). Для тестового режима это, видимо, нормально.
