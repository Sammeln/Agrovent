using System.ComponentModel.Design;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Specification;
using Agrovent.ViewModels.TaskPane;
using Agrovent.Views.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
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

                _logger.LogInformation("AddIn успешно загружен.");
            }
            catch (Exception ex)
            {
                Application.ShowMessageBox($"Ошибка при загрузке AddIn: {ex.Message}",
                    Xarial.XCad.Base.Enums.MessageBoxIcon_e.Error);
                throw;
            }
        }

        private void EnsureDatabaseCreated()
        {
            try
            {
                _logger.LogInformation("Проверка базы данных...");
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

        private void OnCommandClickExecute(AGR_Commands_e command)
        {
            AGR_BaseComponent activeComponent = Application.Documents.Active.AGR_BaseComponent() as AGR_BaseComponent;
            try
            {
                switch (command)
                {
                    case AGR_Commands_e.Command1:
                        ShowSpecificationWindow();
                        break;

                    case AGR_Commands_e.Command2:
                        ShowAvaArticleInfo();
                        break;

                    case AGR_Commands_e.SaveComponent:
                        SaveActiveComponent();
                        break;
                    case AGR_Commands_e.UpdateProperties:
                        //_commandService.UpdatePropertiesAsync(activeComponent);
                        Test();
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

        private void Test()
        {
            var activeDoc = Application.Documents.Active as ISwDocument3D;
            string filePath = activeDoc.Path;
            string activeConfig = activeDoc.Configurations.Active.Name;


            object com = Application.Sw.GetPreviewBitmap(filePath, activeConfig);
            stdole.StdPicture pic = com as stdole.StdPicture;
            Bitmap bmp = Bitmap.FromHbitmap((IntPtr)pic.Handle);
            bmp.Save(@"D:\Part1_1.bmp");
        }

        private void ShowSpecificationWindow()
        {
            if (Application.Documents.Active is ISwAssembly swAssembly)
            {
                var assemblyVM = new AGR_AssemblyComponentVM(swAssembly);
                var specWindow = new AGR_SpecificationWindow();
                specWindow.DataContext = new AGR_SpecificationViewModel(assemblyVM);
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

                    var saved = await _versionService.CheckAndSaveComponentAsync(component);

                    if (saved)
                    {
                        Application.ShowMessageBox($"Компонент {component.Name} успешно сохранен в базу данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                    else
                    {
                        Application.ShowMessageBox($"Компонент {component.Name} не изменился или уже существует в базе данных",
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
                    var saved = await _versionService.CheckAndSaveAssemblyAsync(assembly);

                    if (saved)
                    {
                        Application.ShowMessageBox($"Сборка {assembly.Name} успешно сохранена в базу данных",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
                    }
                    else
                    {
                        Application.ShowMessageBox($"Сборка {assembly.Name} не изменилась или уже существует в базе данных",
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

        private void InitDI()
        {
            AGR_ServiceContainer.Initialize(services =>
            {
                // Регистрация самого AddIn
                services.AddSingleton<AgroventAddin>(this);

                // Дополнительные сервисы, специфичные для SolidWorks
                services.AddSingleton(Application);
                services.AddSingleton(CommandManager);

                // ViewModels
                services.AddTransient<AGR_TaskPaneViewModel>();
            });
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
    }
}

#region MyRegion

//using System.ComponentModel.Design;
//using System.Runtime.InteropServices;
//using System.Text;
//using Agrovent.DAL;
//using Agrovent.Services;
//using Agrovent.ViewModels.Components;
//using Agrovent.ViewModels.Specification;
//using Agrovent.ViewModels.TaskPane;
//using Agrovent.Views.Windows;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Xarial.XCad.SolidWorks;
//using Xarial.XCad.SolidWorks.Documents;
//using Xarial.XCad.UI.Commands;

//namespace Agrovent
//{
//    [ComVisible(true)]
//    [Guid("8864d08d-f77a-47b9-858f-4af5eea4fd76")]
//    public class AgroventAddin : SwAddInEx
//    {
//        #region DI Services
//        private ILogger<AgroventAddin> _logger;
//        #endregion

//        public override void OnConnect()
//        {
//            //Инициализация контейнера сервисов
//            InitDI();

//            //Получение сервисов из контейнера
//            _logger = AGR_ServiceContainer.GetService<ILogger<AgroventAddin>>();

//            //решение проблемы с Behaviour
//            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

//            //Инициализация группы команд в солиде
//            CommandManager.AddCommandGroup<AGR_Commands_e>().CommandClick += OnCommandClickExecute;

//            //инициализация модели представления TaskPane
//            InitTaskPane();

//            _logger.LogDebug("AddIn успешно добавлено.");

//        }



//        public override void OnDisconnect()
//        {
//            // Cleanup code here
//        }

//        private void OnCommandClickExecute(AGR_Commands_e command)
//        {
//            switch (command)
//            {
//                case AGR_Commands_e.Command1:

//                    if (Application.Documents.Active is ISwAssembly swAssembly)
//                    {
//                        AGR_SpecificationWindow specWindow = new AGR_SpecificationWindow();
//                        specWindow.DataContext = new AGR_SpecificationViewModel(new AGR_AssemblyComponentVM(swAssembly));
//                        specWindow.ShowDialog();
//                    }

//                    break;
//                case AGR_Commands_e.Command2:
//                    // Handle Command2
//                    DataContext dataContext = new DataContext();
//                    var a = dataContext.AvaArticles.First();
//                    Application.ShowMessageBox($"Имя:{a.Name}\nАртикул:{a.Article}\nТип:{a.Type}\nКод:{a.PartNumber}\nБренд:{a.Brand}",
//                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info,
//                        Xarial.XCad.Base.Enums.MessageBoxButtons_e.Ok);
//                    break;

//                default:
//                    break;
//            }
//        }
//        private void InitDI()
//        {
//            AGR_ServiceContainer.Initialize(services =>
//            {
//                // Дополнительная регистрация специфичная для нашего Add-in
//                services.AddSingleton<AgroventAddin>(this);

//                // Можно добавить конфигурацию из файла
//                // services.AddSingleton<IConfiguration>(LoadConfiguration());
//            });
//        }
//        private void InitTaskPane()
//        {
//            var _taskPaneVM = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
//            var taskPaneView = this.CreateTaskPaneWpf<AGR_TaskPaneView>();
//            //taskPaneView.Control.DataContext = _taskPaneVM;
//            taskPaneView.IsActive = true;
//            taskPaneView.Control.Focus();
//        }
//    }


//    public enum AGR_Commands_e
//    {
//        Command1,
//        Command2
//    }

//}

#endregion