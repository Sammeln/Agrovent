using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Handlers;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Services;
using Agrovent.TestMacroFeature;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Specification;
using Agrovent.ViewModels.TaskPane;
using Agrovent.Views.Pages;
using Agrovent.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NPOI.Util;
using SolidWorks.Interop.sldworks;
using Xarial.XCad.Base;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.Features;
using Xarial.XCad.Geometry;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Features.CustomFeature;
using Xarial.XCad.SolidWorks.Geometry;
using Xarial.XCad.UI.Commands;

namespace Agrovent
{
    [ComVisible(true)]
    [Guid("8864d08d-f77a-47b9-858f-4af5eea4fd76")]
    public class AgroventAddin : SwAddInEx
    {
        #region DI Services
        private ILogger<AgroventAddin> _logger;
        private IAGR_ComponentVersionService _versionService;
        private DataContext _dbContext;
        private IAGR_CommandService _commandService;
        private IAGR_ViewModelCacheService _viewModelCache;
        private IAGR_ComponentViewModelFactory _viewModelFactory;


        #endregion

        public override void OnConnect()
        {
            try
            {
                // Инициализация контейнера сервисов
                InitDI();

                // Получение сервисов из контейнера
                _logger = AGR_ServiceContainer.GetService<ILogger<AgroventAddin>>();
                _versionService = AGR_ServiceContainer.GetService<IAGR_ComponentVersionService>();
                _dbContext = AGR_ServiceContainer.GetService<DataContext>();
                _viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
                _viewModelFactory = AGR_ServiceContainer.GetService<IAGR_ComponentViewModelFactory>();

                // Проверка и создание БД (если нужно)
                EnsureDatabaseCreated();

                // Решение проблемы с Behaviour
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Инициализация группы команд в SolidWorks
                CommandManager.AddCommandGroup<AGR_Commands_e>().CommandClick += OnCommandClickExecute;

                //Инициализация сервиса команд
                _commandService = AGR_ServiceContainer.GetService<IAGR_CommandService>();

                // Инициализация модели представления TaskPane
                InitTaskPane();

                InitComponentRegistryTaskPane();


                //
                (Application.Sw as SldWorks).ReferenceNotFoundNotify += AgroventAddin_ReferenceNotFoundNotify;


                Application.Documents.RegisterHandler(
                () => new AGR_DocumentHandler(this, _viewModelCache, _viewModelFactory));

                _logger.LogInformation("AddIn успешно загружен.");
            }
            catch (Exception ex)
            {
                Application.ShowMessageBox($"Ошибка при загрузке AddIn: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                throw;
            }
        }
        public override void OnDisconnect()
        {
            try
            {
                _dbContext?.Dispose();
                _logger?.LogInformation("AddIn успешно отключен.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при отключении AddIn");
            }
        }

        private void EnsureDatabaseCreated()
        {
            try
            {
                _logger.LogInformation("Проверка базы данных...");
                //_dbContext.Database.EnsureDeleted();
                _dbContext.Database.EnsureCreated();
                _logger.LogInformation("База данных готова к работе.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации базы данных");
                Application.ShowMessageBox($"Ошибка подключения к базе данных: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
            }
        }
        private void InitTaskPane()
        {
            try
            {
                var taskPaneVM = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
                var taskPaneView = this.CreateTaskPaneWpf<AGR_TaskPaneView>();
                taskPaneView.Control.DataContext = taskPaneVM;
                taskPaneView.IsActive = true;
                taskPaneView.Control.Focus();

                _logger.LogInformation("TaskPane инициализирован");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации TaskPane");
            }
        }
        private void InitComponentRegistryTaskPane()
        {
            try
            {
                var taskPane = AGR_ServiceContainer.GetService<AGR_ComponentRegistryTaskPaneVM>();
                var taskPaneView = this.CreateTaskPaneWpf<AGR_ComponentRegistryTaskPaneView>();

                taskPane.LoadDataCommand.Execute(null);
                taskPaneView.Control.DataContext = taskPane;
                taskPaneView.IsActive = true;
                taskPaneView.Control.Focus();

                _logger.LogInformation("Component Registry TaskPane инициализирован");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации Component Registry TaskPane");
            }
        }
        private void InitDI()
        {
            AGR_ServiceContainer.Initialize(services =>
            {
                // Регистрация самого AddIn
                services.AddSingleton<AgroventAddin>(this);

                // Дополнительные сервисы, специфичные для SolidWorks
                services.AddSingleton(Application);
            });
        }
        private void OnCommandClickExecute(AGR_Commands_e command)
        {
            try
            {
                switch (command)
                {
                    case AGR_Commands_e.Command1:
                    ShowSpecificationWindow();
                    break;

                    case AGR_Commands_e.ExportToIges:

                    //CopyDocuments();
                    //GetPackAndGO();
                    //ShowAvaArticleInfo();
                    ExportToIGES();
                    break;

                    case AGR_Commands_e.SaveComponent:
                    _commandService.SaveActiveComponentAsync();
                    break;
                    if (Application.Documents.Active is ISwAssembly) SaveActiveAssembly();
                    else SaveActiveComponent();

                    break;

                    case AGR_Commands_e.UpdateProperties:
                    _commandService.UpdatePropertiesAsync();
                    break;
                    //case AGR_Commands_e.ComponentRegistry:
                    //    _commandService.OpenComponentRegistryAsync();
                    //    break;
                    case AGR_Commands_e.ProjectsExplorer:
                    _commandService.OpenProjectExplorerWindowAsync();
                    break;
                    case AGR_Commands_e.MoveComponentWithTriade:
                    Application.Sw.RunCommand(1993, string.Empty);
                    break;

                    case AGR_Commands_e.TestCommand:

                    break;

                    default:
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении команды");
                Application.ShowMessageBox($"Ошибка: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
            }
        }

        ///////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////
        private int AgroventAddin_ReferenceNotFoundNotify(string FileName)
        {

            return 0;
        }

        private void ShowSpecificationWindow()
        {
            if (Application.Documents.Active is ISwAssembly swAssembly)
            {
                var assemblyVM = new AGR_AssemblyComponentVM(swAssembly);
                var specWindow = new AGR_SpecificationWindow();
                var unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();
                specWindow.DataContext = new AGR_SpecificationViewModel(assemblyVM, unitOfWork);
                specWindow.ShowDialog();
            }
            else
            {
                Application.ShowMessageBox("Откройте сборку для просмотра спецификации",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
            }
        }

        private void ShowAvaArticleInfo()
        {
            try
            {
                using var scope = AGR_ServiceContainer.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                var article = dbContext.Set<AvaArticleModel>().FirstOrDefault();

                if (article != null)
                {
                    Application.ShowMessageBox($"Имя:{article.Name}\nАртикул:{article.Article}\nТип:{article.Type}\nКод:{article.PartNumber}\nБренд:{article.Brand}",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
                        Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о статье");
            }
        }

        private async void SaveActiveComponent()
        {
            try
            {
                if (Application.Documents.Active is ISwDocument3D swDoc)
                {
                    _logger.LogInformation($"Сохранение компонента: {swDoc.Title}");

                    IAGR_BaseComponent component = swDoc switch
                    {
                        ISwPart part => new AGR_PartComponentVM(part),
                        ISwAssembly assembly => new AGR_AssemblyComponentVM(assembly),
                        _ => throw new InvalidOperationException("Неподдерживаемый тип документа")
                    };

                    var componentName = component.Name;
                    var saved = await _versionService.CheckAndSaveComponentAsync(component);

                    if (saved)
                    {
                        Application.ShowMessageBox($"Компонент {componentName} успешно сохранен в базу данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                    else
                    {
                        Application.ShowMessageBox($"Компонент {componentName} не изменился или уже существует в базе данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                }
                else
                {
                    Application.ShowMessageBox("Откройте 3D-документ для сохранения",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении компонента");
                Application.ShowMessageBox($"Ошибка сохранения: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
            }
        }

        private async void SaveActiveAssembly()
        {
            try
            {
                if (Application.Documents.Active is ISwAssembly swAssembly)
                {
                    _logger.LogInformation($"Сохранение сборки: {swAssembly.Title}");

                    var assembly = new AGR_AssemblyComponentVM(swAssembly);
                    var assemblyName = assembly.Name;
                    var saved = await _versionService.CheckAndSaveAssemblyAsync(assembly);

                    if (saved)
                    {
                        Application.ShowMessageBox($"Сборка {assemblyName} успешно сохранена в базу данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                    else
                    {
                        Application.ShowMessageBox($"Сборка {assemblyName} не изменилась или уже существует в базе данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                }
                else
                {
                    Application.ShowMessageBox("Откройте сборку для сохранения",
                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении сборки");
                Application.ShowMessageBox($"Ошибка сохранения: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
            }
        }

        private void ExportToIGES()
        {
            var doc = Application.Documents.Active;
            var pn = (doc as ISwDocument3D)
                .Configurations.Active
                .Properties
                .AGR_TryGetProp(AGR_PropertyNames.Partnumber)?.Value.ToString();
            var folder = @"\\192.168.10.56\pdm";
            var docName = Path.GetFileName(doc.Path);

            if (string.IsNullOrEmpty(pn))
            {
                Application.ShowMessageBox(
                    "Обозначение не заполнено",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);

                return;
            }
            var igesDoc = Path.Combine(
                folder,
                pn + "." + Path.ChangeExtension(docName, "IGS"));
            Application.Documents.Active.SaveAs(igesDoc);
        }
    }
}