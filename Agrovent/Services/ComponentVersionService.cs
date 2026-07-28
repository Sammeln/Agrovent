using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agrovent.DAL;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Services;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.TaskPane;
using Agrovent.ViewModels.Windows;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities.Components;
using EnumsNET;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.OLE.Interop;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Extensions;
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

        public ComponentVersionService(
            IUnitOfWork unitOfWork
            ,ILogger<ComponentVersionService> logger
            ,DataContext context
            ,IAGR_SaveProgressVM saveProgress
            ,IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _saveProgress = saveProgress as AGR_SaveProgressVM;
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component)
        {
            // 0. Генерируем PartNumber, если он пуст
            await GenerateMissingPartNumbersAsync(new[] { component });

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Начинаем сохранение компонента: {component.PartNumber}");
                _saveProgress.AddLogMessage($"Начинаем сохранение компонента: {component.PartNumber}");

                // 1. Проверяем, изменился ли компонент
                var hasChanged = await _unitOfWork.ComponentRepository.HasComponentChanged(component);

                if (hasChanged)
                {
                    _logger.LogInformation($"Компонент не изменился: {component.PartNumber}");
                    _saveProgress.AddLogMessage($"Компонент не изменился: {component.PartNumber}");

                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 2. Получаем существующую версию (в рамках той же транзакции)
                var existingVersion = await _unitOfWork.ComponentRepository.GetExistingVersion(component);

                if (existingVersion != null)
                {
                    _logger.LogInformation($"Компонент уже существует: {component.PartNumber} v{existingVersion.Version}");
                    _saveProgress.AddLogMessage($"Компонент уже существует: {component.PartNumber} v{existingVersion.Version}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // --- НОВОЕ: Вычисляем HashSum до сохранения ---
                var componentHash = component.CalculateComponentHash();
                component.HashSum = componentHash;
                _logger.LogDebug($"Вычислен HashSum для {component.PartNumber}: {componentHash}");
                _saveProgress.AddLogMessage($"Вычислен HashSum для {component.PartNumber}: {componentHash}");

                // --- КОНЕЦ НОВОГО ---

                // 3. Сохраняем компонент
                var savedVersion = await _unitOfWork.ComponentRepository.SaveComponent(component, component.CalculateComponentHash());

                // 4. Сохраняем все изменения
                var savedCount = await _unitOfWork.CompleteAsync();
                _logger.LogInformation($"Сохранено {savedCount} записей для компонента {component.PartNumber}");
                _saveProgress.AddLogMessage($"Сохранено {savedCount} записей для компонента {component.PartNumber}");

                // 5. Проверяем, что Id установлены
                if (savedVersion.Id == 0)
                {
                    _logger.LogError($"Id компонента {component.PartNumber} не установлен!");
                    _saveProgress.AddLogMessage($"Id компонента {component.PartNumber} не установлен!");

                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }


                _logger.LogInformation($"Компонент успешно сохранен: {component.PartNumber}, Id: {savedVersion.Id}");
                _saveProgress.AddLogMessage($"Компонент успешно сохранен: {component.PartNumber}, Id: {savedVersion.Id}");

                // --- НОВОЕ: Сохраняем файлы ---
                // Проверяем, является ли компонент файловым (имеет путь)
                if (component is IAGR_HasFile fileComponent && !string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
                {
                    try
                    {
                        await TrySaveDocuments(component);
                        await CopyFilesToStorageAsync(component, componentHash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при копировании файлов для компонента {component.PartNumber} (HashSum: {componentHash}) в хранилище.");
                        _saveProgress.AddLogMessage($"Ошибка при копировании файлов для компонента {component.PartNumber} (HashSum: {componentHash}) в хранилище. {ex.Message}");
                        // В зависимости от требований, можно:
                        // - Просто залогировать ошибку (как сейчас)
                        // - Выбросить исключение, чтобы сигнализировать о проблеме
                        // - Вернуть false, чтобы сигнализировать о частичном успехе
                        // - Записать статус ошибки в базу данных
                        // Пока просто логируем.
                    }
                }
                else
                {
                    _logger.LogDebug($"Компонент {component.PartNumber} не имеет связанного файла для сохранения (IAGR_HasFile или FilePath пуст).");
                    _saveProgress.AddLogMessage($"Компонент {component.PartNumber} не имеет связанного файла для сохранения (IAGR_HasFile или FilePath пуст).");
                }

                await _unitOfWork.CommitTransactionAsync();
                // --- КОНЕЦ НОВОГО ---
                var _viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
                var taskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
                if (taskPane != null)
                {
                    if (_viewModelCache != null)
                    {
                        _viewModelCache.Clear();
                    }
                    taskPane.ActiveView = null;
                    taskPane.BaseComponent = null;
                    taskPane.ActiveComponent = null;
                    taskPane.Selection = null;
                    taskPane.ActiveView = new AGR_HomePageVM();
                }

                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, $"Ошибка при сохранении компонента: {component.PartNumber}");
                _saveProgress.AddLogMessage($"Ошибка при сохранении компонента: {component.PartNumber}. {ex.Message}");
                throw;
            }
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

            var assemblyPartnumber = assembly.PartNumber;
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Начинаем сохранение сборки: {assembly.PartNumber}");
                _saveProgress.AddLogMessage($"Начинаем сохранение сборки: {assembly.PartNumber}");

                // 1. Проверяем, изменилась ли сборка
                var hasChanged = await _unitOfWork.ComponentRepository.HasComponentChanged(assembly);

                if (hasChanged)
                {
                    _logger.LogInformation($"Сборка не изменилась: {assembly.PartNumber}");
                    _saveProgress.AddLogMessage($"Сборка не изменилась: {assembly.PartNumber}");


                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 2. Получаем существующую версию сборки
                var existingVersion = await _unitOfWork.ComponentRepository.GetExistingVersion(assembly);

                if (existingVersion != null)
                {
                    _logger.LogInformation($"Сборка уже существует: {assembly.PartNumber} v{existingVersion.Version}");
                    _saveProgress.AddLogMessage($"Сборка уже существует: {assembly.PartNumber} v{existingVersion.Version}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // --- НОВОЕ: Вычисляем HashSum для сборки ---
                var assemblyHash = assembly.CalculateComponentHash();
                _logger.LogDebug($"Вычислен HashSum для сборки {assembly.PartNumber}: {assemblyHash}");
                _saveProgress.AddLogMessage($"Вычислен HashSum для сборки {assembly.PartNumber}: {assemblyHash}");
                // --- КОНЕЦ НОВОГО ---

                // 3. Сохраняем структуру сборки
                await _unitOfWork.ComponentRepository.SaveAssemblyStructure(assembly, assembly.GetChildComponents());

                // 4. Сохраняем все изменения и коммитим транзакцию
                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation($"Сборка успешно сохранена: {assembly.PartNumber}");
                _saveProgress.AddLogMessage($"Сборка успешно сохранена: {assembly.PartNumber}");

                // --- НОВОЕ: Сохраняем файлы сборки ---
                if (assembly is IAGR_HasFile assemblyFileComponent && !string.IsNullOrEmpty(assemblyFileComponent.CurrentModelFilePath))
                {
                    try
                    {
                        await TrySaveDocuments(assembly);
                        await CopyFilesToStorageAsync(assembly, assemblyHash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка при копировании файлов для сборки {assemblyPartnumber} (HashSum: {assemblyHash}) в хранилище.");
                        _saveProgress.AddLogMessage($"Ошибка при копировании файлов для сборки {assemblyPartnumber} (HashSum: {assemblyHash}) в хранилище. {ex.Message}");
                        // В зависимости от требований, можно обработать ошибку
                    }
                }
                else
                {
                    _logger.LogDebug($"Сборка {assemblyPartnumber} не имеет связанного файла для сохранения (IAGR_HasFile или FilePath пуст).");
                    _saveProgress.AddLogMessage($"Сборка {assemblyPartnumber} не имеет связанного файла для сохранения (IAGR_HasFile или FilePath пуст).");
                }
                // --- КОНЕЦ НОВОГО ---

                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, $"Ошибка при сохранении сборки: {assemblyPartnumber}");
                _saveProgress.AddLogMessage($"Ошибка при сохранении сборки: {assemblyPartnumber}. {ex.Message}. {ex.InnerException.InnerException.Message.ToString()}");
                throw;
            }
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
                if (attr.HasAnyFlags(FileAttributes.ReadOnly))
                {
                    continue;
                }
                item.Save();
            }
        }



        public async Task<AgroventInfrastructure.Entities.Components.Component> CreateNewComponent(IAGR_BaseComponent baseComponent)
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
            var filesToCopyInfo = new Dictionary<string, int>(); // Ключ: исходный путь, Значение: хэш компонента

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
                    if (filesToCopyInfo.TryGetValue(sourcePath, out int hashForPath))
                    {
                        var targetDir = Path.Combine(AGR_Options.StorageRootFolderPath, hashForPath.ToString("D10"));
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
                if (!filesToCopyInfo.TryGetValue(sourceFile, out int rootComponentHash))
                {
                    _logger.LogError($"Не найден хэш для исходного файла rootComponent {sourceFile}.");
                    _saveProgress.AddLogMessage($"Не найден хэш для исходного файла rootComponent {sourceFile}.");
                    //return; // Или выбросить исключение
                }
                var targetDirRoot = Path.Combine(AGR_Options.StorageRootFolderPath, rootComponentHash.ToString("D10"));
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
            var filesToCopyInfo = new Dictionary<string, (int Hash, string PartNumber, bool IsPurchased)>();

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
                            // Требование 3: Для purchased компонентов оставляем старую маску (StorageRootFolderPath/hash)
                            targetDir = Path.Combine(AGR_Options.StorageRootFolderPath, fileInfo.Hash.ToString("D10"));
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
                    targetDirRoot = Path.Combine(AGR_Options.StorageRootFolderPath, rootFileInfo.Hash.ToString("D10"));
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
        private void AddComponentFilesToDict(IAGR_BaseComponent component, Dictionary<string, int> filesDict)
        {
            if (!(component is IAGR_HasFile fileComponent))
            {
                _logger.LogDebug($"Компонент {component.PartNumber} не реализует IAGR_HasFile, пропускаем.");
                return;
            }

            var componentHash = component.CalculateComponentHash(); // Вычисляем хэш для текущего компонента

            // Добавляем CurrentModelFilePath
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentModelFilePath))
                {
                    filesDict[fileComponent.CurrentModelFilePath] = componentHash;
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
                    filesDict[fileComponent.CurrentDrawFilePath] = componentHash;
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
        private void AddComponentFilesToProductionDict(IAGR_BaseComponent component, Dictionary<string, (int Hash, string PartNumber, bool IsPurchased)> filesDict)
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
                    filesDict[fileComponent.CurrentModelFilePath] = (componentHash, partNumber, isPurchased);
                    _logger.LogDebug($"Добавлен файл модели для {component.PartNumber} (Purchased: {isPurchased}): {fileComponent.CurrentModelFilePath}");
                }
            }

            // Добавляем CurrentDrawFilePath
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                if (!filesDict.ContainsKey(fileComponent.CurrentDrawFilePath))
                {
                    filesDict[fileComponent.CurrentDrawFilePath] = (componentHash, partNumber, isPurchased);
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

        public async Task<bool> HasComponentChangedAsync(IAGR_BaseComponent component)
        {
            try
            {
                _logger.LogDebug($"Проверка изменений компонента: {component.PartNumber}");

                // Этот метод только для чтения, поэтому не используем транзакцию
                return await _unitOfWork.ComponentRepository.HasComponentChanged(component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке изменений компонента: {component.PartNumber}");
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