// File: Services/AGR_CommandService.cs
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Agrovent.DAL; // Для IUnitOfWork
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Extensions; // Для AGR_TryGetProp и т.д.
using Agrovent.Infrastructure.Helpers;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Services.Migration;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.PackNGo;
using Agrovent.ViewModels.Specification; // Для AGR_SpecificationViewModel
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.Base;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.Documents.Structures;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public class AGR_CommandService : IAGR_CommandService
    {
        private readonly ILogger _logger;
        private readonly IAGR_ComponentVersionService _componentVersionService; // Возможно, нужен для AvaArticle
        private readonly IServiceProvider _serviceProvider; // Необходим для получения VM
        private readonly IAGR_ViewModelCacheService _viewModelCache;
        private readonly IAGR_ComponentViewModelFactory _ComponentViewModelFactory;
        private readonly ISwApplication _swApp;
        private readonly IAGR_EditLockService _editLocks;

        public AGR_CommandService(
            ILogger<AGR_CommandService> logger,
            IAGR_ComponentVersionService componentVersionService,
            IServiceProvider serviceProvider,
            IAGR_ViewModelCacheService viewModelCache,
            IAGR_ComponentViewModelFactory viewModelFactory,
            ISwApplication swApp, // Принимаем IServiceProvider
            IAGR_EditLockService editLocks)
        {
            _editLocks = editLocks ?? throw new ArgumentNullException(nameof(editLocks));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _componentVersionService = componentVersionService ?? throw new ArgumentNullException(nameof(componentVersionService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _viewModelCache = viewModelCache ?? throw new ArgumentNullException(nameof(viewModelCache));
            _ComponentViewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
            _swApp = swApp;

        }

        public async Task<bool> UpdatePropertiesAsync()
        {
            if (_swApp == null)
            {
                AGR_Helper.ShowMessage("Приложение недоступно",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            var activeDoc = _swApp.Documents.Active;
            if (activeDoc == null)
            {
                AGR_Helper.ShowMessage("Нет активного документа.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            ISwDocument3D swDoc = activeDoc as ISwDocument3D;
            if (swDoc == null)
            {
                AGR_Helper.ShowMessage("Активный документ не является 3D-моделью.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            IAGR_BaseComponent component = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));


            if (component is AGR_PartComponentVM part)
            {
                part.RefreshFromDocument();
                AGR_Helper.ShowMessage($"Свойства {part.Name} обновлены.",
                   Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                   Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return true;
            }

            if (component is AGR_AssemblyComponentVM assembly)
            {
                string message;
                assembly.RefreshFromDocument();
                message = $"Свойства {assembly.Name} обновлены.\n";

                var componentsList = assembly
                    .GetFlatComponents(true)
                    .Select(x => x.Component as AGR_BaseComponent)
                    .ToList();
                foreach (var comp in componentsList.Where(x => x.IsPurchased == false))
                {
                    comp.RefreshFromDocument();
                    message += $"Свойства {comp.Name} обновлены.\n";
                }
                AGR_Helper.ShowMessage($"{message}",
                   Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                   Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
            }
            return false;
            //if (iComponent == null)
            //{
            //    _logger.LogWarning("UpdatePropertiesAsync called with null document.");
            //    return false;
            //}
            //var component = iComponent as AGR_BaseComponent;
            //var config = component.mConfiguration;
            //var props = config.Properties;

            //try
            //{
            //    _logger.LogDebug($"Starting property update for document: {component.mDocument.Title}");

            // 1. Обновление базовых свойств (Наименование, Обозначение, Признак, Расширение, Путь файла)
            // Эти свойства, скорее всего, управляются извне или из базы данных.
            // Предположим, что их значения нужно получить из соответствующего ViewModel или базы данных.
            // Пример (псевдокод - нужно интегрировать с логикой получения актуальных значов):
            // var componentData = await _componentVersionService.GetComponentByPartNumber(...);
            // props[AGR_PropertyNames.Name].Value = componentData.Name;
            // props[AGR_PropertyNames.Partnumber].Value = componentData.PartNumber;
            // props[AGR_PropertyNames.AvaType].Value = componentData.AvaType.ToString();
            // props[AGR_PropertyNames.Extension].Value = Path.GetExtension(document.Path);
            // props[AGR_PropertyNames.FilePath].Value = document.Path; // Не рекомендуется хранить путь в файле, но как пример

            // 2. Обновление массы
            //await UpdateMassPropertyAsync(config);

            // 3. Обновление специфических свойств для деталей
            //if (document is ISwPart)
            //{
            //    await UpdatePartSpecificPropertiesAsync(document, config);
            //}
            //else if (document is ISwAssembly)
            //{
            //    // Для сборок может быть логика обновления AvaArticle на основе дочерних компонентов
            //    // Это сложнее и требует отдельного обсуждения/реализации
            //    // await UpdateAssemblyAvaArticleAsync(document, props);
            //}

            // 4. Подтверждение изменений свойств
            //foreach (var prop in props)
            //{
            //    if (!prop.IsCommitted)
            //    {
            //        await prop.Commit(CancellationToken.None);
            //    }
            //}

            //    _logger.LogDebug($"Successfully updated properties for document: {component.mDocument.Title}");
            //    return true;
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, $"Error updating properties for document: {component.mDocument.Title}");
            //    return false;
            //}
        }
        public async Task<bool> OpenComponentRegistryAsync()
        {
            try
            {
                _logger.LogDebug("Открытие окна реестра компонентов");

                // Получаем ViewModel из DI контейнера
                var registryVM = AGR_ServiceContainer.GetService<AGR_ComponentRegistryVM>();
                if (registryVM == null)
                {
                    _logger.LogError("Не удалось получить AGR_ComponentRegistryVM из DI контейнера.");
                    return false;
                }
                registryVM.LoadDataCommand.Execute(null);

                // Создаем View и устанавливаем DataContext
                var registryView = new AGR_ComponentRegistryView
                {
                    DataContext = registryVM,
                    Title = "Реестр компонентов",
                    Width = 1200,
                    Height = 800,
                    ResizeMode = ResizeMode.CanResizeWithGrip
                };

                // Открываем окно (модально или немодально)
                registryView.Show(); // или window.Show(); для немодального окна

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна реестра компонентов");
                return false;
            }
        }
        public async Task<bool> OpenProjectExplorerWindowAsync()
        {
            try
            {
                _logger.LogDebug("Открытие окна проводника проектов");

                var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
                var vmLogger = _serviceProvider.GetRequiredService<ILogger<AGR_ProjectExplorerVM>>();

                var projectTreeVM = new AGR_ProjectExplorerVM(scopeFactory, vmLogger);
                _ = projectTreeVM.LoadProjectsAsync();

                var projectTreeView = new AGR_ProjectExplorerView
                {
                    DataContext = projectTreeVM,
                    Title = "Проводник проектов",
                    Width = 1200,
                    Height = 800,
                    ResizeMode = ResizeMode.CanResizeWithGrip
                };
                projectTreeVM.CloseRequested += (s, e) => projectTreeView.Close();

                projectTreeView.ShowDialog();


                projectTreeView.ShowDialog();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна проводника проектов");
                return false;
            }
        }
        public async Task<bool> SaveActiveComponentAsync()
        {
            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить ISwApplication.");
                    return false;
                }

                var activeDoc = swApp.Documents.Active;
                if (activeDoc == null)
                {
                    swApp.ShowMessageBox("Нет активного документа.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }

                ISwDocument3D swDoc = activeDoc as ISwDocument3D;
                if (swDoc == null)
                {
                    swApp.ShowMessageBox("Активный документ не является 3D-моделью.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }


                IAGR_BaseComponent component = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));

                var componentName = component.Name;
                var componentType = activeDoc is ISwAssembly ? "Сборка" : "Деталь";


                //если сборка - своё окно специфкации
                if (component.ComponentType == AGR_ComponentType_e.Assembly)
                {
                    // Для сборки показываем спецификацию
                    var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();
                    if (unitOfWork == null)
                    {
                        _logger.LogError("Не удалось получить IUnitOfWork из DI контейнера.");
                        return false;
                    }


                    var scopeFactory = AGR_ServiceContainer.GetService<IServiceScopeFactory>();

                    var specificationVM = new AGR_SpecificationViewModel((AGR_AssemblyComponentVM)component, unitOfWork, scopeFactory);
                    var specificationWindow = new AGR_SpecificationWindow
                    {
                        DataContext = specificationVM,
                        Title = specificationVM.WindowTitle,
                        WindowState = WindowState.Maximized,
                        ResizeMode = ResizeMode.CanResizeWithGrip,
                        ShowInTaskbar = true,
                        Topmost = true

                    };

                    specificationWindow.ShowDialog();

                    var dialogResult = specificationVM.DialogResult;
                    if (dialogResult != true)
                    {
                        _logger.LogInformation("Сохранение отменено пользователем.");
                        return false;
                    }
                    // Пользователь уже просмотрел спецификацию, значит подтверждает сохранение
                }
                //если деталь - своя форма
                else
                {
                    // Для детали показываем окно подтверждения сохранения
                    var confirmationVM = new AGR_SaveConfirmationVM(component, _logger);
                    var confirmationDialog = new SaveConfirmationView
                    {
                        DataContext = confirmationVM,
                        ShowInTaskbar = true,
                        Title = $"Сохранение {component.Name}"

                    };


                    var dialogResult = confirmationDialog.ShowDialog();

                    // Если пользователь нажал "Отмена" или закрыл окно - прерываем сохранение
                    if (confirmationVM.DialogResult != true)
                    {
                        _logger.LogInformation("Сохранение отменено пользователем.");
                        return false;
                    }
                }

                // --- ПОЛУЧАЕМ SINGLETON SaveProgressVM ---
                var progressVM = AGR_ServiceContainer.GetService<IAGR_SaveProgressVM>() as AGR_SaveProgressVM;
                if (progressVM == null)
                {
                    _logger.LogError("Не удалось получить SaveProgressVM из DI контейнера.");
                    return false;
                }

                // Очищаем лог перед началом
                progressVM.LogMessages.Clear();
                progressVM.AddLogMessage($"Начало процесса сохранения {componentType}: {componentName}");
                _logger.LogInformation($"Начало процесса сохранения {componentType}: {componentName}");

                // --- ПОКАЗЫВАЕМ ОКНО С ПРОГРЕССОМ (в UI-потоке SolidWorks) ---
                var progressDialog = new SaveProgressView();
                progressDialog.DataContext = progressVM;

                progressDialog.Show(); // Используем Show(), а не ShowDialog(), чтобы UI не блокировался *до* вызова сохранения
                progressDialog.ShowInTaskbar = true;

                bool saved = false;
                try
                {
                    if (activeDoc is ISwAssembly)
                    {
                        saved = await _componentVersionService.CheckAndSaveAssemblyAsync((AGR_AssemblyComponentVM)component);
                    }
                    else
                    {
                        // Сохраняем как деталь
                        saved = await _componentVersionService.CheckAndSaveComponentAsync(component);
                    }

                    if (saved)
                    {
                        progressVM.AddLogMessage($"Успешно сохранено: {componentName}");
                        _logger.LogInformation($"Успешно сохранено: {componentName}");
                    }
                    else
                    {
                        progressVM.AddLogMessage($"Компонент {componentName} не сохранён: не взят на редактирование.");
                        _logger.LogInformation($"Компонент {componentName} не сохранён: не взят на редактирование.");
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Ошибка при сохранении {componentType} {componentName}: {ex.Message}";
                    progressVM.AddLogMessage(errorMsg);
                    _logger.LogError(ex, errorMsg);
                }
                finally
                {
                    // Устанавливаем флаг завершения в любом случае (успешно или с ошибкой)
                    progressVM.SetFinished();
                }

                progressDialog.Activate();


                return saved; // Возвращаем результат сохранения
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при вызове SaveActiveComponentAsync");
                var swAppFallback = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swAppFallback != null)
                {
                    swAppFallback.ShowMessageBox($"Ошибка: {ex.Message}",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                }
                return false;
            }
        }
        public async Task<bool> CopyFilesToStorageAsync()
        {
            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить ISwApplication.");
                    return false;
                }

                var activeDoc = swApp.Documents.Active;
                if (activeDoc == null)
                {
                    swApp.ShowMessageBox("Нет активного документа.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }

                ISwDocument3D swDoc = activeDoc as ISwDocument3D;
                if (swDoc == null)
                {
                    swApp.ShowMessageBox("Активный документ не является 3D-моделью.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }


                IAGR_BaseComponent component = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));

                if (activeDoc is ISwAssembly)
                {
                    await _componentVersionService.CopyFilesToStorageAsync(component);
                    AGR_Helper.ShowMessage($"Копирование успешно выполнено", Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                    return true;

                }
                return false;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при вызове CopyFilesToStorageAsync");
                AGR_Helper.ShowMessage("Неожиданная ошибка при вызове CopyFilesToStorageAsync", Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                var swAppFallback = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swAppFallback != null)
                {
                    swAppFallback.ShowMessageBox($"Ошибка: {ex.Message}",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                }
                return false;
            }

        }
        public async Task<bool> CopyFilesToProdAsync()
        {
            try
            {
                var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить ISwApplication.");
                    return false;
                }

                var activeDoc = swApp.Documents.Active;
                if (activeDoc == null)
                {
                    swApp.ShowMessageBox("Нет активного документа.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }

                ISwDocument3D swDoc = activeDoc as ISwDocument3D;
                if (swDoc == null)
                {
                    swApp.ShowMessageBox("Активный документ не является 3D-моделью.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }


                IAGR_BaseComponent component = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));

                    await _componentVersionService.CopyFilesToProdAsync(component);
                    return true;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при вызове CopyFilesToProdAsync");
                var swAppFallback = AGR_ServiceContainer.GetService<ISwApplication>();
                if (swAppFallback != null)
                {
                    swAppFallback.ShowMessageBox($"Ошибка: {ex.Message}",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                }
                return false;
            }

        }
        public async Task<bool> UpdateDrawingsAsync()
        {
            var swApp = AGR_ServiceContainer.GetService<ISwApplication>();
            var progressVM = AGR_ServiceContainer.GetService<IAGR_SaveProgressVM>() as AGR_SaveProgressVM;

            try
            {
                if (swApp == null)
                {
                    _logger.LogError("Не удалось получить ISwApplication.");
                    return false;
                }

                var activeDoc = swApp.Documents.Active;
                if (activeDoc == null)
                {
                    swApp.ShowMessageBox("Нет активного документа.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }

                if (!(activeDoc is ISwDocument3D swDoc))
                {
                    swApp.ShowMessageBox("Активный документ должен быть деталью или сборкой.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                    return false;
                }

                // Собираем пути ко всем 3D-моделям, которые нужно проверить:
                // сам активный документ + (для сборки) все входящие детали/подсборки
                var modelPaths = new List<string> { swDoc.Path };

                if (swDoc is ISwAssembly swAssembly)
                {
                    var componentPaths = swAssembly.Configurations.Active.Components
                        .AGR_TryFlatten()
                        .Select(c => c.ReferencedDocument?.Path)
                        .Where(p => !string.IsNullOrEmpty(p));

                    modelPaths.AddRange(componentPaths);
                }

                var distinctModelPaths = modelPaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (progressVM == null)
                {
                    _logger.LogError("Не удалось получить SaveProgressVM из DI контейнера.");
                    return false;
                }

                // 1. Создаем источник токена отмены
                using var cts = new CancellationTokenSource();

                // --- Показываем окно прогресса ---
                progressVM.LogMessages.Clear();
                progressVM.IsFinished = false;
                progressVM.CancelationToken = cts;
                progressVM.AddLogMessage($"Начало обновления чертежей. Компонентов для проверки: {distinctModelPaths.Count}");

                var progressDialog = new SaveProgressView
                {
                    DataContext = progressVM,
                    Title = "Обновление чертежей...",
                    ShowInTaskbar = true,
                    Topmost = true
                };
                progressDialog.Show();

                int updated = 0;
                int missing = 0;
                int failed = 0;
                bool isCanceled = false;

                try
                {
                    foreach (var modelPath in distinctModelPaths)
                    {
                        if (progressVM.CancelationToken.IsCancellationRequested)
                        {
                            isCanceled = true;
                            break; // Выходим из цикла
                        }


                        var componentName = Path.GetFileNameWithoutExtension(modelPath);
                        var kindLabel = GetComponentKindLabel(modelPath); // "детали" / "сборки"
                        var drawingPath = Path.ChangeExtension(modelPath, ".slddrw");

                        if (!File.Exists(drawingPath))
                        {
                            missing++;
                            progressVM.AddLogMessage($"Чертеж {kindLabel} {componentName} не найден");
                            continue;
                        }

                        try
                        {
                            if (UpdateDrawing(swApp, drawingPath))
                            {
                                updated++;
                                progressVM.AddLogMessage($"Обновлен чертеж {kindLabel} {componentName}");
                            }
                            else
                            {
                                failed++;
                                progressVM.AddLogMessage($"Не удалось обновить чертеж {kindLabel} {componentName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            var msg = $"Ошибка при обновлении чертежа {kindLabel} {componentName}: {ex.Message}";
                            progressVM.AddLogMessage(msg);
                            _logger.LogError(ex, msg);
                        }

                        // 3. ВАЖНО: Возвращаем управление UI-потоку, чтобы окно могло обработать нажатие кнопки "Закрыть"
                        // и обновить интерфейс. Без этого цикл может "заморозить" окно.
                        await Dispatcher.Yield(DispatcherPriority.Input);

                        if (cts.Token.IsCancellationRequested)
                        {
                            isCanceled = true;
                            break;
                        }
                    }

                    // 4. Формируем итоговый отчет
                    progressVM.AddLogMessage("──────────────────────");
                    if (isCanceled)
                    {
                        progressVM.AddLogMessage($"Операция прервана пользователем. Частичный результат: обновлено — {updated}, без чертежа — {missing}, ошибок — {failed}");
                    }
                    else
                    {
                        progressVM.AddLogMessage($"Итого: обновлено — {updated}, без чертежа — {missing}, ошибок — {failed}");
                    }
                }
                finally
                {
                    progressVM.SetFinished();
                }

                progressDialog.Activate();

                // Возвращаем true только если не было отмены и нет ошибок
                return !isCanceled && failed == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при вызове UpdateDrawingsAsync");
                progressVM?.AddLogMessage($"Ошибка: {ex.Message}");
                progressVM?.SetFinished();
                swApp?.ShowMessageBox($"Ошибка: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                return false;
            }
        }
        public async Task<bool> GetSheetMetallPartsAssmbly()
        {
            if (_swApp == null)
            {
                AGR_Helper.ShowMessage("Приложение недоступно",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            var activeDoc = _swApp.Documents.Active;
            if (activeDoc == null)
            {
                AGR_Helper.ShowMessage("Нет активного документа.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            ISwDocument3D swDoc = activeDoc as ISwDocument3D;
            if (swDoc == null)
            {
                AGR_Helper.ShowMessage("Активный документ не является 3D-моделью.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            IAGR_BaseComponent component = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));

            if (component is AGR_AssemblyComponentVM assembly)
            {
                var sheetMetalComponents = assembly
                                    .GetFlatComponents(true)
                                    .Select(x => x.Component as AGR_BaseComponent)
                                    .Where(c => c != null && c.ComponentType == AGR_ComponentType_e.SheetMetallPart) // Предполагаем, что есть свойство IsSheetMetal
                                    .ToList();
                if (sheetMetalComponents.Count == 0)
                {
                    AGR_Helper.ShowMessage("В сборке нет листовых деталей.",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
                        Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                    return false;
                }

                //Создание документа сборки
                var targetAssem = _swApp.Documents.NewAssembly() as ISwAssembly;

                //устанавливаем как активный документ
                _swApp.Documents.Active = targetAssem;

                foreach (var item in sheetMetalComponents)
                {
                    // Создаем шаблон компонента
                    var xComp = targetAssem.Configurations.Active.Components.PreCreate<IXComponent>();
                    if (xComp == null)
                    {
                        _logger.LogError($"Команда 'Добавить в сборку': Не удалось создать шаблон компонента для {item.Name}");
                        return false;
                    }

                    // Устанавливаем ссылку на документ
                    xComp.ReferencedDocument = item.SwDocument;

                    // Добавляем в сборку
                    targetAssem.Configurations.Active.Components.Add(xComp);
                }

                return true;
            }
            else
            {
                AGR_Helper.ShowMessage("Активный документ не является сборкой.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }
        }

        /// <summary>
        /// Определяет "деталь" это или "сборка" по расширению файла — без лишних обращений к COM.
        /// </summary>
        private static string GetComponentKindLabel(string modelPath)
        {
            var ext = Path.GetExtension(modelPath);
            return string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase)
                ? "сборки"
                : "детали";
        }

        /// <summary>
        /// Открывает чертёж, делает Rebuild, сохраняет и закрывает
        /// (если он не был открыт пользователем ранее).
        /// </summary>
        private bool UpdateDrawing(ISwApplication swApp, string drawingPath)
        {
            var alreadyOpenDoc = swApp.Documents
                .FirstOrDefault(d => string.Equals(d.Path, drawingPath, StringComparison.OrdinalIgnoreCase));

            var wasAlreadyOpen = alreadyOpenDoc != null;

            var drawingDoc = alreadyOpenDoc
                ?? swApp.Documents.Open(drawingPath, Xarial.XCad.Documents.Enums.DocumentState_e.Silent);

            if (drawingDoc == null)
            {
                _logger.LogWarning($"Не удалось открыть чертёж: {drawingPath}");
                return false;
            }

            try
            {
                var model = (drawingDoc as ISwDocument)?.Model as ModelDoc2;
                if (model == null)
                {
                    _logger.LogWarning($"Не удалось получить нативный документ SolidWorks для {drawingPath}");
                    return false;
                }

                model.Extension.LoadDraftingStandard(@"\\192.168.10.1\kd\DataFiles\Форматки\Агровент.sldstd");

                var sheetCollection = (drawingDoc as ISwDrawing)?.Sheets;

                if (sheetCollection != null && sheetCollection.Count > 0)
                {
                    foreach (var sheet in sheetCollection)
                    {
                        var width = sheet.PaperSize.Width;
                        var height = sheet.PaperSize.Height;
                        bool isFirstSheet = sheet.Name.Contains("Лист1") || sheet.Name.Contains("Sheet1");
                        string templateName = string.Empty;

                        switch (sheet.PaperSize.StandardPaperSize)
                        {
                            case Xarial.XCad.Documents.Enums.StandardPaperSize_e.A4Landscape:
                                templateName = "А4-1 (Альбомный).slddrt";
                            break;
                            case Xarial.XCad.Documents.Enums.StandardPaperSize_e.A4Portrait:
                                if (isFirstSheet) templateName = "А4-1 (1С).slddrt";
                                else templateName = "А4-2 (1С).slddrt";
                            break;
                            case Xarial.XCad.Documents.Enums.StandardPaperSize_e.A3Landscape:
                                if (isFirstSheet) templateName = "А3-1 (1С).slddrt";
                                else templateName = "А3-2 (1С).slddrt";
                            break;
                            case Xarial.XCad.Documents.Enums.StandardPaperSize_e.A2Landscape:
                                if (isFirstSheet) templateName = "А2-1 (1С).slddrt";
                                else templateName = "А2-2 (1С).slddrt";
                            break;
                            case Xarial.XCad.Documents.Enums.StandardPaperSize_e.A1Landscape:
                                if (isFirstSheet) templateName = "А1-1 (1С).slddrt";
                                else templateName = "А1-2 (1С).slddrt";
                            break;
                            default:
                            break;
                        }

                            (model as DrawingDoc).SetupSheet5(
                                Name: sheet.Name,
                                PaperSize: 12,
                                TemplateIn: 12,
                                Scale1: sheet.Scale.Numerator,
                                Scale2: sheet.Scale.Denominator,
                                FirstAngle: true,
                                TemplateName: templateName,
                                Width: width,
                                Height: height,
                                PropertyViewName: "По умолчанию",
                                RemoveModifiedNotes: true
                                );
                    }
                }

                model.ForceRebuild3(false);

                int errors = 0;
                int warnings = 0;
                model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);

                if (errors != 0)
                {
                    _logger.LogWarning($"При сохранении чертежа {drawingPath} возникли ошибки (код {errors}).");
                    return false;
                }

                _logger.LogDebug($"Чертёж обновлён: {drawingPath}");
                return true;
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    drawingDoc.Close();
                }
            }
        }
        public async Task<bool> PackNGoAsync()
        {
            if (_swApp == null)
            {
                AGR_Helper.ShowMessage("Приложение недоступно",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            var activeDoc = _swApp.Documents.Active;
            if (activeDoc == null)
            {
                AGR_Helper.ShowMessage("Нет активного документа.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            ISwDocument3D swDoc = activeDoc as ISwDocument3D;
            if (swDoc == null)
            {
                AGR_Helper.ShowMessage("Активный документ не является деталью или сборкой.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            var rootComponent = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));

            // Свежий IServiceScopeFactory/ILogger<T> берём из уже имеющегося IServiceProvider -
            // сам AGR_PackNGoVM использует IServiceScopeFactory.CreateScope() на каждую операцию
            // с БД (генерация партномеров), не переиспользуя закэшированный на плагин UnitOfWork.
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var vmLogger = _serviceProvider.GetRequiredService<ILogger<AGR_PackNGoVM>>();

            var vm = new AGR_PackNGoVM(rootComponent, _swApp, vmLogger);
            var window = new AGR_PackNGoWindow
            {
                DataContext = vm
            };

            window.ShowDialog();

            return vm.DialogResult == true;
        }

        #region Миграция и диагностика (временно, удалить после миграции хранилища)

        /// <summary>Отчёт перед миграцией: ничего не меняет. Файлы отчёта в Документы\Agrovent\Migration.</summary>
        public async Task<bool> MigrationReportAsync()
        {
            EnsureUiSynchronizationContext();

            if (_swApp.Documents.Count > 0)
            {
                // Открытые документы мешают: SolidWorks подставляет уже открытый файл с тем же именем
                ShowError("Закройте все документы перед построением отчёта.");
                return false;
            }

            var answer = MessageBox.Show(
                "Отчёт перед миграцией хранилища. БД и файлы не меняются.\n\n"
                + "Да - полный: БД, папки, ссылки сборок и чертежей + открытие каждой модели (Обозначение по конфигурациям, маркер). Долго.\n"
                + "Нет - быстрый: БД, папки, ссылки (модели не открываются).\n\n"
                + $"Прервать проход: создать файл {AGR_MigrationReportService.StopFileName} в папке отчёта.",
                "Миграция: отчёт", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return false;

            try
            {
                var service = _serviceProvider.GetRequiredService<IAGR_MigrationReportService>();
                var folder = await service.BuildReportAsync(answer == MessageBoxResult.Yes);
                AGR_Helper.ShowMessage($"Отчёт готов:\n{folder}\n\nНачните с summary.txt.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                try { System.Diagnostics.Process.Start("explorer.exe", folder); } catch { /* не критично */ }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Миграция: ошибка построения отчёта.");
                ShowError($"Ошибка построения отчёта: {ex.Message}");
                return false;
            }
        }

        /// <summary>Слияние дублей покупных (до миграции хранилища): поиск групп и отчёт, затем по подтверждению слияние.
        /// Нужна таблица MigrationLog (SQL в EditingRework.md).</summary>
        public async Task<bool> MergePurchasedDuplicatesAsync()
        {
            EnsureUiSynchronizationContext();
            var service = _serviceProvider.GetRequiredService<IAGR_PurchasedDuplicatesService>();

            AGR_DuplicatesResult result;
            try
            {
                result = await service.AnalyzeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Дубли покупных: ошибка поиска.");
                ShowError($"Ошибка поиска дублей: {ex.Message}");
                return false;
            }
            try { System.Diagnostics.Process.Start("explorer.exe", result.Folder); } catch { /* не критично */ }

            if (result.ComponentsToDelete == 0)
            {
                AGR_Helper.ShowMessage($"Дублей покупных к слиянию нет (групп пропущено: {result.Groups.Count}).\n{result.Folder}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return true;
            }

            var answer = MessageBox.Show(
                $"Групп дублей: {result.Groups.Count}, к слиянию: {result.ToMerge.Count()}, пропущено: {result.Groups.Count(g => g.Blocked != null)}.\n"
                + $"Будет удалено компонентов: {result.ComponentsToDelete}, версий: {result.VersionsToDelete}.\n\n"
                + $"Отчёт: {result.Folder} (duplicates.csv, summary.txt).\n\n"
                + "Да - слить (одна транзакция, БД меняется). Нет - только отчёт.",
                "Миграция: дубли покупных", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return true;

            try
            {
                var applied = await service.ApplyAsync(result);
                AGR_Helper.ShowMessage(
                    $"Слито групп: {applied.Groups}. Удалено компонентов: {applied.Components}, версий: {applied.Versions}.\n"
                    + $"AssemblyStructures: переведено {applied.StructureRows}, склеено {applied.StructureRowsMerged}.\n"
                    + $"ProjectComponents: переведено {applied.ProjectRows}, удалено повторов {applied.ProjectRowsMerged}.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return true;
            }
            catch (Exception ex)
            {
                ShowError($"Слияние не выполнено, изменения откатаны: {ex.Message}");
                return false;
            }
        }

        /// <summary>Миграция хранилища, этап 3: копирование версий в guid\версия и перенаправление ссылок.
        /// Повторный запуск продолжает с неперенесённых (журнал MigrationLog).</summary>
        public async Task<bool> MigrateStorageAsync()
        {
            EnsureUiSynchronizationContext();

            if (_swApp.Documents.Count > 0)
            {
                // SolidWorks подставляет уже открытый файл с тем же именем
                ShowError("Закройте все документы перед миграцией.");
                return false;
            }

            var answer = MessageBox.Show(
                "Миграция хранилища.\n\n"
                + $"Каждая версия копируется в {AgroventInfrastructure.AGR_Options.StorageRootFolderPath}\\<guid>\\<версия>, ссылки сборок и чертежей "
                + "перенаправляются на новые файлы. Копия модели открывается: «Обозначение» из БД в Custom файла, "
                + "удаление «Обозначения» из конфигураций и «Версии»/«Хэш-суммы», отпечатки, маркер, сохранение. "
                + "В БД: ComponentFiles.FilePath и отпечатки версии. Старые файлы не меняются, ход пишется в MigrationLog.\n\n"
                + "До запуска: SQL подготовки (StorageGuid, MigrationLog), слияние дублей покупных, нет замков, "
                + "сетевое хранилище недоступно (тест).\n\n"
                + $"Прервать: создать файл {AGR_MigrationReportService.StopFileName} в папке прогона (Документы\\Agrovent\\Migration\\Migration_<время>).\n\n"
                + "Начать?",
                "Миграция: перенос файлов", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK) return false;

            try
            {
                var service = _serviceProvider.GetRequiredService<IAGR_StorageMigrationService>();
                var run = await service.RunAsync();
                AGR_Helper.ShowMessage(
                    (run.Stopped ? "ПРЕРВАНО файлом STOP.\n" : "")
                    + $"Версий: {run.Total}, перенесено: {run.Done}, уже Done: {run.Skipped}, ошибок: {run.Errors}.\n"
                    + $"Ссылок заменено: {run.ReferencesReplaced}, не разрешено: {run.ReferencesUnresolved}.\n"
                    + $"Время: {run.Elapsed:hh\\:mm\\:ss}\n\n{run.Folder}\n(summary.txt, refs.csv)",
                    run.Errors > 0 || run.ReferencesUnresolved > 0 ? Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning : Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                try { System.Diagnostics.Process.Start("explorer.exe", run.Folder); } catch { /* не критично */ }
                return run.Errors == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Миграция: ошибка переноса файлов.");
                ShowError($"Ошибка миграции: {ex.Message}");
                return false;
            }
        }

        /// <summary>Диагностика маркера: маркер, текущий отпечаток, Обозначение по конфигурациям.
        /// Каноническая строка отпечатка пишется в Документы\Agrovent\Fingerprint (сравнивать два файла diff-ом).</summary>
        public Task<bool> ShowMarkerInfoAsync()
        {
            var doc = GetActiveDocument3D();
            if (doc == null) return Task.FromResult(false);

            try
            {
                var markers = _serviceProvider.GetRequiredService<IAGR_MarkerService>();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Файл: {doc.Path}");

                switch (markers.TryRead(doc, out var marker, out var error))
                {
                    case AGR_MarkerReadResult_e.Ok:
                        sb.AppendLine($"Маркер: {marker!.PartNumber} v{marker.Version}, Id {marker.ComponentId}, схема {marker.SchemaVersion}");
                        sb.AppendLine($"GUID: {marker.StorageGuid:D}, алгоритм отпечатков {marker.FingerprintVersion}" +
                                      (marker.FingerprintsComparable ? "" : $" (текущий {AGR_Marker.CurrentFingerprintVersion}: не сравнивается)"));
                        break;
                    case AGR_MarkerReadResult_e.Invalid:
                        sb.AppendLine($"Маркер ПОВРЕЖДЁН: {error}");
                        break;
                    default:
                        sb.AppendLine("Маркера нет");
                        break;
                }

                var geometry = AGR_GeometryFingerprint.Compute(doc, out var geometryCanonical);
                var properties = AGR_PropertiesFingerprint.Compute(doc, out var propertiesCanonical);
                AppendFingerprint(sb, "Геометрия", marker?.GeometryFingerprint, geometry, marker?.FingerprintsComparable == true);
                AppendFingerprint(sb, "Свойства", marker?.PropertiesFingerprint, properties, marker?.FingerprintsComparable == true);

                sb.AppendLine();
                var fileLevel = AGR_PropertyReader.Read(doc.Model, "", AgroventInfrastructure.Enums.AGR_PropertyNames.Partnumber);
                sb.AppendLine($"Обозначение (файл): {fileLevel ?? "(нет)"}");
                foreach (var (config, value) in AGR_PropertyReader.ReadByConfigurations(doc.Model, AgroventInfrastructure.Enums.AGR_PropertyNames.Partnumber))
                    sb.AppendLine($"Обозначение ({config}): {value ?? "(нет)"}");

                var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Agrovent", "Fingerprint");
                Directory.CreateDirectory(dir);
                var name = string.IsNullOrEmpty(doc.Path) ? doc.Title : Path.GetFileNameWithoutExtension(doc.Path);
                var file = Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file,
                    sb + "\r\n[Геометрия]\r\n" + geometryCanonical + "\r\n[Свойства]\r\n" + propertiesCanonical,
                    new System.Text.UTF8Encoding(true));

                sb.AppendLine();
                sb.AppendLine($"Каноническая строка: {file}");

                if (marker == null && error != null)
                {
                    // Повреждённый или тестовый маркер (этап 1) останавливает сохранение: даём обнулить
                    sb.AppendLine();
                    sb.AppendLine("Обнулить маркер? Документ будет сохранён, файл станет «без маркера».");
                    if (MessageBox.Show(sb.ToString(), "Маркер и отпечаток", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        if (markers.SaveWithMarker(doc, null, out var saveError))
                            AGR_Helper.ShowMessage("Маркер обнулён.", Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                        else
                            ShowError($"Маркер не обнулён: {saveError}");
                    }
                    return Task.FromResult(true);
                }

                AGR_Helper.ShowMessage(sb.ToString(), Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info, Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Диагностика маркера: ошибка.");
                ShowError($"Ошибка диагностики маркера: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        private static void AppendFingerprint(System.Text.StringBuilder sb, string title, string? inMarker, string current, bool comparable)
        {
            sb.AppendLine($"{title}: в маркере {(string.IsNullOrEmpty(inMarker) ? "(нет)" : inMarker)}");
            sb.AppendLine($"{title}: сейчас    {current}");
            if (!string.IsNullOrEmpty(inMarker) && comparable)
                sb.AppendLine(inMarker == current ? $"{title}: СОВПАДАЕТ" : $"{title}: НЕ СОВПАДАЕТ");
        }

        #endregion

        #region Редактирование (замки + локальная копия)

        /// <summary>
        /// «Взять на редактирование»: окно выбора → замки (всё или ничего) → копирование всей сборки
        /// в локальную папку → открытие копии. Если активный документ уже в LocalWorkFolder,
        /// только берём замки на новые отмеченные компоненты.
        /// </summary>
        public async Task<bool> EditActiveComponentAsync()
        {
            EnsureUiSynchronizationContext();

            var swDoc = GetActiveDocument3D();
            if (swDoc == null) return false;

            var root = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));
            if (root == null) return false;

            AGR_EditSelectionViewModel vm;
            try
            {
                vm = await AGR_EditSelectionViewModel.CreateAsync(AGR_EditSelectionMode_e.Edit, root, _editLocks, _serviceProvider.GetRequiredService<IAGR_MarkerService>(), _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Редактирование: не удалось построить дерево компонентов.");
                ShowError($"Не удалось загрузить состав изделия: {ex.Message}");
                return false;
            }

            new AGR_EditSelectionWindow { DataContext = vm }.ShowDialog();
            if (vm.DialogResult != true) return false;

            var newItems = vm.GetCheckedComponents()
                .Where(n => n.State != AGR_EditState_e.EditingByMe)
                .ToList();
            if (newItems.Count == 0) return false;

            var rootPath = swDoc.Path;
            List<(string Source, string Target)>? files = null;

            // Всё, что может отменить операцию, спрашиваем ДО взятия замков
            if (vm.IsCopyRequired)
            {
                files = CollectFilesForLocalCopy(rootPath, vm.TargetFolder, out var error);
                if (files == null)
                {
                    ShowError(error);
                    return false;
                }
                if (!ConfirmCloseDirtyDocuments()) return false;
            }

            var conflicts = await _editLocks.TryTakeAsync(
                newItems.Select(n => (n.ComponentId, n.Version)).ToList(),
                System.Environment.MachineName);
            if (conflicts.Count > 0)
            {
                var lines = conflicts.Select(c => $"{c.PartNumber}: {c.UserName} с {c.TakenAt.ToLocalTime():dd.MM.yyyy HH:mm}");
                AGR_Helper.ShowMessage("Компоненты уже взяты на редактирование другим пользователем:\n" + string.Join("\n", lines),
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            var takenIds = newItems.Select(n => n.ComponentId).ToList();

            if (vm.IsCopyRequired)
            {
                var targetRoot = files!.First(f => string.Equals(f.Source, rootPath, StringComparison.OrdinalIgnoreCase)).Target;

                if (!CopyToLocalFolder(rootPath, targetRoot, files!, vm.TargetFolder))
                {
                    // Компенсация: снимаем только что взятые замки, возвращаем исходный документ
                    await _editLocks.ReleaseAsync(takenIds);
                    TryOpenDocument(rootPath);
                    ShowError("Не удалось скопировать файлы в локальную папку. Замки сняты. Подробности в логе.");
                    return false;
                }

                TryOpenDocument(targetRoot);
            }
            else
            {
                // Документы не переоткрывались: обновляем состояние кэшированных VM на месте
                var allMine = vm.GetCheckedComponents().Select(n => n.ComponentId).ToHashSet();
                foreach (var cached in GetCachedViewModels())
                {
                    var id = cached.ComponentVersion?.ComponentId;
                    if (id == null || !allMine.Contains(id.Value)) continue;
                    cached.EditState = AGR_EditState_e.EditingByMe;
                    cached.EditLockedBy = null;
                    cached.HasMyLocks = true;
                }
                root.HasMyLocks = true;
            }

            _logger.LogInformation($"Редактирование: взято {takenIds.Count} компонентов ({string.Join(", ", newItems.Select(n => n.PartNumber))}).");
            AGR_Helper.ShowMessage(
                $"Взято на редактирование: {takenIds.Count}." + (vm.IsCopyRequired ? $"\nЛокальная копия: {vm.TargetFolder}" : ""),
                Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
                Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
            return true;
        }

        /// <summary>«Отменить редактирование»: окно со своими замками в этой сборке → снятие.
        /// Локальные файлы не трогаем.</summary>
        public async Task<bool> CancelEditingAsync()
        {
            EnsureUiSynchronizationContext();

            var swDoc = GetActiveDocument3D();
            if (swDoc == null) return false;

            var root = _viewModelCache.GetOrCreate(swDoc, d => _ComponentViewModelFactory.CreateComponent(d));
            if (root == null) return false;

            AGR_EditSelectionViewModel vm;
            try
            {
                vm = await AGR_EditSelectionViewModel.CreateAsync(AGR_EditSelectionMode_e.Cancel, root, _editLocks, _serviceProvider.GetRequiredService<IAGR_MarkerService>(), _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Отмена редактирования: не удалось построить дерево компонентов.");
                ShowError($"Не удалось загрузить состав изделия: {ex.Message}");
                return false;
            }

            if (!vm.Flatten().Any(n => n.State == AGR_EditState_e.EditingByMe))
            {
                AGR_Helper.ShowMessage("В этом изделии нет компонентов, взятых вами на редактирование.",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
                    Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                return false;
            }

            new AGR_EditSelectionWindow { DataContext = vm }.ShowDialog();
            if (vm.DialogResult != true) return false;

            var released = vm.GetCheckedComponents().Select(n => n.ComponentId).ToHashSet();
            if (released.Count == 0) return false;

            await _editLocks.ReleaseAsync(released);

            // Снятые компоненты свободны; HasMyLocks сборок пересчитываем (у них могли остаться другие замки)
            foreach (var cached in GetCachedViewModels())
            {
                var id = cached.ComponentVersion?.ComponentId;
                if (id != null && released.Contains(id.Value))
                {
                    cached.EditState = AGR_EditState_e.Free;
                    cached.EditLockedBy = null;
                    cached.HasMyLocks = false;
                }
                if (cached.HasMyLocks && cached.ComponentType == AGR_ComponentType_e.Assembly)
                    await _editLocks.RefreshEditStateAsync(cached);
            }
            await _editLocks.RefreshEditStateAsync(root);

            _logger.LogInformation($"Отмена редактирования: снято {released.Count} замков.");
            return true;
        }

        /// <summary>
        /// Полный список файлов изделия для CopyDocument: все зависимости (включая подавленные,
        /// ExcludedFromBom, Envelope и содержимое покупных сборок) + чертежи с тем же именем рядом с моделью.
        /// Все файлы кладутся плоско в targetFolder. null = ошибка (текст в error).
        /// </summary>
        private List<(string Source, string Target)>? CollectFilesForLocalCopy(string rootPath, string targetFolder, out string error)
        {
            error = "";
            var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootPath };

            try
            {
                var deps = _swApp.Sw.GetDocumentDependencies2(rootPath, true, true, false) as object[];
                if (deps != null)
                {
                    foreach (var d in deps.OfType<string>())
                    {
                        if (Path.IsPathRooted(d) && File.Exists(d)) models.Add(d);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Редактирование: GetDocumentDependencies2 для {rootPath}.");
                error = $"Не удалось получить список файлов изделия: {ex.Message}";
                return null;
            }

            // Встроенные/виртуальные компоненты лежат во временной папке, их не копируем
            models.RemoveWhere(p => p.Contains("Local\\Temp", StringComparison.OrdinalIgnoreCase));

            var sources = new List<string>(models);
            foreach (var model in models)
            {
                var drawing = Path.ChangeExtension(model, ".slddrw");
                if (File.Exists(drawing)) sources.Add(drawing);
            }

            // Копия плоская: одинаковые имена из разных папок затрут друг друга
            var duplicates = sources
                .GroupBy(s => Path.GetFileName(s), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .ToList();
            if (duplicates.Count > 0)
            {
                error = "Файлы с одинаковыми именами в разных папках, копирование в одну папку невозможно:\n"
                    + string.Join("\n", duplicates.SelectMany(g => g));
                return null;
            }

            _logger.LogDebug($"Редактирование: к копированию {sources.Count} файлов из {rootPath}.");
            return sources
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => (s, Path.Combine(targetFolder, Path.GetFileName(s))))
                .ToList();
        }

        /// <summary>Есть несохранённые документы → спросить «закрыть без сохранения / отмена».
        /// true = можно закрывать всё.</summary>
        private bool ConfirmCloseDirtyDocuments()
        {
            var dirty = new List<string>();
            foreach (var doc in _swApp.Documents)
            {
                try
                {
                    if (doc is ISwDocument sw && sw.Model.GetSaveFlag()) dirty.Add(doc.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Редактирование: не удалось проверить флаг сохранения документа.");
                }
            }
            if (dirty.Count == 0) return true;

            var answer = MessageBox.Show(
                "Перед копированием все документы будут закрыты. Несохранённые документы:\n"
                + string.Join("\n", dirty)
                + "\n\nЗакрыть БЕЗ сохранения? Изменения будут потеряны.",
                "Взять на редактирование", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return answer == MessageBoxResult.Yes;
        }

        /// <summary>CloseAllDocuments + CopyDocument (ссылки переназначаются на копии) + снятие ReadOnly.</summary>
        private bool CopyToLocalFolder(string rootSource, string rootTarget, List<(string Source, string Target)> files, string targetFolder)
        {
            try
            {
                Directory.CreateDirectory(targetFolder);

                _swApp.Sw.CloseAllDocuments(true); // true - без запросов на сохранение (спросили раньше)

                var errors = _swApp.Sw.CopyDocument(
                    rootSource,
                    rootTarget,
                    files.Select(f => f.Source).ToArray(),
                    files.Select(f => f.Target).ToArray(),
                    (int)swMoveCopyOptions_e.swMoveCopyOptionsOverwriteExistingDocs);

                if (errors != 0)
                {
                    _logger.LogError($"Редактирование: CopyDocument вернул код {errors} ({rootSource} -> {rootTarget}).");
                    return false;
                }

                // Файлы из хранилища только для чтения: в локальной копии снимаем атрибут
                foreach (var (_, target) in files)
                {
                    if (File.Exists(target) && File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly))
                        File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
                }

                _logger.LogInformation($"Редактирование: скопировано {files.Count} файлов в {targetFolder}.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Редактирование: ошибка копирования в {targetFolder}.");
                return false;
            }
        }

        private void TryOpenDocument(string path)
        {
            try
            {
                _swApp.Documents.Open(path, DocumentState_e.Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Не удалось открыть {path}.");
                ShowError($"Не удалось открыть {path}: {ex.Message}");
            }
        }

        private ISwDocument3D? GetActiveDocument3D()
        {
            if (_swApp?.Documents.Active is ISwDocument3D doc) return doc;

            AGR_Helper.ShowMessage("Активный документ не является деталью или сборкой.",
                Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning,
                Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
            return null;
        }

        private IEnumerable<IAGR_BaseComponent> GetCachedViewModels() => _viewModelCache.ViewModelsDictonary.Values
            .Select(v => v.ViewModel)
            .Where(v => v != null)
            .ToList();

        private static void ShowError(string message) => AGR_Helper.ShowMessage(message,
            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error,
            Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);

        private static void EnsureUiSynchronizationContext()
        {
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        }

        #endregion
        private async Task UpdateMassPropertyAsync(ISwConfiguration configuration)
        {
            ISwDocument3D document3D = configuration.OwnerDocument as ISwDocument3D;

            var massProp = configuration.Properties.GetOrPreCreate(AGR_PropertyNames.BlankMass); // Или другое имя для итоговой массы

            if (massProp != null)
            {
                var massPrp = document3D.Evaluation.PreCreateMassProperty();
                if (!massPrp.IsCommitted) massPrp.Commit(CancellationToken.None);
                var mass = massPrp.Mass; // Получаем массу в кг

                // Форматирование значения массы (например, 2 знака после запятой)
                massProp.Value = Math.Round(mass, 3).ToString(); // SolidWorks свойства обычно строковые
                _logger.LogDebug($"Updated mass property to: {mass} kg");
            }
            else
            {
                _logger.LogWarning($"Could not access SolidWorks specific property for mass update in document: {document3D.Title}");
            }
        }
        private async Task UpdatePartSpecificPropertiesAsync(ISwDocument3D document, IXConfiguration configuration)
        {
            // Пример: обновление длины/ширины/толщины заготовки для листовых деталей
            // Эта логика может быть сложной и зависеть от геометрии.
            // Псевдокод:
            /*
            if (IsSheetMetalPart(document)) // Нужно реализовать метод определения листовой детали
            {
                var length = GetSheetMetalLength(document); // Нужно реализовать
                var width = GetSheetMetalWidth(document);  // Нужно реализовать
                var thickness = GetSheetMetalThickness(document); // Используем существующий метод или получаем из геометрии

                var props = configuration.Properties;
                var lengthProp = props.GetOrPreCreate(AGR_PropertyNames.BlankLen);
                var widthProp = props.GetOrPreCreate(AGR_PropertyNames.BlankWid);
                var thickProp = props.GetOrPreCreate(AGR_PropertyNames.BlankThick);

                lengthProp.Value = length.ToString();
                widthProp.Value = width.ToString();
                thickProp.Value = thickness.ToString();

                await lengthProp.Commit(CancellationToken.None);
                await widthProp.Commit(CancellationToken.None);
                await thickProp.Commit(CancellationToken.None);

                _logger.LogDebug($"Updated sheet metal properties for: {document.Title}");
            }
            */
            // Для обычных деталей, возможно, только масса обновляется в этом методе.
        }

    }
}