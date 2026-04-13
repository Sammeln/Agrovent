// File: Services/AGR_CommandService.cs
using Agrovent.DAL.Entities.Components; // Если нужно для AvaArticle
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions; // Для AGR_TryGetProp и т.д.
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Windows;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public class AGR_CommandService : IAGR_CommandService
    {
        private readonly ILogger<AGR_CommandService> _logger;
        private readonly IAGR_ComponentVersionService _componentVersionService; // Возможно, нужен для AvaArticle
        private readonly IServiceProvider _serviceProvider; // Необходим для получения VM

        public AGR_CommandService(
            ILogger<AGR_CommandService> logger,
            IAGR_ComponentVersionService componentVersionService,
            IServiceProvider serviceProvider) // Принимаем IServiceProvider
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _componentVersionService = componentVersionService ?? throw new ArgumentNullException(nameof(componentVersionService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<bool> UpdatePropertiesAsync()
        {
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
                _logger.LogDebug("Открытие окна проводника компонентов");

                // Получаем ViewModel из DI контейнера
                var projectTreeVM = AGR_ServiceContainer.GetService<AGR_ProjectExplorerVM>();
                if (projectTreeVM == null)
                {
                    _logger.LogError("Не удалось получить AGR_ComponentRegistryVM из DI контейнера.");
                    return false;
                }
                projectTreeVM.LoadProjectsCommand.Execute(null);

                // Создаем View и устанавливаем DataContext
                var projectTreeView = new AGR_ProjectExplorerView
                {
                    DataContext = projectTreeVM,
                    Title = "Дерево компонентов",
                    Width = 1200,
                    Height = 800,
                    ResizeMode = ResizeMode.CanResizeWithGrip
                };

                // Открываем окно (модально или немодально)
                projectTreeView.ShowDialog(); // или window.Show(); для немодального окна

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна проводника компонентов");
                return false;
            }
        }
        // --- НОВАЯ КОМАНДА: Сохранить активный компонент/сборку ---
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

                IAGR_BaseComponent component = swDoc switch
                {
                    ISwPart part => new AGR_PartComponentVM(part),
                    ISwAssembly assembly => new AGR_AssemblyComponentVM(assembly),
                    _ => throw new InvalidOperationException("Неподдерживаемый тип документа")
                };

                var componentName = component.Name;
                var componentType = activeDoc is ISwAssembly ? "Сборка" : "Деталь";

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
                // progressDialog.ShowDialog(); // Блокирует UI до закрытия окна, что может быть неудобно, если сохранение быстро.

                bool saved = false;
                try
                {
                    // --- ВЫПОЛНЯЕМ СОХРАНЕНИЕ В ТОМ ЖЕ ПОТОКЕ ---
                    if (activeDoc is ISwAssembly)
                    {
                        var assembly = activeDoc as ISwAssembly;
                        var docsReadOnly = assembly.Configurations.Active.Components.TryFlatten()
                            .Where(x => (File.GetAttributes(x.ReferencedDocument.Path).HasFlag(FileAttributes.ReadOnly)) == true)
                            .Select(x => Path.GetFileName(x.ReferencedDocument.Path)).Distinct()
                            .ToList();

                        if (docsReadOnly.Count != 0)
                        {
                            progressVM.AddLogMessage("В сборке есть файлы 'Только для чтения':");
                            foreach (var item in docsReadOnly)
                            {
                                progressVM.AddLogMessage(item);
                            }
                            progressVM.AddLogMessage($"Сохранение отменено: {componentName}");
                            return false;
                        }

                        // Сохраняем как сборку
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
                    // Можно добавить stack trace в лог, если нужно
                    // progressVM.AddLogMessage($"Stack Trace: {ex.StackTrace}");
                }
                finally
                {
                    // Устанавливаем флаг завершения в любом случае (успешно или с ошибкой)
                    // Это может быть вызван из UI-потока, так как метод выполняется в нём
                    progressVM.SetFinished();
                }

                // progressDialog.Close(); // Опционально: закрыть окно после завершения
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