# UML-схемы Agrovent (AGR_PropManager)

Диаграммы классов в PlantUML, собраны по коду решения (`Agrovent`, `Agrovent.DAL`, `Agrovent.Infrastructure`).
Папка `AGR_PropManager` в корне содержит только `obj` и исходников не имеет.

| Файл | Что показывает |
|---|---|
| [01_packages.puml](01_packages.puml) | Сборки, пакеты, зависимости между слоями и внешние системы (SolidWorks/xCAD, PostgreSQL, хранилище) |
| [02_entities.puml](02_entities.puml) | Сущности EF и таблицы БД: поля, PK/FK, кратность связей, правила удаления (по `DataContext.OnModelCreating`) |
| [03_infrastructure_contracts.puml](03_infrastructure_contracts.puml) | Интерфейсы компонентов, свойств и сущностей, перечисления, `AGR_Options` / `AGR_StoragePaths` |
| [04_dal.puml](04_dal.puml) | `DataContext`, `UnitOfWork`, репозитории, `AGR_EditLockService`, план сохранения (`AGR_SavePlan`) |
| [05_addin_services.puml](05_addin_services.puml) | `AgroventAddin`, `AGR_Commands_e`, сервисы add-in, маркер, хелперы, расширения xCAD, команды |
| [06_viewmodels_components.puml](06_viewmodels_components.puml) | Иерархия VM компонентов (`AGR_BaseComponent` → `AGR_FileComponent` → Part/Assembly), свойства, материалы, панель задач |
| [07_viewmodels_windows.puml](07_viewmodels_windows.puml) | VM окон (спецификация, сохранение, взятие на редактирование, реестр, проекты, Pack'n'Go) и привязка View → VM |

## Нотация
- `<|--` наследование, `<|..` реализация интерфейса
- `*--` композиция (владение, каскадное удаление), `o--` агрегация, `-->` ассоциация, `..>` зависимость (создаёт / использует)
- `<<table X>>` таблица в БД, `<<singleton>>` / `<<scoped>>` время жизни в DI (`ServiceConfiguration`)
- Серым выделено устаревшее или вспомогательное: `ComponentVersion2`, `Interfaces.Entities`, `AGR_Paint2`, `ViewModels.Tree.AGR_TaskPaneViewModel`

## Как открыть
- VS Code: расширение *PlantUML* (jebbs.plantuml), `Alt+D` для предпросмотра. Нужны Java и Graphviz, либо в настройках расширения указать render-сервер.
- Rider / IntelliJ: плагин *PlantUML Integration*.
- Экспорт в PNG/SVG: `java -jar plantuml.jar -tsvg docs/uml/*.puml`

## Что заметно по схемам
- `IAGR_Part` объявлен, но классами не реализуется: `AGR_PartComponentVM` реализует `IAGR_HasMaterial` + `IAGR_HasPaint`.
- `Interfaces.Entities.*` и `ComponentVersion2` дублируют сущности и в `DataContext` не участвуют.
- Два класса `AGR_TaskPaneViewModel` (`ViewModels.TaskPane` и `ViewModels.Tree`): DI и add-in используют первый.
- `Project` и `ProjectComponent` не наследуют `BaseEntity` (свой `Id`, нет `CreatedAt`/`UpdatedAt`).
- `AGR_SpecificationWindow` и `SaveConfirmationView` переиспользуются для `AGR_AssemblyEditVM` и `AGR_ComponentEditVM` (правка из реестра).
