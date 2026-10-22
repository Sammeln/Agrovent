using System.IO;
using System.Text.Json;
using Agrovent.DAL;
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Services;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.TaskPane;
using Agrovent.ViewModels.Windows;
using AgroventInfrastructure;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Interfaces;
using AgroventInfrastructure.Interfaces.Components;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Services
{
    public class ComponentVersionService : IAGR_ComponentVersionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ComponentVersionService> _logger;
        private readonly AGR_SaveProgressVM _saveProgress;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAGR_EditLockService _editLocks;
        private readonly AGR_SavePlanBuilder _planBuilder;
        private readonly IAGR_MarkerService _markers;

        public ComponentVersionService(
            IUnitOfWork unitOfWork
            ,ILogger<ComponentVersionService> logger
            ,DataContext context
            ,IAGR_SaveProgressVM saveProgress
            ,IServiceScopeFactory scopeFactory
            ,IAGR_MarkerService markers
            ,IAGR_EditLockService editLocks)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _saveProgress = saveProgress as AGR_SaveProgressVM;
            _scopeFactory = scopeFactory;
            _editLocks = editLocks;
            _planBuilder = new AGR_SavePlanBuilder(markers, editLocks, logger);
            _markers = markers;
        }

        /// <summary>План сохранения; при ошибках (чужой замок, повреждённый маркер) - исключение со списком.</summary>
        private async Task<AGR_SavePlan> BuildPlanAsync(IEnumerable<IAGR_BaseComponent> components)
        {
            var (plan, errors) = await _planBuilder.BuildAsync(components);
            if (errors.Count > 0)
            {
                foreach (var e in errors) _saveProgress.AddLogMessage(e);
                throw new InvalidOperationException("Сохранение невозможно:\n" + string.Join("\n", errors));
            }
            return plan;
        }

        /// <summary>Компоненты так же, как их обходит SaveAssemblyStructure (GetChildComponents по уровням),
        /// чтобы в плане был каждый компонент структуры.</summary>
        private static void CollectStructure(IAGR_Assembly assembly, List<IAGR_BaseComponent> result, HashSet<string> seen)
        {
            foreach (var item in assembly.GetChildComponents())
            {
                if (!seen.Add(AGR_SavePlan.KeyOf(item.Component))) continue;
                result.Add(item.Component);
                if (item.Component is IAGR_Assembly child)
                {
                    CollectStructure(child, result, seen);
                }
            }
        }

        /// <summary>Замки, которые снимаются в транзакции сохранения (компоненты с новой версией по моему замку).</summary>
        private static List<int> LockedIds(AGR_SavePlan plan)
            => plan.Items.Values
                .Where(i => i.Action == AGR_VersionAction_e.NewVersion && i.LockedComponentId.HasValue)
                .Select(i => i.LockedComponentId!.Value)
                .Distinct()
                .ToList();

        public async Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component)
        {
            // 0. PartNumber (отдельное соединение) и сохранение локальных документов
            await GenerateMissingPartNumbersAsync(new[] { component });
            await TrySaveDocuments(component);

            // 1. План до транзакции: хеш больше не решает, есть ли изменения
            var plan = await BuildPlanAsync(new[] { component });
            var planItem = plan.Get(component);
            if (planItem.Action != AGR_VersionAction_e.NewVersion)
            {
                _logger.LogInformation($"Компонент не на редактировании: {component.PartNumber} v{planItem.Version}");
                _saveProgress.AddLogMessage($"Компонент не взят на редактирование, сохранять нечего: {component.PartNumber} v{planItem.Version}");
                return false;
            }

            return await SaveByPlanAsync(component, plan, async () =>
            {
                await _unitOfWork.ComponentRepository.ResolvePlannedVersion(component, plan);
            });
        }

        public async Task<bool> CheckAndSaveAssemblyAsync(AGR_AssemblyComponentVM assembly)
        {
            // 0. Собираем ВСЕ компоненты сборки (включая саму сборку), у которых нет PartNumber
            var allComponents = assembly.GetFlatComponents(true)
                .Select(c => c.Component)
                .ToList();

            if (string.IsNullOrWhiteSpace(assembly.PartNumber))
            {
                allComponents.Add(assembly);
            }
            // 1. Генерируем PartNumber через ОТДЕЛЬНОЕ соединение ДО начала транзакции!
            await GenerateMissingPartNumbersAsync(allComponents);
            await TrySaveDocuments(assembly);

            // 2. Облегчённые компоненты: маркер не прочитать, автоматически не разрешаем
            var lightweight = AGR_SavePlanBuilder.FindLightweight((ISwAssembly)assembly.SwDocument);
            if (lightweight.Count > 0)
            {
                throw new InvalidOperationException(
                    "В сборке есть облегчённые компоненты. Разрешите их (\"Установить как разрешённые\") и повторите сохранение:\n"
                    + string.Join("\n", lightweight));
            }

            // 3. План до транзакции: новые версии по замкам, остальные ссылаются на версию из маркера,
            //    у неотмеченных проверяется отпечаток геометрии
            var planComponents = new List<IAGR_BaseComponent> { assembly };
            CollectStructure(assembly, planComponents, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AGR_SavePlan.KeyOf(assembly) });
            var plan = await BuildPlanAsync(planComponents);
            if (plan.Get(assembly).Action != AGR_VersionAction_e.NewVersion)
            {
                _logger.LogInformation($"Сборка не на редактировании: {assembly.PartNumber}");
                _saveProgress.AddLogMessage($"Сборка не взята на редактирование, сохранять нечего: {assembly.PartNumber}");
                return false;
            }

            return await SaveByPlanAsync(assembly, plan, async () =>
            {
                await _unitOfWork.ComponentRepository.SaveAssemblyStructure(assembly, assembly.GetChildComponents(), plan);
            });
        }

        /// <summary>
        /// Общий порядок сохранения (этап 4.4): транзакция → версии (saveVersions) и снятие моих замков →
        /// CompleteAsync (появляются Id) → маркеры в файлы новых версий → копирование в хранилище → Commit.
        /// Любая ошибка: Rollback и удаление созданных папок версий.
        /// </summary>
        private async Task<bool> SaveByPlanAsync(IAGR_BaseComponent root, AGR_SavePlan plan, Func<Task> saveVersions)
        {
            var rootName = root.Name;
            var rootPartNumber = root.PartNumber;
            var userId = await _editLocks.GetCurrentUserIdAsync();
            var swApp = AGR_ServiceContainer.GetService<ISwApplication>()
                ?? throw new InvalidOperationException("Нет доступа к SolidWorks.");
            var writer = new AGR_StorageWriter(swApp, _markers, _logger, _saveProgress.AddLogMessage);

            // Контекст общий на всё время работы add-in: сущности прошлых сохранений (в т.ч. откатанных,
            // или удалённых из БД вручную) не должны попасть в это сохранение через Local.
            _unitOfWork.ClearTracking();
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                _logger.LogInformation($"Начинаем сохранение: {rootName} {rootPartNumber}");
                _saveProgress.AddLogMessage($"Начинаем сохранение: {rootName} {rootPartNumber}");

                // 1. Версии по плану и снятие моих замков в одной транзакции
                await saveVersions();
                await ResolveRemainingAsync(plan);
                await _unitOfWork.ComponentRepository.RemoveEditLocksAsync(LockedIds(plan), userId);

                // 4.6: у неотмеченных геометрия проверена отпечатком; изменённые свойства файла пишем в их версию
                var updatedIds = new HashSet<int>();
                foreach (var item in plan.Items.Values.Where(i => i.Action == AGR_VersionAction_e.UseExisting
                                                               && !i.Component.IsPurchased
                                                               && i.Saved != null))
                {
                    if (updatedIds.Add(item.Saved!.Id))
                        await _unitOfWork.ComponentRepository.UpdateReferencedVersionPropertiesAsync(item.Saved, item.Component);
                }
                var savedCount = await _unitOfWork.CompleteAsync();
                _saveProgress.AddLogMessage($"Подготовлено {savedCount} записей БД.");

                // 2. Маркеры: Id и версии уже известны, документы ещё открыты
                writer.WriteMarkers(plan);

                // 3. Файлы новых версий в хранилище (закрывает все документы)
                writer.CopyToStorage(plan, root);

                // 4. Только теперь фиксируем БД
                await _unitOfWork.CommitTransactionAsync();

                var created = plan.Items.Values.Count(i => i.Action == AGR_VersionAction_e.NewVersion);
                _logger.LogInformation($"Сохранено: {rootName} {rootPartNumber}, новых версий: {created}");
                _saveProgress.AddLogMessage($"Сохранено: {rootName} {rootPartNumber}, новых версий: {created}");

                ResetTaskPane();
                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                writer.DeleteCreatedFolders();
                // Маркеры, уже записанные в локальные файлы, указывают на откатанные версии. Замки остались,
                // поэтому при повторном сохранении эти компоненты снова получат новую версию.
                _logger.LogError(ex, $"Ошибка при сохранении: {rootPartNumber}");
                _saveProgress.AddLogMessage($"Ошибка при сохранении {rootPartNumber}, изменения в БД отменены. {ex.Message} {ex.InnerException?.Message}");
                throw;
            }
        }

        /// <summary>
        /// SaveAssemblyStructure не заходит в неотмеченные подсборки, а в плане есть и их дети
        /// (отпечаток проверяется у всех). Им нужна версия для свойств (4.6) и копирования: берём существующую.
        /// </summary>
        private async Task ResolveRemainingAsync(AGR_SavePlan plan)
        {
            foreach (var item in plan.Items.Values.Where(i => i.Saved == null).ToList())
            {
                if (item.Action != AGR_VersionAction_e.UseExisting)
                {
                    // Новая версия, на которую не ссылается ни одна новая версия сборки: файл без маркера
                    // внутри подсборки, не взятой на редактирование
                    throw new InvalidOperationException(
                        $"{item.Component.Name} ({item.Component.PartNumber}): нет маркера, а подсборка с ним не взята на редактирование. " +
                        "Возьмите подсборку на редактирование и повторите сохранение.");
                }
                await _unitOfWork.ComponentRepository.ResolvePlannedVersion(item.Component, plan);
            }
        }

        /// <summary>Документы закрыты: сбрасываем кэш VM и панель (как раньше после сохранения детали).</summary>
        private static void ResetTaskPane()
        {
            var viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
            var taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
            if (taskPane == null) return;

            viewModelCache?.Clear();
            taskPane.ActiveView = null;
            taskPane.BaseComponent = null;
            taskPane.ActiveComponent = null;
            taskPane.Selection = null;
            taskPane.ActiveView = new AGR_HomePageVM();
        }
        private async Task GenerateMissingPartNumbersAsync(IEnumerable<IAGR_BaseComponent> components)
        {
            // Отбираем только те компоненты, у которых еще нет PartNumber
            var componentsWithoutPn = components.Where(c => string.IsNullOrWhiteSpace(c.PartNumber)).ToList();
            if (!componentsWithoutPn.Any()) return;

            _logger.LogInformation($"Генерация PartNumber для {componentsWithoutPn.Count} компонентов через отдельное соединение...");
            _saveProgress.AddLogMessage($"Генерация PartNumber для {componentsWithoutPn.Count} компонентов...");

            // Создаем ИЗОЛИРОВАННЫЙ скоуп, чтобы получить новый DataContext и НОВОЕ соединение с БД
            using var scope = _scopeFactory.CreateScope();
            var tempContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var connection = tempContext.Database.GetDbConnection();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                foreach (var comp in componentsWithoutPn)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT nextval('part_number_seq')";
                    var result = await command.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException("БД вернула пустое значение для Sequence.");

                    int nextNumber = Convert.ToInt32(result);
                    comp.PartNumber = nextNumber.ToString("D7"); // Присваиваем PartNumber объекту в памяти

                    _logger.LogDebug($"Сгенерирован PartNumber: {comp.PartNumber} для {comp.Name}");
                    _saveProgress.AddLogMessage($"Сгенерирован PartNumber: {comp.PartNumber} для {comp.Name}");
                }
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }
        private async Task TrySaveDocuments(IAGR_BaseComponent component)
        {
            var docsToSave = component.SwDocument.OwnerApplication.Documents.Where(d => d.IsDirty).ToList();
            foreach (var item in docsToSave)
            {
                var attr = File.GetAttributes(item.Path);
                if (attr.HasFlag(FileAttributes.ReadOnly))
                {
                    continue;
                }
                item.Save();
            }
        }



        public async Task<Component> CreateNewComponent(IAGR_BaseComponent baseComponent)
        {
            return await _unitOfWork.ComponentRepository.CreateNewComponent(baseComponent);
        }
        public async Task<bool> CreateNewComponents(List<IAGR_BaseComponent> baseComponents)
        {
            await _unitOfWork.ComponentRepository.CreateNewComponents(baseComponents);
            return true;
        }

        // Копирование файлов в хранилище ---
        public async Task CopyFilesToStorageAsync(IAGR_BaseComponent rootComponent, int rootHashSum)
        {
            var rootPartNumber = rootComponent.PartNumber;

            // 1. Собираем информацию о файлах и их хэшах
            var filesToCopyInfo = new Dictionary<string, (int Hash, int Version)>(); // Ключ: исходный путь, Значение: хэш и версия компонента

            // Если rootComponent - сборка, получаем плоский список зависимостей
            if (rootComponent is AGR_AssemblyComponentVM assemblyComponent)
            {
                _logger.LogDebug($"Сборка обнаружена, извлекаем зависимости: {rootComponent.PartNumber}");
                _saveProgress.AddLogMessage($"Сборка обнаружена, извлекаем зависимости: {rootComponent.PartNumber}");

                var flatDependencies = assemblyComponent.GetFlatComponents(false); // Получаем IEnumerable<IAGR_SpecificationItem>
                foreach (var specItem in flatDependencies)
                {
                    var depComponent = specItem.Component; // IAGR_BaseComponent
                    AddComponentFilesToDict(depComponent, filesToCopyInfo);
                }
                AddComponentFilesToDict(rootComponent, filesToCopyInfo);
            }
            // Для детали или других типов добавляем только сам rootComponent
            else
            {
                AddComponentFilesToDict(rootComponent, filesToCopyInfo);
            }

            if (filesToCopyInfo.Count == 0)
            {
                _logger.LogWarning($"Не найдено файлов для копирования для компонента {rootComponent.PartNumber} (HashSum: {rootHashSum})");
                _saveProgress.AddLogMessage($"Не найдено файлов для копирования для компонента {rootComponent.PartNumber} (HashSum: {rootHashSum})");
                return;
            }

            // 2. Получаем экземпляр SolidWorks Application
            ISwApplication swApp;
            try
            {
                swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить доступ к экземпляру SolidWorks Application для копирования файлов.");
                    _saveProgress.AddLogMessage("Не удалось получить доступ к экземпляру SolidWorks Application для копирования файлов.");
                    return; // Или выбросить исключение
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении экземпляра SolidWorks Application.");
                _saveProgress.AddLogMessage($"Ошибка при получении экземпляра SolidWorks Application.{ex.Message}");
                return; // Или выбросить исключение
            }


            // --- НОВАЯ ЛОГИКА: Подготовка массивов и вызов CopyDocument ---
            if (rootComponent is IAGR_HasFile rootFileComponent && !string.IsNullOrEmpty(rootFileComponent.CurrentModelFilePath))
            {
                // 1.1 Сформировать sourceArray
                var sourceArray = filesToCopyInfo.Keys.Where(path => !string.IsNullOrEmpty(path)).ToArray();

                // 1.2 Сформировать targetArray
                var targetArray = new string[sourceArray.Length];
                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var sourcePath = sourceArray[i];
                    if (filesToCopyInfo.TryGetValue(sourcePath, out var infoForPath))
                    {
                        var targetDir = AGR_StoragePaths.GetVersionFolder(infoForPath.Hash, infoForPath.Version);
                        Directory.CreateDirectory(targetDir); // Убедиться, что папка существует
                        targetArray[i] = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    }
                    else
                    {
                        _logger.LogError($"Не найден хэш для пути {sourcePath} в словаре filesToCopyInfo.");
                        _saveProgress.AddLogMessage($"Не найден хэш для пути {sourcePath} в словаре filesToCopyInfo.");
                        // Пропускаем этот файл или обрабатываем ошибку по-другому
                        targetArray[i] = null; // или throw new Exception(...)
                    }
                }

                // Проверяем, что все пути в targetArray заполнены
                if (targetArray.Any(t => t == null))
                {
                    _logger.LogError("Не все целевые пути были вычислены. Операция копирования отменена.");
                    _saveProgress.AddLogMessage("Не все целевые пути были вычислены. Операция копирования отменена.");
                    return;
                }

                // 1.3 Записать исходный путь rootComponent
                var sourceFile = rootFileComponent.CurrentModelFilePath; // Это CurrentModelFilePath

                // 1.4 Записать целевой путь rootComponent
                if (!filesToCopyInfo.TryGetValue(sourceFile, out var rootInfo))
                {
                    _logger.LogError($"Не найден хэш для исходного файла rootComponent {sourceFile}.");
                    _saveProgress.AddLogMessage($"Не найден хэш для исходного файла rootComponent {sourceFile}.");
                    //return; // Или выбросить исключение
                }
                var targetDirRoot = AGR_StoragePaths.GetVersionFolder(rootInfo.Hash, rootInfo.Version);
                Directory.CreateDirectory(targetDirRoot); // Убедиться, что папка существует
                var targetFile = Path.Combine(targetDirRoot, Path.GetFileName(sourceFile));

                // 1.5 Закрыть все файлы
                _logger.LogDebug("Закрытие всех документов перед копированием.");
                _saveProgress.AddLogMessage("Закрытие всех документов перед копированием.");

                //var taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
                //var _viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
                //if (taskPane != null)
                //{
                //    if (_viewModelCache != null)
                //    {
                //        _viewModelCache.Clear();
                //    }
                //    taskPane.ActiveView = null;
                //    taskPane.BaseComponent = null;
                //    taskPane.ActiveComponent = null;
                //    taskPane.Selection = null;
                //    taskPane.ActiveView = new AGR_HomePageVM();
                //}
                swApp.Sw.CloseAllDocuments(true); // true - подавляет запросы на сохранение

                try
                {
                    // 1.6 Вызвать CopyDocument
                    int errorsRaw = 0;
                    errorsRaw = swApp.Sw.CopyDocument(
                        sourceFile, // sourcePath: активный файл (корень)
                        targetFile, // targetPath: целевой файл корня
                        sourceArray, // sourcepaths: массив всех зависимостей (и корня, и дочерних)
                        targetArray, // targetpaths: массив всех целевых путей
                        (int)swMoveCopyOptions_e.swMoveCopyOptionsOverwriteExistingDocs
                    );

                    if (errorsRaw != 0)
                    {
                        _logger.LogError($"Ошибки SolidWorks при копировании файлов через CopyDocument: {JsonSerializer.Serialize(errorsRaw)}");
                        // Логика обработки ошибок
                    }
                    else
                    {
                        _logger.LogInformation($"Файлы успешно скопированы через SW CopyDocument для компонента {rootPartNumber} (HashSum: {rootHashSum}).");
                        _saveProgress.AddLogMessage($"Файлы успешно скопированы через SW CopyDocument для компонента {rootPartNumber} (HashSum: {rootHashSum}).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Исключение при вызове SW CopyDocument для компонента {rootPartNumber} (HashSum: {rootHashSum}).");
                    // Логика обработки исключения
                }

                //swApp.Documents.Open(sourceFile); // Открываем исходный файл обратно в SolidWorks

            }
            else
            {
                _logger.LogWarning($"RootComponent {rootPartNumber} не реализует IAGR_HasFile или FilePath пуст.");
                _saveProgress.AddLogMessage($"RootComponent {rootPartNumber} не реализует IAGR_HasFile или FilePath пуст.");
            }
            // --- КОНЕЦ НОВОЙ ЛОГИКИ ---
        }
        // Копирование файлов в производственное хранилище (Production) ---
        public async Task CopyFilesToProdAsync(IAGR_BaseComponent rootComponent, int rootHashSum)
        {
            var rootPartNumber = rootComponent.PartNumber;

            // 1. Собираем информацию о файлах. 
            // Теперь храним не только хэш, но и PartNumber и флаг IsPurchased
            var filesToCopyInfo = new Dictionary<string, (int Hash, int Version, string PartNumber, bool IsPurchased)>();

            if (rootComponent is AGR_AssemblyComponentVM assemblyComponent)
            {
                _logger.LogDebug($"Сборка обнаружена, извлекаем зависимости: {rootComponent.PartNumber}");
                _saveProgress.AddLogMessage($"Сборка обнаружена, извлекаем зависимости: {rootComponent.PartNumber}");

                var flatDependencies = assemblyComponent.GetFlatComponents(false);
                foreach (var specItem in flatDependencies)
                {
                    var depComponent = specItem.Component;
                    AddComponentFilesToProductionDict(depComponent, filesToCopyInfo);
                }
                AddComponentFilesToProductionDict(rootComponent, filesToCopyInfo);
            }
            else
            {
                AddComponentFilesToProductionDict(rootComponent, filesToCopyInfo);
            }

            if (filesToCopyInfo.Count == 0)
            {
                _logger.LogWarning($"Не найдено файлов для копирования для компонента {rootComponent.PartNumber}");
                _saveProgress.AddLogMessage($"Не найдено файлов для копирования для компонента {rootComponent.PartNumber}");
                return;
            }

            ISwApplication swApp;
            try
            {
                swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить доступ к экземпляру SolidWorks Application.");
                    _saveProgress.AddLogMessage("Не удалось получить доступ к экземпляру SolidWorks Application.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении экземпляра SolidWorks Application.");
                _saveProgress.AddLogMessage($"Ошибка при получении экземпляра SolidWorks Application. {ex.Message}");
                return;
            }

            if (rootComponent is IAGR_HasFile rootFileComponent && !string.IsNullOrEmpty(rootFileComponent.CurrentModelFilePath))
            {
                var sourceArray = filesToCopyInfo.Keys.Where(path => !string.IsNullOrEmpty(path)).ToArray();
                var targetArray = new string[sourceArray.Length];

                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var sourcePath = sourceArray[i];
                    if (filesToCopyInfo.TryGetValue(sourcePath, out var fileInfo))
                    {
                        string targetDir;

                        // --- ЛОГИКА ВЫБОРА ПАПКИ ---
                        if (fileInfo.IsPurchased)
                        {
                            // Требование 3: purchased компоненты ссылаются на файл в хранилище (StorageRoot/hash/версия)
                            targetDir = AGR_StoragePaths.GetVersionFolder(fileInfo.Hash, fileInfo.Version);
                        }
                        else
                        {
                            // Требования 1 и 2: Для обычных компонентов используем ProductionRootFolderPath/partnumber
                            targetDir = Path.Combine(AGR_Options.ProductionRootFolderPath, fileInfo.PartNumber);
                        }

                        Directory.CreateDirectory(targetDir);
                        targetArray[i] = Path.Combine(targetDir, Path.GetFileName(sourcePath));
                    }
                    else
                    {
                        _logger.LogError($"Не найдена информация для пути {sourcePath} в словаре filesToCopyInfo.");
                        _saveProgress.AddLogMessage($"Не найдена информация для пути {sourcePath} в словаре filesToCopyInfo.");
                        targetArray[i] = null;
                    }
                }

                if (targetArray.Any(t => t == null))
                {
                    _logger.LogError("Не все целевые пути были вычислены. Операция копирования отменена.");
                    _saveProgress.AddLogMessage("Не все целевые пути были вычислены. Операция копирования отменена.");
                    return;
                }

                var sourceFile = rootFileComponent.CurrentModelFilePath;

                if (!filesToCopyInfo.TryGetValue(sourceFile, out var rootFileInfo))
                {
                    _logger.LogError($"Не найдена информация для исходного файла rootComponent {sourceFile}.");
                    _saveProgress.AddLogMessage($"Не найдена информация для исходного файла rootComponent {sourceFile}.");
                }

                // Целевой путь для корневого компонента
                string targetDirRoot;
                if (rootFileInfo.IsPurchased)
                {
                    targetDirRoot = AGR_StoragePaths.GetVersionFolder(rootFileInfo.Hash, rootFileInfo.Version);
                }
                else
                {
                    targetDirRoot = Path.Combine(AGR_Options.ProductionRootFolderPath, rootFileInfo.PartNumber);
                }

                Directory.CreateDirectory(targetDirRoot);
                var targetFile = Path.Combine(targetDirRoot, Path.GetFileName(sourceFile));

                _logger.LogDebug("Закрытие всех документов перед копированием.");
                _saveProgress.AddLogMessage("Закрытие всех документов перед копированием.");

                var taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
                var _viewModelCache = AGR_ServiceContainer.GetService<AGR_ViewModelCacheService>();
                if (taskPane != null)
                {
                    if (_viewModelCache != null) _viewModelCache.Clear();
                    taskPane.ActiveView = null;
                    taskPane.BaseComponent = null;
                    taskPane.ActiveComponent = null;
                    taskPane.Selection = null;
                }
                swApp.Sw.CloseAllDocuments(true);

                try
                {
                    var errorsRaw = swApp.Sw.CopyDocument(
                        sourceFile,
                        targetFile,
                        sourceArray,
                        targetArray,
                        (int)swMoveCopyOptions_e.swMoveCopyOptionsOverwriteExistingDocs
                    );

                    if (errorsRaw != 0)
                    {
                        _logger.LogError($"Ошибки SolidWorks при копировании файлов через CopyDocument: {JsonSerializer.Serialize(errorsRaw)}");
                    }
                    else
                    {
                        _logger.LogInformation($"Файлы успешно скопированы в Production для компонента {rootPartNumber}.");
                        _saveProgress.AddLogMessage($"Файлы успешно скопированы в Production для компонента {rootPartNumber}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Исключение при вызове SW CopyDocument для компонента {rootPartNumber}.");
                }
            }
            else
            {
                _logger.LogWarning($"RootComponent {rootPartNumber} не реализует IAGR_HasFile или FilePath пуст.");
                _saveProgress.AddLogMessage($"RootComponent {rootPartNumber} не реализует IAGR_HasFile или FilePath пуст.");
            }
        }


        // --- Добавление файлов компонента в словарь ---
        private void AddComponentFilesToDict(IAGR_BaseComponent component, Dictionary<string, (int Hash, int Version)> filesDict)
        {
            if (!(component is IAGR_HasFile fileComponent))
            {
                _logger.LogDebug($"Компонент {component.PartNumber} не реализует IAGR_HasFile, пропускаем.");
                return;
            }

            var componentHash = component.CalculateComponentHash(); // Вычисляем хэш для текущего компонента
            var componentVersion = component.Version;               // проставляется в ComponentRepository.SaveComponent

            // Добавляем CurrentModelFilePath
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentModelFilePath))
                {
                    filesDict[fileComponent.CurrentModelFilePath] = (componentHash, componentVersion);
                    _logger.LogDebug($"Добавлен файл модели для {component.PartNumber} (Hash: {componentHash}): {fileComponent.CurrentModelFilePath}");
                }
                else
                {
                    _logger.LogDebug($"Файл модели для {component.PartNumber} уже добавлен: {fileComponent.CurrentModelFilePath}");
                }
            }
            else
            {
                _logger.LogDebug($"CurrentModelFilePath пуст для компонента {component.PartNumber}");
            }

            // Добавляем CurrentDrawFilePath (если есть)
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentDrawFilePath))
                {
                    filesDict[fileComponent.CurrentDrawFilePath] = (componentHash, componentVersion);
                    _logger.LogDebug($"Добавлен файл чертежа для {component.PartNumber} (Hash: {componentHash}): {fileComponent.CurrentDrawFilePath}");
                }
                else
                {
                    _logger.LogDebug($"Файл чертежа для {component.PartNumber} уже добавлен: {fileComponent.CurrentDrawFilePath}");
                }
            }
            else
            {
                _logger.LogDebug($"CurrentDrawFilePath пуст для компонента {component.PartNumber}");
            }
        }

        // --- Добавление файлов компонента в словарь для Production ---
        private void AddComponentFilesToProductionDict(IAGR_BaseComponent component, Dictionary<string, (int Hash, int Version, string PartNumber, bool IsPurchased)> filesDict)
        {
            if (!(component is IAGR_HasFile fileComponent))
            {
                _logger.LogDebug($"Компонент {component.PartNumber} не реализует IAGR_HasFile, пропускаем.");
                return;
            }

            var componentHash = component.CalculateComponentHash();
            var partNumber = component.PartNumber;

            // ВАЖНО: Замените эту проверку на реальную логику определения "purchased" компонента в вашей системе!
            // Например: component.ComponentType == ComponentType.Purchased или component.IsPurchased
            bool isPurchased = component.IsPurchased;

            // Добавляем CurrentModelFilePath
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentModelFilePath))
                {
                    filesDict[fileComponent.CurrentModelFilePath] = (componentHash, component.Version, partNumber, isPurchased);
                    _logger.LogDebug($"Добавлен файл модели для {component.PartNumber} (Purchased: {isPurchased}): {fileComponent.CurrentModelFilePath}");
                }
            }

            // Добавляем CurrentDrawFilePath
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentDrawFilePath))
                {
                    filesDict[fileComponent.CurrentDrawFilePath] = (componentHash, component.Version, partNumber, isPurchased);
                    _logger.LogDebug($"Добавлен файл чертежа для {component.PartNumber} (Purchased: {isPurchased}): {fileComponent.CurrentDrawFilePath}");
                }
            }
        }

        public async Task<ComponentVersion?> GetComponentVersionAsync(string partNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос версии компонента: {partNumber} v{version}");

                // Этот метод только для чтения, поэтому не используем транзакцию
                return await _unitOfWork.ComponentRepository.GetComponentVersion(partNumber, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении версии компонента: {partNumber}");
                throw;
            }
        }

        // Дополнительный метод для получения последней версии компонента
        public async Task<ComponentVersion?> GetLatestComponentVersionAsync(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос последней версии компонента: {partNumber}");
                return await _unitOfWork.ComponentRepository.GetLatestComponentVersion(partNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента: {partNumber}");
                throw;
            }
        }

        // Дополнительный метод для получения структуры сборки
        public async Task<List<AssemblyStructure>> GetAssemblyStructureAsync(string assemblyPartNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос структуры сборки: {assemblyPartNumber} v{version}");
                return await _unitOfWork.ComponentRepository.GetAssemblyStructure(assemblyPartNumber, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении структуры сборки: {assemblyPartNumber}");
                throw;
            }
        }
    }
}
