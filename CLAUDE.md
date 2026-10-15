# Agrovent (AGR_PropManager)

Надстройка (add-in) для SolidWorks 2017 + PDM-функции: компоненты, версии, спецификации, хранилище файлов.
Стек: C# / WPF (MVVM), xCAD 0.8.3 (Xarial), EF Core + PostgreSQL (Npgsql).
Проекты решения: `Agrovent` (add-in, UI, сервисы), `Agrovent.DAL` (DataContext, репозитории, UnitOfWork), `Agrovent.Infrastructure` (сущности, enum, интерфейсы).

## Как работать со мной
- Язык общения: русский.
- Для больших задач сначала задавай уточняющие вопросы и предлагай разбивку на этапы, код пиши после согласования.
- Не придумывай API: если не уверен в имени метода xCAD/SolidWorks, ставь комментарий `// CHECK` и скажи об этом.

## Правила кода
Команды в ViewModel оформляются так: каждая команда в своём `#region <Имя>Command`, имя заканчивается на `Command`, все команды класса лежат в общем `#region COMMANDS`:

```csharp
#region LoadDataCommand
private ICommand _LoadDataCommand;
public ICommand LoadDataCommand => _LoadDataCommand
    ??= new RelayCommand(OnLoadDataCommandExecuted, CanLoadDataCommandExecute);
private bool CanLoadDataCommandExecute(object p) => true;
private void OnLoadDataCommandExecuted(object p)
{
    // код команды
}
#endregion
```

Команды SolidWorks (кнопки в CommandManager): enum `AGR_Commands_e` в `AgroventAddin.cs`, обработчик `OnCommandClickExecute`, логика команд в `AGR_CommandService`.
Состояние кнопок читает ТОЛЬКО кэш VM (`IAGR_ViewModelCacheService.Get`), в БД не ходит: SolidWorks вызывает его часто и синхронно.

## База данных (важно)
- EF-миграции НЕ использовать: `DataContextModelSnapshot.cs` устарел (нет `Users`, `SavedByUserId`), миграция сгенерирует лишнее.
- Схему меняем SQL-скриптами вручную (PostgreSQL, имена таблиц и колонок в кавычках, PascalCase). Скрипт пиши в ответе, сам БД не меняй.
- Обращения к БД из долгоживущих сервисов только через `IServiceScopeFactory` (DataContext зарегистрирован Scoped, в add-in своего scope нет).

## Окружение
- Запуск и отладка add-in только на машине разработчика в Visual Studio с установленным SolidWorks. Здесь собрать и проверить поведение в SolidWorks нельзя: после правок перечисляй, что нужно проверить руками.
- Не коммить и не пушь без просьбы.

## Текущая работа
Переделка логики сохранения/редактирования компонентов: `docs/EditingRework.md` (решения, статус, открытые вопросы).
