<!-- Журнал этапа вынесен дословно из Docs/GameplayPrototype.md 2026-11-03; вход и статус: Docs/GameplayPrototype.md -->

#### 2c.3b Сервер: ретро-окно (жжение при обрыве связи): принят координатором 2026-11-03

Приёмка (координатор, 2026-11-03): тесты перезапущены, `OrbitServer.Tests` 273/273, `OrbitMathTests` 571/571. Прочитаны: метка и `seq` в `GameServer.Maneuver`/`ReportAttitude`/`CheckSeq`/`Commit`, фиксация `destroyAt` (`FixImpact`), аренда управления в `Net` (`TakeControl`, `ControlCommands` покрывает все команды управления протокола: `maneuver`, `attitude`, `radio_send`, `spawn`), проверка окна в `Maneuvers.Evaluate`; тесты цепочки из 8 сегментов (W=60 и W=15) и спасения от удара через WebSocket. Замечаний нет. Для клиента 2c.4: `duplicate` приходит с `accepted: false`, но это подтверждение; номер `seq` общий для `maneuver` и `attitude`, после спавна снова с 1.

Проект: `Docs/Stage2c-Downtime-Retro.md`, раздел B, и решения пользователя от 2026-10-30 в конце того файла (п. 1: W = 60 с в конфиге; п. 2: grace = W + 5, `spawn` в ожидании = «покинуть аппарат»; п. 6: аренда управления, прежние сессии - слушатели, возврат новым `hello` или командой на выбор исполнителя). Клиентская часть (`Uplink`, `Reconciler`, бюджет обрыва в HUD, п. 3 решений) не делалась: это 2c.4+.

Журнал (исполнитель, 2026-11-03). Git-коммитов нет, файлы в `E:\Projects\orbital\Assets\` (в том числе Core) не трогались, файл секрета админа не читался.
- `dotnet test` в `E:\Projects\orbital-server\OrbitServer.Tests`: пройдено 273, не пройдено 0 (было 256; +17 в новом `Stage2c3bTests.cs`). До правок тот же набор: 256/256. После первых правок сервера упали 17 старых тестов, все ожидаемо; исправлены явно, с комментариями `stage 2c.3b` (разбор ниже).
- `dotnet test` в `E:\Projects\orbital\Tools\OrbitMathTests`: пройдено 571, не пройдено 0 (Core не менялся, прогон для контроля).
- Проверка, что новые тесты настоящие (каждая порча отдельно, затем исходники возвращены из копии и сверены `diff`): метка не учитывается в `Evaluate` (только конец жжения) - падают 3 теста `Stage2c3bTests`; `destroyAt` не фиксируется - 2; проверка аренды отключена - 1; `duplicate` никогда не распознаётся - 3. После возврата полный набор снова 273/273.

Устройство (`E:\Projects\orbital-server\OrbitServer\`):
- **Окно W** (`Config.cs`). `maxSegmentAgeSeconds` по умолчанию 60 (было 15) и в коде, и в `serverconfig.json`. Граница прежняя (`PastBound`: `now - W × max(1, scale)`, первые `bootGraceSeconds` после старта - `min(now - W, resumeAt - W)` из 2c.1b, без изменений), одинакова для непрерывной сессии и после переподключения: от сессий и `hello` она не зависит, клиент её продлить не может.
- **Grace удара.** `impactGraceSeconds` стал необязательным: не задан - W + 5 (свойство `ImpactGraceSeconds` считает его из `ImpactGraceSetting`, имя в JSON прежнее). В `serverconfig.json` записано явно 65. Новая проверка `ValidateRetroWindow` (в `ServerConfig.Load` и в конструкторе `GameServer`): W конечное и больше 0, grace конечный и не меньше W, `segmentSeamToleranceSeconds` не меньше 0; иначе `InvalidDataException`, `Program.Main` возвращает 2.
- **Фиксация `destroyAt`.** Когда предсказанный удар впервые разрешён (`ReapLocked` видит аппарат разбитым на «сейчас»), в запись аппарата пишутся `impactAt` и `destroyAt = удар + grace × max(1, scale)` по масштабу в этот момент; запись сохраняется. Дальше `DestroyTimeOf`, `StateOf` и `NextDestruction` берут сохранённый срок. Каждый запрос и `SetTimeScale` сначала вызывают `ReapLocked` по старому масштабу, поэтому масштаб берётся тот, что действовал при ударе. Принятый сегмент стирает фиксацию (снапшот новый). Запас после старта (`bootGraceSeconds`, 2c.1b) применяется поверх фиксированного срока, как раньше (по масштабу в момент проверки).
- **Запись аппарата (`CraftLedger`, `Storage.cs`).** `ServerState.Ledgers`: `craftId`, `committedUntil`, `lastSeq`, `lastDigest`, `impactAt`, `destroyAt`. Поле необязательное, формат остался 4. Спавн создаёт запись с меткой = момент спавна и `lastSeq` 0; уничтожение аппарата удаляет запись. Аппарат из старого сохранения получает запись при загрузке (`Migrate`): метка = max(конец жжения, время записи ориентации) - это и есть конец последнего жжения, время последнего отчёта или спавн. Аппарат, добавленный кодом в обход спавна (тесты), до первого принятого сообщения (или до разрешения его удара) записи не имеет: метки нет (как до 2c.3b), `committedUntil` в ответах null.
- **Водяная метка.** `Evaluate` получает метку параметром: нижняя граница начала = max(метка, конец последнего жжения). Начало ниже неё более чем на `segmentSeamToleranceSeconds` (1e-3) - `before_committed`; в пределах допуска - сдвиг на метку с сохранением конца клиента (тот же механизм стыка, что в 2c.3, теперь и после отчёта ориентации и спавна). У `attitude` то же самое (время ниже на допуск сдвигается на метку). Закрыта дыра 2c.3: сегмент, начинающийся раньше уже принятого `attitude`, теперь `before_committed`. Порядок проверок: неизвестный аппарат -> `wrong_craft` -> проверки `seq` -> прежние проверки формата и границы возраста (`start_too_early`/`report_too_old`) -> метка -> физика. Принятие двигает метку: сегмент - до своего фактического конца (при касании внутри сегмента - до касания), отчёт - до своего времени.
- **Идемпотентность.** `ManeuverRequest` и `AttitudeReport` получили `seq` и `craftId`. Сравнение содержимого - SHA-256 от типа сообщения и сериализации прочитанного сервером объекта запроса (все поля, включая `seq`). `seq` растёт по общей очереди клиента (одна нумерация для `maneuver` и `attitude`, у каждого аппарата своя, после спавна снова с 1). Последний принятый номер и хеш хранятся в записи аппарата и переживают рестарт.
- **Обязательность `seq`.** Протокол (`Net.cs`) требует `seq` и `craftId` в `maneuver` и `attitude`: без `seq` - `bad_seq`, без `craftId` - `wrong_craft` (ответ в своём типе, с `state`, `lastSeq`, `committedUntil`). API `GameServer.Maneuver`/`ReportAttitude` принимает запрос без `seq` (номер не проверяется и не записывается, метка действует): это внутренний вызов сервера и около 100 вызовов в старых тестах, к протоколу он отношения не имеет. Старые WebSocket-тесты (9 вызовов в `NetTests`, `Stage14Tests`, `Stage2c3Tests`) поправлены: шлют `seq` и `craftId`.
- **Аренда управления** (`Net.cs`). У сессии флаг `HasControl`. Успешный `hello` под блокировкой списка сессий снимает флаг со всех других сессий того же аккаунта и ставит себе; потерявшим уходит push `control_taken`. Слушатель получает все push (`proximity_update`, `radio_message`, `craft_destroyed`, `craft_update`, `time_scale`; все они адресуются по аккаунту), может `get_state`, `radio_fetch`, `ping`, `get_world` и т.п. `maneuver`, `attitude`, `radio_send`, `spawn` отклоняются с `control_lost` в своём типе ответа. Вернуть управление - новый `hello` (решение: отдельная команда не нужна, `hello` и так отдаёт клиенту всё для пересинхронизации: `lastSeq`, `committedUntil`, снапшот, состояние). Если управляющая сессия закрылась, слушатели управления сами не получают (нужен `hello`). Аренда в памяти, после рестарта её нет. Админ-права сессии (2c.2) от аренды не зависят.
- **`spawn` в ожидании grace = «покинуть аппарат».** Если аппарат аккаунта летит и разрешён разбитым на «сейчас» (ждёт grace), `spawn` сначала проверяет запрос (точка старта и т.п.), затем уничтожает старый аппарат тем же `Destroy`, что и reaper в конце grace, и создаёт новый. Решение: да, с обломком и событиями, как при обычном уничтожении - удар в игровом времени уже был, игрок лишь отказывается от шанса спасти аппарат; другие игроки должны увидеть обломок так же, как через 65 с. Обломок со временем удара и причиной `impact`, `CraftsLost` +1, событие `Destroyed` с новым полем `Abandoned`, push `craft_destroyed` (с `abandoned: true`) своему аккаунту и `wreck_created` остальным. `spawn_result.abandonedCraftId`. Падающий (ещё не разбитый), летящий, стоящий аппарат и крушение `burn_touchdown` (уничтожается в конце жжения) по-прежнему дают `already_has_craft`.
- **Последствия по таблице проекта.** `burn_touchdown` окончателен сразу (без изменений с 2c.3: снапшот разбитый, сегмент раньше конца жжения не принимается меткой). Обломок, `craft_destroyed`, `wreck_created`, освобождение аккаунта - в `destroyAt` (или сразу при «покинуть»). Маршруты радио окончательны: доставки считаются в момент `radio_send` и позже не пересчитываются (тест). Push другим при принятии сегмента - новый `craft_update`. `crashed` + `destroyAt` у других - как в 2c.3 (`get_state`, `proximity_update`).
- **Push `craft_update`.** После каждого принятого `maneuver` всем вошедшим сессиям, кроме отправителя (и слушателям того же аккаунта): `{type, time, cause: "maneuver", craftId, seq, state}`, `state` - состояние на «сейчас». Нужен, потому что аппарат после обрыва может быть вне горячей группы других. Для принятого `attitude` push не шлётся (см. вопросы).
- **Топливо.** Только серверное, как и было: `rcsFuelUsed` считается от последней принятой записи ориентации (конец жжения или отчёт), принятие сдвигает эту точку; `duplicate` топливо не расходует (тест).
- **Ответы.** `ManeuverResult` и `AttitudeResult` получили `seq` (как пришёл), `lastSeq`, `committedUntil` (после запроса); `HelloResult` и `SpawnResult` - `lastSeq`, `committedUntil`; `hello_ok` ещё `control: true`. `GameServer.LedgerInfo(craftId)` - для сетевого слоя и тестов.

Изменённые старые тесты (все ожидаемые, с комментариями в коде):
- `ServerTests.cs` `BadRequests_AreRejectedWithReasons`, `NetTests.cs` `Hello_Maneuver_GetState_OverWebSocket`, `Stage2c3Tests.cs` `Seam_BeyondTheTolerance_...`: `overlapping_burn` -> `before_committed`.
- `Stage14Tests.cs`: `AttitudeReport_NotWhileLandedOrBurning` (`burn_in_progress` -> `before_committed`), `AttitudeReport_Rejections` (отчёт старше принятого: `report_too_old` -> `before_committed`), `Wire_...` (WebSocket: `seq`, `craftId`).
- `NetTests.cs`: `seq`/`craftId` в WebSocket-маневрах; в `Maneuver_TriggersProximityPush_ToNeighbourOnly` перед `proximity_update` может прийти `craft_update`, он пропускается.
- `Stage2c3Tests.cs`: тесты оставлены на числах 2c.3 явным конфигом (`MaxSegmentAgeSeconds = 15`, `ImpactGraceSeconds = 15`); в `ImpactGrace_WithoutASegment_...` убрана проверка `spawn` -> `already_has_craft` в окне grace (теперь это «покинуть», проверено в 2c.3b); в `Net_SpawnSnapshot_...` второй `hello` того же аккаунта забирает управление, поэтому первая сессия делает `hello` снова; `seq`/`craftId` в WebSocket-вызовах.
- `Stage2c1bTests.cs`: числа 2c.1b явным конфигом (W = grace = 15); в `BootGrace_SegmentAgeBound_...` аппарат создаётся в 900, а не в 1000: его спавн теперь зафиксирован, а тест шлёт сегмент, начатый в 986.
- `Stage2cTests.cs` `StartTolerance_ScalesWithTheTimeScale`: явный `MaxSegmentAgeSeconds = 15`.

Новые тесты `OrbitServer.Tests/Stage2c3bTests.cs` (17):
- конфиг: W 60, grace 65 по умолчанию, grace следует за W, если не задан (в коде и при загрузке файла), поставляемый `serverconfig.json` 60/65; отказ старта при grace < W (30 при 60; 65 при 100), W 0 и -5: `ServerConfig.Load` бросает с понятным текстом, `Program.Main` возвращает 2, конструктор `GameServer` отклоняет конфиг из кода; grace = W разрешён;
- метка: отчёт `attitude` принят в T-5, сегмент с началом T-10 - `before_committed` (с `state`, `committedUntil`, `lastSeq`), состояние побайтно прежнее; старший отчёт тоже `before_committed`; сегмент на 5e-4 раньше метки сдвинут на неё, конец клиента сохранён; внутри принятого жжения - `before_committed` и для сегмента, и для отчёта;
- метка = максимум: спавн (сегмент на 1 с раньше спавна отклонён), конец будущего жжения, время отчёта;
- `duplicate` (состояние побайтно прежнее, топливо RCS не списано второй раз), `seq_conflict` (другое содержимое и другой тип с тем же номером), `stale_seq`, `bad_seq` (0, -3), `wrong_craft` (сегмент и отчёт), отклонённый номер не расходуется, пропуски номеров разрешены, базовая точка RCS сдвигается;
- цепочка из 8 сегментов по 5 с, отправленная через 45 с: при W = 60 принята целиком (`lastSeq` 8, метка = конец цепочки), при W = 15 голова `start_too_early` и состояние не меняется, первые 6 элементов отклоняются;
- grace истёк: в t + 61 сегмент спасения уже `start_too_early`, в t + 65 уничтожение, затем `unknown_craft`, записи нет, `hello` без аппарата отдаёт `lastSeq`/`committedUntil` null;
- `destroyAt` фиксирован: после разрешения удара масштаб 10 не двигает срок (t + 65, а не t + 650), уничтожение по фиксированному сроку;
- `spawn` в grace: неверная точка ничего не уничтожает, верный `spawn` уничтожает старый аппарат сразу (`abandoned`, время удара, обломок, `CraftsLost`), здоровый и падающий аппарат - `already_has_craft`;
- радио: маршрут, посчитанный при отправке, не меняется после принятого задним числом сегмента, свой `radio_send` идёт в «сейчас»;
- рестарт: kill -9 после `maneuver` seq 1 и `attitude` seq 2, после старта `hello` отдаёт `lastSeq` 2 и метку, повтор - `duplicate` (топливо не списано), seq 1 - `stale_seq`, сегмент до метки - `before_committed`, сегмент от метки принят; фиксированный `destroyAt` сохранён в файле и после рестарта со сменой масштаба тот же;
- WebSocket, аренда: второй `hello` - `control: true`, первая сессия получает `control_taken`, её `maneuver`/`attitude`/`radio_send`/`spawn` - `control_lost` в своих типах ответа, `get_state` и `radio_fetch` работают, push `craft_update` от управляющей сессии получают слушатель и другой игрок (не отправитель); новый `hello` возвращает управление, вторая сессия получает `control_taken` и `control_lost`; без `seq` - `bad_seq`, без `craftId` и с чужим - `wrong_craft`;
- WebSocket, обрыв сокета на 20 с посреди жжения: первый сегмент (seq 1) принят вживую, сокет оборван, через 25 с новое подключение, `hello_ok` `lastSeq` 1 и метка 1005, повтор seq 1 - `duplicate`, сегменты 2..5 приняты, другой игрок получил 5 `craft_update`;
- WebSocket, спасение от удара: аппарат висит на 20 м, сокет оборван, вторая сессия в t + 5 видит `crashed`, `crashedAt` t, `destroyAt` t + 65; через t + 20 переподключение и посадочные сегменты от момента обрыва (тяга чуть меньше веса, по 5 с, несколько штук) - последний `landed`; вторая сессия получила `craft_update` по каждому, последний `landed`, `get_state` `landed`; в t + 75 обломков нет, `craft_destroyed`/`wreck_created` не приходили;
- WebSocket: окно одинаково для сессии без обрыва и после переподключения (W + 1 назад - отказ, W - 1 - принят, у обоих);
- WebSocket: `spawn` в grace - `abandonedCraftId`, `craft_destroyed` с `abandoned: true` себе, `wreck_created` другому.

Протокол (числа для примера; имена полей и коды - как в коде и тестах):
```json
{"type":"maneuver","id":7,"seq":12,"craftId":"craft-1","direction":[1,0,0],"throttle":0.71,"startTime":1005,"duration":5}
{"type":"maneuver_result","id":7,"result":{"accepted":true,"reason":null,"startTime":1005,"endTime":1010,...,"stateAtEnd":{...},
  "state":null,"version":4,"seq":12,"lastSeq":12,"committedUntil":1010}}

{"type":"maneuver_result","id":8,"result":{"accepted":false,"reason":"before_committed",
  "message":"start is before the committed watermark 1010",...,"state":{"craftId":"craft-1",...},"seq":13,"lastSeq":12,"committedUntil":1010}}
{"type":"maneuver_result","id":9,"result":{"accepted":false,"reason":"duplicate","message":"seq 12 was already accepted; nothing changed",
  ...,"state":{...},"seq":12,"lastSeq":12,"committedUntil":1010}}

{"type":"attitude","id":10,"seq":13,"craftId":"craft-1","time":1012,"orientation":[1,0,0,0],"omega":[0,0,0],"rcsFuelUsed":0.4}
{"type":"attitude_result","id":10,"result":{"accepted":true,"reason":null,"rcsFuelUsed":0.4,"rcsFuelRemaining":196.6,"state":{...},
  "version":5,"seq":13,"lastSeq":13,"committedUntil":1012}}

{"type":"hello_ok","id":1,"nickname":"Eagle","craftId":"craft-1",...,"snapshot":{...},"state":{...},"lastSeq":13,"committedUntil":1012,"control":true}
{"type":"control_taken","time":1020,"craftId":"craft-1","message":"another session of this account took the control; send hello to take it back"}
{"type":"maneuver_result","id":4,"result":{"accepted":false,"reason":"control_lost","message":"another session of this account has the control; send hello to take it back",...}}
{"type":"spawn_result","id":5,"result":{"ok":false,"reason":"control_lost",...}}

{"type":"craft_update","time":1030,"cause":"maneuver","craftId":"craft-1","seq":14,"state":{"craftId":"craft-1","mode":"landed",...}}

{"type":"spawn_result","id":6,"result":{"ok":true,"craftId":"craft-2",...,"lastSeq":0,"committedUntil":1040,"abandonedCraftId":"craft-1"}}
{"type":"craft_destroyed","craftId":"craft-1","reason":"impact","time":1032.1,...,"wreck":{...},"abandoned":true}
```
Коды отказа `maneuver_result`/`attitude_result`, новые: `before_committed`, `duplicate` (не ошибка: элемент уже принят, клиент снимает его с очереди как подтверждённый), `seq_conflict`, `stale_seq`, `bad_seq`, `wrong_craft`, `control_lost`; ушли из оборота `overlapping_burn`, `burn_in_progress` и `report_too_old` с текстом «a newer attitude is already recorded» (все стали `before_committed`; `report_too_old` остался для границы окна). `radio_send_result` и `spawn_result`: `control_lost`.

Запись в файле состояния:
```json
"ledgers": [{"craftId": "craft-1", "committedUntil": 1012, "lastSeq": 13, "lastDigest": "5f0c...e1", "impactAt": null, "destroyAt": null}]
```
Настройки (`OrbitServer/serverconfig.json`): `"maxSegmentAgeSeconds": 60, "segmentSeamToleranceSeconds": 0.001, "impactGraceSeconds": 65` (если `impactGraceSeconds` убрать, будет W + 5).

Решения по умолчанию (приняты исполнителем, отметить, если не подходят):
1. `duplicate` возвращается как `accepted: false` с кодом `duplicate`, а не как принятие: результата первого приёма сервер не хранит, а клиент по коду понимает, что элемент уже у сервера. Клиент 2c.4 должен считать `duplicate` подтверждением, а не отказом (иначе он очистит очередь).
2. Номер расходуется только принятым сообщением, пропуски номеров разрешены (номер должен быть больше последнего принятого). Так клиент после отказа может не перенумеровывать очередь.
3. Нумерация `seq` у каждого аппарата своя (запись уходит вместе с аппаратом, после спавна снова с 1); `wrong_craft` отсекает сообщения для старого аппарата.
4. `seq` обязателен в протоколе и необязателен во внутреннем API (обоснование выше).
5. Все нарушения метки дают один код `before_committed` (как в проекте), включая бывшие `overlapping_burn` и `burn_in_progress`; различие - только в тексте `message`.
6. Возврат управления - только новый `hello`.
7. «Покинуть аппарат» - с обломком и событиями.
8. `craft_update` - только после принятого сегмента.
9. `impactGraceSeconds` не задан - W + 5 (решение пользователя «grace = W + 5 по умолчанию» понято как вычисляемое значение по умолчанию; в поставляемом файле записано явно).

Что изменено:
- `E:\Projects\orbital-server\OrbitServer\Config.cs`: `MaxSegmentAgeSeconds` 60, `ImpactGraceSeconds`/`ImpactGraceSetting` (W + 5), `ValidateRetroWindow`.
- `OrbitServer/Storage.cs`: `ServerState.Ledgers`, класс `CraftLedger`.
- `OrbitServer/Maneuvers.cs`: `ManeuverRequest.Seq`/`CraftId`, `ManeuverResult.Seq`/`LastSeq`/`CommittedUntil`, метка в `Evaluate` (`before_committed`, стык с меткой).
- `OrbitServer/GameServer.cs`: поля `HelloResult`, `SpawnResult`, `AttitudeReport`, `AttitudeResult`, `CraftDestroyedEvent.Abandoned`; запись аппарата (`LedgerOf`, `NewLedger`, `Commit`, `CheckSeq`, `Digest`, `LedgerInfo`), миграция, фиксация `destroyAt` (`FixImpact`, `FixedDestroyTime`), метка и `seq` в `Maneuver` и `ReportAttitude`, «покинуть» в `Spawn`.
- `OrbitServer/Net.cs`: аренда (`HasControl`, `TakeControl`, `ControlLost`, push `control_taken`), обязательные `seq`/`craftId`, push `craft_update`, `hello_ok.lastSeq/committedUntil/control`, `craft_destroyed.abandoned`.
- `OrbitServer/serverconfig.json`: `maxSegmentAgeSeconds` 60, `impactGraceSeconds` 65.
- `OrbitServer.Tests/Stage2c3bTests.cs` - новый; `ServerTests.cs`, `NetTests.cs`, `Stage14Tests.cs`, `Stage2c3Tests.cs`, `Stage2c1bTests.cs`, `Stage2cTests.cs` - правки выше.
- `README.md` сервера: настройки, метка, идемпотентность, аренда, «покинуть», `craft_update`, число тестов.
- В репозитории orbital: этот файл (подраздел и число тестов в «Где что лежит»; строку статуса 2c в таблице не трогал - её обновит координатор), `Docs/Stage2c-Downtime-Retro.md` (строка статуса раздела B).

Не проверено:
- Реальный сервер (`dotnet run`) руками не запускался; сеть проверена только тестами на loopback, обрыв - `ClientWebSocket.Abort()` на той же машине (настоящий разрыв сети, NAT, полуоткрытые TCP-соединения не моделировались).
- Срабатывание таймера reaper по реальному времени в фиксированный `destroyAt` тестом не проверено (тесты на `ManualClock` вызывают reap запросом или `Reap()`); таймер взводится от `NextDestruction`, которая берёт фиксированный срок.
- Гонка двух одновременных `hello` одного аккаунта не тестировалась (флаг меняется под блокировкой списка сессий, победит последний, вошедший в неё).
- Клиент (`Uplink`, очередь с `seq`, обработка `duplicate`/`control_taken`, бюджет обрыва и блокировка тяги по решению пользователя п. 3, баннеры) - 2c.4+; чек-лист Editor из проекта (обрыв сети 20 с при посадке, остановка сервера на 2 мин) не выполнялся.

Открытые вопросы:
1. `bootGraceSeconds` (2c.1b) по-прежнему считается по масштабу в момент проверки и может продлить даже фиксированный `destroyAt`, если масштаб после рестарта поднять. Фиксировать и его в момент старта?
2. `craft_update` шлётся только после сегмента. Отчёт `attitude` меняет только ориентацию и расход RCS; если другим (или слушателям своего аккаунта) нужна ориентация без жжения, добавить push и для него просто.
3. `craft_update` уходит всем вошедшим, без фильтра по расстоянию: при многих игроках это много трафика. Возможно, ограничить горячей группой плюс аппаратами, которые видели этот аппарат разбитым.
4. После закрытия управляющей сессии управление никому не передаётся, пока кто-то не сделает `hello`. Нужна ли автоматическая передача последнему слушателю?
5. Аренда не сохраняется: после рестарта сервера первый `hello` любой сессии берёт управление (клиенты всё равно переподключаются с `hello`).
6. Достижения «по горизонту `now - W`» не делались: достижений на сервере ещё нет.
