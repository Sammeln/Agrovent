// File: Services/AGR_CommandService.cs
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Agrovent.DAL; // Для IUnitOfWork
using Agrovent.Infrastructure.Extensions; // Для AGR_TryGetProp и т.д.
using Agrovent.Infrastructure.Helpers;
using Agrovent.Infrastructure.Interfaces;
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

        public AGR_CommandService(
            ILogger<AGR_CommandService> logger,
            IAGR_ComponentVersionService componentVersionService,
            IServiceProvider serviceProvider,
            IAGR_ViewModelCacheService viewModelCache,
            IAGR_ComponentViewModelFactory viewModelFactory,
            ISwApplication swApp) // Принимаем IServiceProvider
        {
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
                        progressVM.AddLogMessage($"Компонент {componentName} не изменился или уже существует.");
                        _logger.LogInformation($"Компонент {componentName} не изменился или уже существует.");
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
                    await _componentVersionService.CopyFilesToStorageAsync(component, component.CalculateComponentHash());
                    return true;
                }
                return false;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при вызове CopyFilesToStorageAsync");
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

                    await _componentVersionService.CopyFilesToProdAsync(component, component.CalculateComponentHash());
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