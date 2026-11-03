<!-- Журнал этапа вынесен дословно из Docs/GameplayPrototype.md 2026-11-03; вход и статус: Docs/GameplayPrototype.md -->

#### 2c.2 Сервер: админ-канал и защита: принят координатором 2026-10-30

Решения пользователя (2026-10-29, раздел 4 плана): права админа на сессию (`admin_auth` после `hello`), не флаг аккаунта; произвольный `spawn {point}` только админу; секрет хранится файлом вне обоих репозиториев (`E:\Projects\orbital-secrets\admin-secret.txt`), путь задаётся настройкой, переменная окружения переопределяет файл. Секрет внутри `serverconfig.json` не поддерживается (вариант убран из плана).

Журнал (исполнитель). Git-коммитов нет, файлы Unity и `Assets/Orbital/Core` не трогались.
- `dotnet test` в `E:\Projects\orbital-server\OrbitServer.Tests`: пройдено 238, не пройдено 0 (было 220; +18 в новом `Stage2c2Tests.cs`). Старые тесты не правились. Прогнано дважды.
- `dotnet test` в `E:\Projects\orbital\Tools\OrbitMathTests`: пройдено 571, не пройдено 0 (Core не менялся, прогон для контроля).
- Создан файл секрета `E:\Projects\orbital-secrets\admin-secret.txt`: 32 байта из криптографического генератора (`RandomNumberGenerator`), base64url, 43 символа, без перевода строки. Значение сгенерировано командой PowerShell, которая его не печатает; в отчёт, документы и логи оно не попадало. Рядом `README.txt`: что это, правила (не коммитить, не копировать, права на файл, SSH-туннель), как сменить секрет. Папка не лежит ни в одном git-репозитории (`git rev-parse` даёт «not a git repository»).
- Проверка, что значение нигде не всплыло (печаталось только число файлов, `grep -l -F -f <файл секрета>`): репозиторий orbital (`git grep --untracked`, плюс `Docs`, `Tools`, `Assets`, `UserSettings`, `ProjectSettings`) - 0 файлов; orbital-server целиком, включая `bin/` и `obj/` - 0; scratchpad агента - 0; в папке секретов - 1 (сам файл).
- Ручной запуск собранного `OrbitServer.exe` с `admin.enabled: true` и настоящим файлом (временный конфиг и данные в scratchpad, порт 8799, 4 с): сервер стартует, печатает `Admin channel: ENABLED (loopback connections only), secret from file E:\Projects\orbital-secrets\admin-secret.txt, audit ...\admin-audit.log`. Подключение к этому запуску не проверялось (протокол проверен тестами).

Устройство (`E:\Projects\orbital-server\OrbitServer\`):
- **`Admin.cs` (новый).** `AdminConfig` (секция `admin` конфига), `AdminGate` (секрет, сравнение, ограничения частоты, аудит), `AdminSessionState` (права и бюджет команд сессии), `AuditLog` и `AuditEntry` (JSON lines).
- **Источник секрета.** Сначала переменная окружения `admin.secretEnvVar` (по умолчанию `ORBIT_ADMIN_SECRET`), если она задана и не пуста; иначе файл `admin.secretFile`. Содержимое обрезается (`Trim`: пробелы, табуляция, переводы строк). Минимум `minSecretLength` (16; меньше 16 настроить нельзя). Если переменная задана, но коротка, сервер не стартует (молча на файл не переключается). Относительный `secretFile` разрешается от папки файла настроек при загрузке (`ServerConfig.Load`); в `serverconfig.json` записан абсолютный путь с прямыми слэшами (`E:/Projects/orbital-secrets/admin-secret.txt`, без экранирования). Значение по умолчанию в коде - тот же путь.
- **Отказ старта.** `admin.enabled` и нет годного секрета (нет файла, файл не читается, нет ни файла, ни переменной, короче 16 символов): `AdminGate` бросает `InvalidDataException`; `Program.Main` печатает в stderr `OrbitServer: <сообщение>` и возвращает код 2 до загрузки мира. Сообщение называет путь файла или имя переменной, но никогда содержимое. Пример: `OrbitServer: admin channel is enabled but the secret file is missing: E:\...\admin-secret.txt (create it, fix admin.secretFile or set ORBIT_ADMIN_SECRET)`.
- **Хранение и сравнение.** В памяти держится только SHA-256 секрета (строка после чтения не сохраняется). Кандидат хешируется SHA-256 и сравнивается `CryptographicOperations.FixedTimeEquals` с хешем: время не зависит ни от длины, ни от содержимого кандидата.
- **Порядок проверок `admin_auth`:** нет `hello` (`not_logged_in`) -> канал выключен (`admin_disabled`) -> адрес не loopback при `loopbackOnly` (`not_loopback`; IPv4-mapped IPv6 приводится к IPv4, `::1` и вся 127.0.0.0/8 - loopback) -> IP заблокирован (`rate_limited` с `retryAfterSeconds`, даже с верным секретом) -> сравнение (`bad_secret` или успех). Неудача считается по IP: `maxAuthFailures` (5) неудач за `authFailureWindowSeconds` (60) блокируют IP на `lockoutSeconds` (300); успех сбрасывает счётчик. Попытки с внешнего адреса при `loopbackOnly` неудачами не считаются (до сравнения не доходят). Время ограничений - реальное монотонное (`Stopwatch`), в тестах подставляется.
- **Права - свойство сессии.** Успешный `admin_auth` ставит `IsAdmin` текущему WebSocket-соединению; повторный `hello` под другим ником их не снимает (права не привязаны к аккаунту); новое соединение начинает без прав.
- **Бюджет команд.** `commandsPerMinute` (30) на сессию в скользящей минуте; считаются `admin_set_time_scale`, `admin_status` и привилегированный `spawn {point}`; `admin_auth` не считается (у него свой предел по IP).
- **Команды.** `admin_set_time_scale {scale}` вызывает `GameServer.SetTimeScale` (диапазон - прежние `minTimeScale`/`maxTimeScale` верхнего уровня конфига, дублировать их в секции `admin` не стал); push `time_scale` всем открытым сессиям уходит существующим обработчиком 2c.1. `scale` принимается числом; строки `"NaN"`, `"Infinity"`, нечисловые строки, `null` и отсутствие поля дают `bad_scale` (JSON-число NaN не бывает). `admin_status` отдаёт время, масштаб, `since`, границы масштаба, число сессий (всего, вошедших, админов) и аппаратов. Неизвестный `admin_*` - обычный `error unknown_type`.
- **Произвольный спавн.** `allowCustomSpawn` верхнего уровня оставлен (по умолчанию false) как отдельный флаг для локальных плейтестов: с ним любой игрок ставит аппарат в точку из `allowedSpawnBodies`. Новое: `GameServer.Spawn(..., privileged)`. `spawn {point}` из админ-сессии при `admin.allowCustomSpawn` (по умолчанию true) привилегирован: общий флаг не нужен и список `allowedSpawnBodies` не применяется (любое тело с поверхностью; широта и долгота проверяются как раньше). Такой спавн расходует бюджет команд и пишется в аудит. `get_start_points.allowCustomSpawn` по-прежнему показывает только общий флаг.
- **Аудит.** `admin.auditLogFile` (`admin-audit.log`; относительный путь - рядом с файлом данных `dataFile`; пустая строка - без аудита). Пишется каждая админ-команда вошедшей сессии при включённом канале, в том числе отказы (`bad_secret`, `rate_limited`, `admin_required`, `bad_scale`), и привилегированный спавн. Поля: `utc`, `gameTime`, `remote`, `account` (ключ аккаунта), `command`, `args` (аргументы как пришли; у `admin_auth` - null: ни верный, ни неверный секрет не записываются), `ok`, `reason`. Ошибка записи аудита не валит запрос: в stderr уходит только тип исключения.
- **Консоль.** После строки `listening` печатается режим: `Admin channel: disabled` или `Admin channel: ENABLED (loopback connections only | any address, no TLS), secret from file <путь> | environment variable <имя>, audit <путь>`.
- **`Net.cs`.** Сессия получила `Remote` (адрес из `HttpListenerRequest.RemoteEndPoint`) и `Admin`. Запросы `admin_*` разбираются отдельно (`DispatchAdmin`) до общей проверки входа, чтобы отказ `not_logged_in` приходил в их собственном типе ответа. `NetServer(game, admin = null)`: без явного `AdminGate` он строится из конфига.

Протокол (все команды после `hello`):

```json
{"type":"admin_auth","id":1,"secret":"<секрет>"}
{"type":"admin_auth_result","id":1,"ok":true,"reason":null,"retryAfterSeconds":null}
{"type":"admin_auth_result","id":2,"ok":false,"reason":"bad_secret","retryAfterSeconds":null}
{"type":"admin_auth_result","id":7,"ok":false,"reason":"rate_limited","retryAfterSeconds":299.8}

{"type":"admin_set_time_scale","id":3,"scale":10}
{"type":"admin_result","id":3,"command":"admin_set_time_scale","ok":true,"scale":10,"previous":1,"time":1000,"changed":true}
{"type":"time_scale","scale":10,"time":1000,"previous":1}
{"type":"admin_result","id":4,"command":"admin_set_time_scale","ok":false,"reason":"bad_scale","message":"scale must be within [0.1, 100]"}
{"type":"admin_result","id":5,"command":"admin_set_time_scale","ok":false,"reason":"admin_required","message":null}

{"type":"admin_status","id":6}
{"type":"admin_result","id":6,"command":"admin_status","ok":true,"status":{"time":1000,"scale":10,"since":1000,"minScale":0.1,"maxScale":100,"sessions":2,"loggedIn":2,"admins":1,"crafts":0}}
```

Коды отказа: `admin_auth_result` - `not_logged_in | admin_disabled | not_loopback | rate_limited | bad_secret`; `admin_result` - `not_logged_in | admin_required | rate_limited | bad_scale`; привилегированный `spawn` дополнительно может получить `spawn_result.result.reason = "rate_limited"`.

Строка аудита:

```json
{"utc":"2026-10-30T09:12:44.1234567Z","gameTime":1000,"remote":"127.0.0.1","account":"boss","command":"admin_set_time_scale","args":{"scale":10},"ok":true,"reason":null}
```

Секция конфига (`OrbitServer/serverconfig.json`):

```json
"admin": {
  "enabled": false,
  "secretFile": "E:/Projects/orbital-secrets/admin-secret.txt",
  "secretEnvVar": "ORBIT_ADMIN_SECRET",
  "loopbackOnly": true,
  "minSecretLength": 16,
  "maxAuthFailures": 5,
  "authFailureWindowSeconds": 60,
  "lockoutSeconds": 300,
  "commandsPerMinute": 30,
  "auditLogFile": "admin-audit.log",
  "allowCustomSpawn": true
}
```

Тесты `OrbitServer.Tests/Stage2c2Tests.cs` (18; секреты - случайные строки во временных файлах, настоящий файл секрета тесты не читают; переменные окружения - уникальные имена):
- выключено по умолчанию (`ServerConfig`, `AdminGate.Describe`, `admin_disabled`; поставляемый `serverconfig.json` выключен и указывает на согласованный путь);
- обрезка содержимого файла и сравнение кандидатов разной длины (null, пусто, 1 символ, без последнего символа, с лишним символом, верхний регистр, 100000 символов);
- `not_loopback` для внешних адресов (юнит-тест гейта с `IPAddress`), loopback в формах `::1`, `::ffff:127.0.0.1`, `127.0.0.2`; внешний адрес не блокирует loopback; `loopbackOnly: false` пускает внешний адрес;
- блокировка после 5 неудач даже с верным секретом, `retryAfterSeconds` 300, блокировка по IP, снятие через 300 с; неудачи из разных минутных окон не складываются (подставные часы);
- бюджет 30 команд в скользящую минуту, у каждой сессии свой;
- отказ старта: нет ни файла, ни переменной; нет файла (в сообщении путь); короткий секрет (путь в сообщении, содержимого нет); файл из одних пробелов; `minSecretLength` меньше 16;
- `Program.Main` с временным конфигом возвращает 2 и печатает разрешённый путь (относительный `secretFile` от папки конфига);
- относительный `secretFile` в `ServerConfig.Load`;
- переменная окружения главнее файла; короткая переменная - отказ без отката на файл; при отсутствии файла переменная достаточна;
- WebSocket: без `hello` - `not_logged_in`, выключенный канал - `admin_disabled`/`admin_required`, аудит не пишется, `admin_reboot` - `unknown_type`;
- WebSocket: 5 раз `bad_secret`, шестой с верным секретом `rate_limited`; 6 строк аудита с полями `utc`, `gameTime`, `remote`, `account`, `reason`; ни верного, ни неверного секрета в файле аудита;
- WebSocket: обычный игрок получает `admin_required`, масштаб прежний, отказы в аудите с `args`;
- WebSocket: админ ставит масштаб 10, второй клиент получает push `time_scale`; права не переходят на другую сессию; `admin_status`; порядок и авторы строк аудита;
- WebSocket: `bad_scale` для `"NaN"`, `"Infinity"`, -1, 0, 0.05, 1000, `"ten"`, null, без поля; 100 (граница) принимается; аргументы в аудите как пришли;
- WebSocket: бюджет команд (настройка 3): четвёртая `rate_limited`, вторая админ-сессия со своим бюджетом;
- WebSocket: админ ставит аппарат на Луну при выключенном общем флаге (Луны нет в `allowedSpawnBodies`), обычный игрок получает `custom_spawn_disabled`; тело без поверхности отклонено; обычный спавн не пишется в аудит;
- WebSocket: `admin.allowCustomSpawn: false` - админ получает `custom_spawn_disabled`;
- секрета нет ни в перехваченных `Console.Out`/`Console.Error` (строка режима при старте есть), ни в файле аудита, ни в сообщении об отказе старта.

Что изменено:
- `E:\Projects\orbital-server\OrbitServer\Admin.cs` - новый.
- `OrbitServer/Config.cs`: свойство `Admin`, разрешение относительного `admin.secretFile` в `Load`, комментарий к `AllowCustomSpawn`.
- `OrbitServer/GameServer.cs`: `Spawn(..., privileged)`, `ValidateSpawnPoint(p, privileged)`, комментарий к `SetTimeScale`.
- `OrbitServer/Net.cs`: `Remote` и `Admin` у сессии, `DispatchAdmin`, привилегированный спавн, аудит, строка режима при старте, конструктор `NetServer(game, admin)`.
- `OrbitServer/Program.cs`: `AdminGate.Create` до загрузки мира, код 2 при отказе.
- `OrbitServer/serverconfig.json`: секция `admin` (выключена).
- `OrbitServer.Tests/Stage2c2Tests.cs` - новый.
- `README.md` сервера: админ-канал (локально и на удалённой машине), протокол, число тестов.
- В репозитории orbital: этот файл, `Docs/Stage2c-Plan.md` (2c.2 под файл секрета, настройка клиента в 2c.5/2c.6, раздел 4).
- Вне репозиториев: `E:\Projects\orbital-secrets\admin-secret.txt`, `E:\Projects\orbital-secrets\README.txt`.

Не проверено:
- Подключение настоящего клиента к реальному серверу с включённым каналом (ручной запуск только проверил старт и строку режима); протокол проверен тестами на loopback.
- Поведение за обратным прокси (nginx и т.п. на той же машине): все клиенты придут с адреса прокси, то есть с loopback, и проверка `loopbackOnly` перестанет что-либо отсекать. Заголовок `X-Forwarded-For` сервер не читает. Сейчас прокси нет; при его появлении проверку нужно пересмотреть (см. вопрос 1).
- Linux: путь по умолчанию в коде виндовый; на арендованной машине его нужно задать в `serverconfig.json` (иначе сервер с включённым каналом не стартует и назовёт путь). Права на файл (chmod 600) сервер не проверяет.
- Аудит не ротируется и не ограничен по размеру.
- Клиентская часть (`adminSecretFile`, `ORBITAL_ADMIN_SECRET`, ползунок) - 2c.5/2c.6.

Открытые вопросы:
1. Удалённый админ: по умолчанию только loopback, то есть на арендованном сервере админ подключается через SSH-туннель (`ssh -L 8765:127.0.0.1:8765`). Вариант без туннеля (`loopbackOnly: false`) отправляет секрет по ws без TLS. Если туннель неудобен, нужен TLS (wss) на сервере или прокси с проверкой адреса - решение пользователя.
2. Блокировка по IP при неудачах может быть использована против админа, который сидит за тем же адресом, что и атакующий (тот же NAT); при `loopbackOnly` это невозможно (с loopback ходят только локальные процессы). Для удалённого режима, возможно, нужна блокировка по паре (IP, аккаунт).
3. Общий флаг `allowCustomSpawn` оставлен для локальных плейтестов без секрета. Если он не нужен, его можно убрать в пользу админ-сессии (тесты 1.3/1.5 его используют).
4. Аудит пишет ключ аккаунта (`account`), а не позывной в исходном регистре и не id аппарата; если нужно больше для разбора, добавить просто.
5. Права админа живут до закрытия соединения; явного `admin_logout` нет. Нужен ли выход без разрыва соединения (например, для клиента, который переключает режимы)?
