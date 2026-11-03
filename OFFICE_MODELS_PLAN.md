# Офис из моделей Sketchfab — задание для нового чата

Цель: заменить процедурную мебель 3D-офиса «Куро Продакшн» на бесплатные модели
с Sketchfab. Модели обработать в Blender и встроить в сборщик сцены.

## Что можно и что нельзя

- Меняем **только офисную комнату**: мебель и предметы из таблицы ниже.
- **Расстановку не трогаем.** Каждая модель встает на место старого предмета: та же
  позиция, тот же поворот, те же габариты.
- **Анимации не трогаем.** Без изменений остаются:
  - телефон (`PhoneAlert`), лоток «Входящие» с листами (`InboxStack`, `WindSheet`);
  - часы (`OfficeClock`), шторы, лист у монитора и стикер на мониторе (`WindSheet`);
  - бумаги Марии (`WindSheet`), растение Марии (`PlantSway`), торт со свечой (`Blinker`);
  - пар над кружкой (частицы).
- Не трогаем также: GUI, `demo-next/index.html` (его правят другие чаты), ролик прихода
  (`OfficeBuilder.Arrival.cs`), цех за стеклом, улицу, окно, календарь, вывеску,
  дневник, потолочные светильники, стены и пол.
- Сервер на 8771 не выключать.
- Пользователь просит не гонять игру в браузере: только правки и пересборка, проверяет
  он сам. Скриншоты кадра из Unity через MCP можно (см. «Проверка»).

## Как устроен офис

- Сцену `unity/Assets/Scenes/Office.unity` целиком генерирует
  `unity/Assets/Editor/OfficeBuilder.cs`: меню **Kuro → Собрать офис**. Ручные правки
  сцены пропадут, менять нужно сборщик.
- Сейчас вся мебель процедурная:
  - меши дает `Editor/MeshKit.cs` (RoundedBox, Lathe, Tube, Sheet);
  - текстуры дает `Editor/TextureKit.cs`;
  - готовые меши лежат в `Assets/Meshes`, эта папка пересоздается при каждой сборке.
- Оси: игрок сидит в `(0, 1.2, −0.55)` и смотрит в +Z. Справа (+X) цех, слева (−X)
  дверь и стеллаж.
- Unity 6000.6.0f1, Built-in RP, Linear. Свой постэффект `KuroPost`; затенение
  в углах и под предметами делают декали `Blob(...)` и `Shade(...)` с шейдером `Kuro/AO`.
- Unity управляется через Unity MCP (`Unity_RunCommand`, `Unity_GetConsoleLogs`).
- Blender 5.2.1: `/opt/homebrew/bin/blender`.

## Шаг 1. Скачать модели

Скачивать нужно в Chrome, где выполнен вход в Sketchfab. На странице модели:
**Download 3D Model → glTF** (подойдет и GLB). Архивы сложить в
`unity/SketchfabRaw/`. Эта папка лежит вне `Assets`, поэтому Unity не станет импортировать
исходники. Имя архива неважно, модель узнается по названию.

| Ключ | Модель, автор | Лицензия | glb | Что заменяет |
|---|---|---|---|---|
| `desk` | [Simple Office Desk (FREE)](https://sketchfab.com/3d-models/958136efb5bc48c6b1bf20dd444a4b84), dr.badass2142 | Free Standard | 6,2 МБ | оба стола |
| `chair` | [Office chair](https://sketchfab.com/3d-models/af2f07d06f6349158c1d24d87f5ceb95), inven2000 | CC BY | 10,1 МБ | оба кресла |
| `computer` | [Desktop Computer](https://sketchfab.com/3d-models/561abc2fc95941609fc7bc6f232895c2), tylerhalterman | CC BY | 2,0 МБ | оба монитора, мышь, клавиатура Марии |
| `keyboard` | [Keyboard](https://sketchfab.com/3d-models/66f5ca31bf154c82ae5284a32a362a4e), oscarherry3d | CC BY | 13,6 МБ | клавиатура игрока |
| `cabinet` | [Filing Cabinet - 6MB](https://sketchfab.com/3d-models/d217a4bbdfa2426eb32ebf3b1007a8c3), ahmagh2e | CC BY | 5,3 МБ | архив (из набора взять серый шкаф на 4 ящика) |
| `shelf` | [Dark Wood Book Shelf](https://sketchfab.com/3d-models/2acda3ed2f9046129897f0f1e36092bd), G1ngerBoy | CC BY | 32,4 МБ | стеллаж (пустой, папки ставим свои) |
| `door` | [Wooden door](https://sketchfab.com/3d-models/8726b6c219a54ed585e096f267c2a35e), 7ASSAN | CC BY | 8,4 МБ | дверь |
| `radiator` | [Old Radiator](https://sketchfab.com/3d-models/8a1a2e0263aa401591c1e87d824a79ef), thethieme | CC BY | 7,1 МБ | радиатор под окном |
| `cooler` | [Water cooler](https://sketchfab.com/3d-models/4b88c4c4e94c497ca39f831f374e89fc), tboiston | CC BY | 16,3 МБ | кулер |
| `lamp` | [Desk lamp](https://sketchfab.com/3d-models/b9458e04d30e4b6284d46d35880a9b95), 7ASSAN | CC BY | 11,3 МБ | лампа на столе Марии |
| `mug` | [Coffee Mug (School Project)](https://sketchfab.com/3d-models/5f5ccee1514c440887c072fae8e0d699), FrenchBaguette | CC BY | 0,6 МБ | кружка |
| `cactus` | [Prickly Pear in a Pot](https://sketchfab.com/3d-models/fa479faaff0c4133aa9e6720cfd9aab7), anezka | CC BY | 8,4 МБ | кактус на подоконнике |
| `pens` | [Pens with Pencil holder](https://sketchfab.com/3d-models/1f33e40c27fe451e9efda612c69010ae), janexx | CC BY | 3,5 МБ | стакан с ручками |

Всего около 125 МБ. Эти модели отобраны через публичный поиск Sketchfab
(`api.sketchfab.com/v3/search`, без входа): только скачиваемые, лицензии CC BY или
Free Standard, умеренное число полигонов. Если какая-то модель не подойдет, замену
ищите тем же способом. Лицензии NC/ND не брать.

## Шаг 2. Обработать в Blender (без окна)

Скрипт держать вне папки с архивами, например в scratchpad. Запуск:

```bash
/opt/homebrew/bin/blender -b --factory-startup -P prep.py -- <архив или папка> <ключ> <макс. текстура>
```

Что делает скрипт для каждой модели:

1. Импорт `scene.gltf` или `.glb`: `bpy.ops.import_scene.gltf`.
2. Удалить камеры и источники света. Снять родителей с сохранением трансформа
   (`parent_clear(type='CLEAR_KEEP_TRANSFORM')`), удалить пустышки.
3. Отрезать лишнее:
   - в `cabinet` оставить один шкаф на 4 ящика;
   - `computer` разделить на `computer_monitor`, `computer_keyboard`, `computer_mouse`,
     системный блок удалить.
4. Склеить меши (`join`), применить трансформы, перенести начало координат в центр низа
   габарита.
5. Уменьшить текстуры (`image.scale(w, h)`, затем `image.pack()`):
   - 2048 для стола, кресла, стеллажа, двери и архива;
   - 1024 для монитора, клавиатуры, кулера, лампы и радиатора;
   - 512 для кружки, кактуса и стакана.
6. Если треугольников больше ~30 тыс., применить Decimate. Сейчас все модели укладываются,
   кресло около 29 тыс.
7. Экспорт в `unity/Assets/Models/Sketchfab/<ключ>.glb`:
   `bpy.ops.export_scene.gltf(export_format='GLB')`.
8. Записать рядом `<ключ>.json` с габаритами. Для `shelf` добавить высоты полок
   (горизонтальные грани, смотрящие вверх, в долях высоты). Для `computer_monitor` —
   прямоугольник экрана: центр и размер грани с материалом экрана.

## Шаг 3. Импорт в Unity

- Unity сам glTF не читает. Поставить пакет **glTFast**: `com.unity.cloud.gltfast`
  (Package Manager → Add package by name). Тогда `.glb` в `Assets` станет моделью
  с PBR-материалами.
- Вариант без пакета: из Blender экспортировать FBX. Но тогда теряются карты
  metallic и roughness, и материалы придется собирать в сборщике из текстур (Standard,
  smoothness в альфе metallic).
- Проверить, что модели не розовые и в редакторе, и в WebGL. Если в браузере
  материал пропал, добавить шейдеры glTFast в Always Included Shaders.

## Шаг 4. Встроить в `OfficeBuilder.cs`

Добавить помощник по образцу существующих `Part` и `Box`:

```csharp
// Модель из Assets/Models/Sketchfab/<key>.glb: вписывается в габариты size
// (центр по X/Z, низ на pos.y), поворот rotY. keepRatio — один масштаб по всем осям.
static GameObject Model(string name, Transform parent, string key, Vector3 pos, float rotY, Vector3 size, bool keepRatio = true)
```

Внутри:
- `AssetDatabase.LoadAssetAtPath<GameObject>`, затем `Object.Instantiate`;
- габариты считать по `Renderer.bounds` (есть готовый `LocalBounds(Transform)`);
- масштаб: `min(size / bounds)`, при `keepRatio = false` — по каждой оси;
- сдвиг: низ модели на `pos.y`, центр на `pos.x/z`.

Колайдеры моделям не нужны: интерактивным предметам их ставит `AddInteractable` по
габаритам, а у остальных колайдеры ни на что не влияют. Ориентацию каждой модели
проверить по кадру. Разворот задать в `rotY`: у glTF обычно лицевая сторона смотрит
в +Z, а нам часто нужна −Z.

### Что и где менять

Координаты взяты из текущего кода.
- «office» — мировые координаты.
- «desk» — локальные координаты стола: игрока стоит в `(0, 0, 0.25)`, Марии в
  `(−2.45, 0, −1.2)` с поворотом 90°.
- Размеры заданы как Ш × В × Г, в метрах.

| Предмет | Функция | Где | Габарит | Сохранить |
|---|---|---|---|---|
| Стол игрока | `DeskModel` в `BuildPlayerDesk` | группа `Desk_Player`, office `(0, 0, 0.25)` | 1,6 × 0,75 × 0,8, верх столешницы ровно 0,75 | коврик, все, что на столе, `Blob` под столом |
| Стол Марии | `DeskModel` в `BuildMariaDesk` | `Desk_Maria`, office `(−2.45, 0, −1.2)`, rotY 90 | 1,3 × 0,75 × 0,7 | бумаги, торт, растение |
| Кресло игрока | `ChairModel` | внутри группы `Chair_Player`, office `(0, 0, −0.55)`, сиденье к столу (+Z) | ≈ 0,6 × 1,15 × 0,6, сиденье на 0,47 | группу `Chair_Player`: ее вращает `SeatedLook.chair`; `StripColliders(chair)` |
| Кресло Марии | `ChairModel` | desk `(0, 0, −0.68)` | как у игрока | — |
| Монитор игрока | `MonitorModel(mon, 1f, true)` | группа `Monitor`, office `(0, 0.75, 0.47)`, экран к игроку (−Z) | высота ≈ 0,5; экран 0,594 × 0,336, центр в `(0, 0.325, −0.0132)` | **дочерний `Screen`** (Sheet с текстурой рабочего стола, его ищет `KuroOS`). Положить его поверх экрана модели. `Interactable` на группе `Monitor`, стикер `Sticky` (перенести на новую рамку), кабель (поправить или убрать) |
| Монитор Марии | `MonitorModel(mon, 0.88f, false)` | desk `(−0.35, 0.75, 0.12)` | 0,88 от монитора игрока | свой `Screen` с документом `docs[2]` |
| Клавиатура игрока | `KeyboardModel` | desk `(0, 0.753, −0.12)`, наклон −2,5° | 0,44 × 0,02 × 0,15 | — |
| Клавиатура Марии | `KeyboardModel` | desk `(−0.3, 0.75, −0.15)` | 0,44 × 0,15 | — |
| Мышь | группа `Mouse` | desk `(0.32, 0.753, −0.13)` | 0,058 × 0,034 × 0,1 | провод `MouseCable` подогнать к новой мыши |
| Кружка | группа `Mug` | desk `(0.62, 0.75, −0.15)`, rotY 140 | Ø 0,084, высота 0,1 | частицы `Steam`: поставить над краем новой кружки |
| Стакан с ручками | группа `PenCup` | desk `(−0.68, 0.75, 0.3)` | Ø 0,07, высота с ручками ≈ 0,17 | — |
| Стеллаж | `BuildShelf` | `Shelf`, office `(−3.3, 0, 2.55)`, rotY −90 (лицом в +X) | 1,11 × 1,9 × 0,34 | группы `Accounts` и `Policy` с надписями (это интерактивные предметы). Папки и коробки поставить на полки модели: высоты из `shelf.json` вместо 0,5 / 0,95 / 1,4. Затенение `Shade` на полках тоже по новым высотам |
| Архив | `BuildArchive` | `Archive`, office `(1.95, 0, 3.12)`, ящики к игроку (−Z) | 0,6 × 1,36 × 0,62 | надпись «АРХИВ» на фасаде, коробку и папки на крышке (по новой высоте), `Interactable` «docs» |
| Дверь | `BuildDoor` | `Door`, office `(−3.5, 0, 1.2)`, rotY −90 (лицом в +X) | коробка 1,08 × 2,2; полотно 0,92 × 2,06 | табличку «ВЫХОД» над дверью на 2,42, `Interactable` «door». Проема в стене нет (стена сплошная, x от −3,6 до −3,5): коробка может утопать в стене |
| Радиатор | `Radiator` в `BuildWindow` | office, центр `(−1.6, 0.55, 3.42)` | 0,9 × 0,5 × 0,08 | под подоконником, верх подоконника на 1,0 |
| Кулер | `Cooler` в `BuildRoom` | office `(−2.9, 0, −2.15)`, rotY 180 | 0,32 × 1,48 × 0,32 с бутылью | — |
| Лампа Марии | `Lamp` в `BuildMariaDesk` | desk `(0.52, 0.75, −0.22)`, rotY −30 | высота ≈ 0,36 | светящаяся лампочка (материал с emission) |
| Кактус | `Cactus` в `BuildWindow` | office `(−1.05, 1.0, 3.41)` на подоконнике | Ø ≈ 0,1, высота ≈ 0,17 | — |

Интерактивные предметы проверяет `OfficePlayTest` по путям
`Office/Monitor`, `Office/Archive`, `Office/Shelf/Accounts`, `Office/Shelf/Policy`,
`Office/Door` и другим. Имена групп не менять.

`MarkStatic` сам отправит новые неподвижные модели в статические пакеты.
Затенения `Blob(...)` под предметами оставить, при необходимости поправить их размер.

## Шаг 5. Собрать и проверить

1. **Kuro → Собрать офис**, затем консоль (`Unity_GetConsoleLogs` без фильтра):
   должно быть «Офис собран» и ни одной ошибки.
2. **Kuro → Проверить офис (в Play)**. Ждать итог «Проверка офиса: N из N»,
   в последний раз было 36 из 36. Если `OfficePlayTest.Run()` вызвать сразу после
   входа в Play, вызов теряется: позвать еще раз, когда Play уже идет.
3. Кадр без Play: в `Unity_RunCommand` вызвать `cam.Render()` в HDR RenderTexture,
   затем Blit в sRGB, `ReadPixels`, сохранить PNG в scratchpad. Посмотреть стол,
   левую стену (дверь, стеллаж), правый угол (архив), окно.
4. WebGL для 8771, прямо в `Unity_RunCommand`:
   `Kuro.EditorTools.WebBuild.Build("/private/tmp/claude-501/-Users-avvr-Documents---------/1c75c19c-7695-4e46-b4ef-6a8c1be4bfea/scratchpad/webgl")`.
   Сервер из `.claude/launch.json` («unity-office») раздает именно эту папку. Метку
   `?v=` против кэша сборка ставит сама. Если папки уже нет (например, после
   перезагрузки), собрать заново и поправить путь в `launch.json`.
5. Следить за размером: прошлая сборка весила 37,4 МБ. Если вырастет больше чем
   до ~55 МБ, ужать текстуры (Max Size в импорте) или уменьшить их в Blender.

## Шаг 6. Авторы

Лицензия CC BY требует указать автора. Создать
`unity/Assets/Models/Sketchfab/CREDITS.md`: название модели, автор, ссылка, лицензия
по таблице из шага 1. Free Standard указания автора не требует, но в список можно
внести и эту модель.

## Ловушки Unity MCP (из прошлых сессий)

- После `AssetDatabase.Refresh` и при установке пакета MCP около 15–25 с отвечает
  «Unity not detected», пока идет перекомпиляция. Подождать и повторить.
- В `Unity_RunCommand` запрещен `System.Reflection`. `GetInstanceID()` в 6000.6 не
  компилируется, нужен `GetEntityId()`.
- `Unity_GetConsoleLogs` с фильтром `"Log"` обычные сообщения не отдает: звать без
  фильтра.
- Когда Unity в фоне, Play почти не крутит кадры. `PlayInBackground.cs` включает
  `runInBackground` в редакторе.
- WebGL-сборка идет 30–60 с. Запускать прямо в `RunCommand`, не через
  `EditorApplication.delayCall`.
