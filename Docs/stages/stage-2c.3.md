<!-- Журнал этапа вынесен дословно из Docs/GameplayPrototype.md 2026-11-03; вход и статус: Docs/GameplayPrototype.md -->

#### 2c.3 Сервер: протокол под поток сегментов: принят координатором 2026-10-30

Журнал (исполнитель). Git-коммитов нет, файлы Unity и `Assets/Orbital/Core` не трогались, админ-канал (2c.2) не делался.
- `dotnet test` в `E:\Projects\orbital-server\OrbitServer.Tests`: пройдено 220, не пройдено 0 (было 207; +13 в новом `Stage2c3Tests.cs`). После первых правок упали 9 старых тестов, все ожидаемо (разбор ниже), исправлены явно.
- `dotnet test` в `E:\Projects\orbital\Tools\OrbitMathTests`: пройдено 571, не пройдено 0 (Core не менялся, прогон для контроля).
- Проверка, что новые тесты настоящие: если убрать условие grace в `ReapLocked`, падают 3 теста grace; если отключить стыковку (любое `start < Burn.End` - отказ), падают 3 теста стыка. Правки возвращены, набор снова 220/220.

Устройство (`E:\Projects\orbital-server\OrbitServer\`):
- **Возраст сегмента.** Новая настройка `maxSegmentAgeSeconds` (15 реальных с, в игровых × max(1, scale)) заменила прошлую границу `start_too_early` (раньше `startToleranceSeconds` = 5). Граница в будущее у сегмента и раньше была не `startToleranceSeconds`, а `maxLeadSeconds` (600), она не менялась. `startToleranceSeconds` теперь только граница отчёта ориентации в будущее (`report_in_future`).
- **Отчёт ориентации получил ту же границу в прошлое.** Раньше у `attitude` не было абсолютной границы: принимался любой отчёт не старше последней записи ориентации, и старый отчёт мог переписать ориентацию и расход RCS задним числом, хотя другие игроки это уже видели. Теперь `time < now - maxSegmentAge` даёт `report_too_old` (код тот же, что у «есть запись новее», различается сообщением: «more than ... s in the past»).
- **Стык.** Если `Burn.End - segmentSeamToleranceSeconds <= start < Burn.End` (допуск 1e-3 игровой секунды, не масштабируется), начало сдвигается на `Burn.End`, а конец клиента `start + duration` сохраняется: длительность уменьшается на зазор, расход основного топлива считается по новой длительности (`mdot * duration`), `rcsFuelUsed` клиента не меняется (он считается до конца сегмента, а конец тот же). Так следующий стык остаётся выровненным по цепочке клиента и сдвиг не копится. Фактическое начало приходит в `result.startTime`. Зазор больше допуска даёт `overlapping_burn`; сегмент, который кончается не позже конца предыдущего жжения, даёт `bad_duration`.
- **Grace удара.** Настройка `impactGraceSeconds` (15 реальных с, в игровых × max(1, scale) по масштабу в момент проверки). Reaper уничтожает аппарат с предсказанным баллистическим ударом только при `crashedAt + grace <= now` (`DestroyTimeOf`). Крушение, решённое самим сегментом (касание с превышением скорости под тягой, `burn_touchdown`, снапшот уже `crashed`), уничтожается как раньше, в конце жжения: позже пришедший сегмент его отменить не может (он начинается не раньше конца жжения). `NextDestruction` возвращает `удар + grace`, таймер reaper в `Net.cs` взводится на это время и перевзводится после каждого принятого маневра (маневр, отменивший удар, снимает или переносит срок).
- **Что видно в окне grace (решение).** Аппарат остаётся в мире. Снапшот прежний (летящий, с запланированным ударом), а всё, что считает состояние на «сейчас», видит его разбитым в точке удара: так снапшот разрешается, и так было бы без grace, только дольше. В состоянии появились поля `crashedAt` (время удара) и `destroyAt` (время уничтожения); они заполнены только у разбитого, ещё не уничтоженного аппарата (`GameServer.StateOf`). Одинаково для `get_state`, `hello_ok.state`, `proximity_update`, ответов `maneuver_result`/`attitude_result`. Радио: сам аппарат передавать не может (`craft_crashed`, как раньше); доставки к нему создаются и удаляются при уничтожении вместе с ящиком. Аккаунт держит аппарат до `destroyAt`: `spawn` отвечает `already_has_craft`. Обломок, событие `Destroyed`, push `craft_destroyed` (себе) и `wreck_created` (остальным) появляются только в `destroyAt`; время обломка и события - время удара.
- **Отмена удара.** Сегмент со стартом раньше удара, пришедший в окне grace (и не старше `maxSegmentAge`), проходит обычную проверку: состояние на старт ещё летящее (`ResolveTo(s, start)`), новый снапшот планируется от конца жжения заново, удар исчезает или переносится. Сегмент со стартом после удара даёт `craft_crashed`, отчёт ориентации на «сейчас» тоже.
- **Отказы несут состояние.** `ManeuverResult.State` (новое поле) и `AttitudeResult.State` при отказе - текущее состояние аппарата в форме `get_state` (у `attitude_result` при отказе ещё `version`). При `unknown_craft` null. У принятого маневра `state` = null (есть `stateAtEnd`), у принятого отчёта ориентации `state` как раньше.
- **Снапшот в `spawn_result`.** `SpawnResult.Snapshot` - копия снапшота нового аппарата (тот же JSON, что в `hello_ok.snapshot`, с `spec.flight`). При отказе null.
- **`get_state` без своего аппарата.** Нужен только `hello`. Без аппарата по-прежнему `no_craft`: `maneuver`, `attitude` (действуют за свой аппарат), `radio_send` (передаёт свой аппарат), `radio_fetch` (ящик принадлежит аппарату). `get_world`, `get_wrecks`, `get_start_points`, `ping` работают и до `hello`.

Изменённые старые тесты (все ожидаемые, с комментариями в коде):
- `Stage12Tests.cs`: помощник `Crash`, тесты `Spawn_IsExplicit_...` и `Crash_CreatesExactlyOneWreck_...` ждали удар + 1 с; теперь удар + grace + 1 (константа `Grace` из конфига по умолчанию).
- `NetTests.cs`: `Crash_PushesCraftDestroyed_...` и `Hello_ForAccountWithoutCraft_...` - то же; в первом `get_state` без аппарата теперь отвечает `states` с аппаратом второго игрока вместо `no_craft`.
- `Stage2cTests.cs`, `StartTolerance_ScalesWithTheTimeScale`: опирался на старую границу 5 с. При масштабе 1 отказ на 30 с остаётся (граница 15 с); при масштабе 10 граница 150 игровых с: отказ теперь на 160 с (было 60), принят на 140 с (было 30); часы сдвинуты на 20 реальных с вместо 10, чтобы старт был позже снапшота.
- Тест с `T - 100` (`ServerTests`, `start_too_early`) и отчёт ориентации на `T - 50` (`Stage14Tests`, `report_too_old`) проходят без изменений.

Новые тесты `OrbitServer.Tests/Stage2c3Tests.cs` (13):
- возраст: сегмент 12 с в прошлом принят, 20 с отказ; на масштабе 10 принят 120 с, 200 с отказ; граница в будущее прежняя (`maxLeadSeconds`); отчёт ориентации: та же граница в прошлое на масштабах 1 и 10;
- стык `end - 1e-9`: начало ровно `Burn.End`, конец клиента сохранён, результат (конец, топливо, положение, скорость, состояние) совпадает со случаем точного стыка в пределах 2e-9 с и 1e-3 м; стык с сохранённым концом (`start = end - 1e-9`, `duration = 3 + 1e-9`) совпадает с точным; зазор 2e-3 даёт `overlapping_burn` (с `state`), сегмент короче зазора `bad_duration`;
- grace: аппарат на 50 м без вертикальной скорости, удар в T; в T+5 состояние `crashed` с `crashedAt` = T и `destroyAt` = T+15; сегмент со стартом T-3, пришедший в T+5, принят, аппарат летит и в T+16 не уничтожен;
- без сегмента: в T+15-0.01 ничего (аппарат в мире, `spawn` даёт `already_has_craft`, `hello_ok.state` разбитый с `destroyAt`, сегмент после удара и отчёт ориентации дают `craft_crashed`); в T+15 одно событие, обломок со временем T, аккаунт свободен, `NextDestruction` пуст; на масштабе 10 срок T + 150; `burn_touchdown` без grace;
- отказы несут `state` (совпадает с `get_state`), при `unknown_craft` null, у принятого маневра null; `spawn` несёт копию снапшота с `spec.flight`;
- WebSocket: `spawn_result.result.snapshot.spec.flight` побайтно равен `hello_ok.snapshot.spec.flight`; отказы `maneuver` и `attitude` несут `state`; `get_state` в окне grace показывает `crashed`, `destroyAt`, `crashedAt`, push `craft_destroyed` ещё не пришёл; после grace `get_state` без аппарата отдаёт чужой аппарат, приходит `craft_destroyed` со временем удара, `maneuver`, `attitude`, `radio_fetch` дают `no_craft`.

Протокол (новые поля; числа для примера):
```
-> {"type":"maneuver","id":7,"direction":[0,1,0],"throttle":1,"startTime":1004.999999999,"duration":5}
<- {"type":"maneuver_result","id":7,"result":{"accepted":true,"reason":null,"startTime":1005,"endTime":1009.999999999,
     "fuelUsed":679.6,...,"stateAtEnd":{...},"state":null,"version":3}}

-> {"type":"maneuver","id":8,"direction":[0,1,0],"duration":1,"startTime":980}
<- {"type":"maneuver_result","id":8,"result":{"accepted":false,"reason":"start_too_early",
     "message":"start time is more than 15 s in the past",...,"state":{"craftId":"craft-1","time":1000,"mode":"flying",...}}}

-> {"type":"attitude","id":9,"orientation":[1,0,0,0],"time":980}
<- {"type":"attitude_result","id":9,"result":{"accepted":false,"reason":"report_too_old",
     "message":"report time is more than 15 s in the past","state":{...},"version":3}}

-> {"type":"get_state","id":10}
<- {"type":"states","id":10,"time":1020,"states":[{"craftId":"craft-1","mode":"crashed",...,
     "crashedAt":1012.4,"destroyAt":1027.4},{"craftId":"craft-2","mode":"flying",...,"crashedAt":null,"destroyAt":null}]}

-> {"type":"spawn","id":11}
<- {"type":"spawn_result","id":11,"result":{"ok":true,"craftId":"craft-3","startPointId":"default","point":{...},
     "state":{...},"snapshot":{"id":"craft-3",...,"spec":{"dryMass":5000,...,"flight":{...}},...},
     "archiveAvailable":0,"archiveInTransit":0}}
```
Настройки (`serverconfig.json`): `"maxSegmentAgeSeconds": 15, "segmentSeamToleranceSeconds": 0.001, "impactGraceSeconds": 15`.

Что изменено (`E:\Projects\orbital-server\`):
- `OrbitServer/Config.cs`: `MaxSegmentAgeSeconds`, `SegmentSeamToleranceSeconds`, `ImpactGraceSeconds`; комментарий `StartToleranceSeconds`.
- `OrbitServer/Maneuvers.cs`: граница возраста параметром `Evaluate` (`maxAge`), стык, `ManeuverResult.State`.
- `OrbitServer/GameServer.cs`: `ImpactGrace`, `DestroyTimeOf`, `StateOf`, grace в `ReapLocked` и `NextDestruction`, `state` в отказах маневра и ориентации, граница возраста отчёта ориентации, `SpawnResult.Snapshot`.
- `OrbitServer/Craft.cs`: `CraftState.CrashedAt`, `CraftState.DestroyAt`.
- `OrbitServer/Net.cs`: `get_state` без аппарата.
- `OrbitServer/serverconfig.json`: три поля.
- `OrbitServer.Tests/Stage2c3Tests.cs` новый; `Stage12Tests.cs`, `NetTests.cs`, `Stage2cTests.cs` с правками выше.
- `README.md`: протокол и конфиг.
- В репозитории orbital: этот файл.

Не проверено:
- Реальный сервер (`dotnet run`) руками не запускался; сеть проверена только тестами на loopback.
- Срабатывание таймера reaper в конце grace по реальному времени тестом не проверено: тесты на `ManualClock` вызывают reap запросом (`get_state`) или `Reap()` явно. Механизм таймера прежний, меняется только время взвода.
- Удар после запланированной смены сферы влияния `NextDestruction` по-прежнему не видит (как до 2c.3): такой аппарат уничтожится на ближайшем запросе после `удар + grace`, а не по таймеру.
- Клиент новые поля пока не читает (2c.4-2c.6).

Открытые вопросы:
1. Grace считается по масштабу в момент проверки: если масштаб меняется в окне grace, срок `destroyAt` в игровом времени меняется (при переходе 1 -> 10 с 15 до 150 с). Для прототипа приемлемо; если нужен неизменный срок, его можно фиксировать при первом разрешении удара.
2. Окно grace держит аккаунт: 15 реальных секунд после удара игрок не может сделать `spawn`. Если это мешает, можно разрешить `spawn` при ожидании уничтожения и уничтожать старый аппарат сразу.
3. Сегмент из прошлого (до 15 с) по-прежнему может начинаться раньше последнего принятого отчёта ориентации (`attitude`); сервер это не запрещает (так было и с 5 с). Если клиент шлёт отчёты только между жжениями, конфликта нет. Проверка «старт не раньше записи ориентации» - кандидат на 2c.4/2c.5 после e2e.
4. Отказ `report_too_old` теперь означает две вещи (старше `maxSegmentAge` или старше записи ориентации); различаются сообщением. Если клиенту нужно различать машинно, можно ввести отдельный код.
5. Принятый `maneuver_result` не несёт `state` на «сейчас» (только `stateAtEnd`); если реконсиляции клиента это понадобится, добавить просто.
