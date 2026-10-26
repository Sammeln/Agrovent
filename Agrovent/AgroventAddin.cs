using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Handlers;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Properties;
using Agrovent.Services;
using Agrovent.Tests;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.TaskPane;
using Agrovent.Views.Pages;
using Agrovent.Views.Windows;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using Xarial.XCad;
using Xarial.XCad.Base;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.UI;
using Xarial.XCad.UI.Commands;
using Xarial.XCad.UI.Commands.Attributes;
using Xarial.XCad.UI.Commands.Enums;
using Xarial.XCad.UI.Commands.Structures;

namespace Agrovent
{
    [Title("Агровент-М")]
    public enum AGR_Commands_e
    {
        [Title("Информация / сохранить")]
        [Icon(typeof(Resources), nameof(Resources.Information_96))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        SaveComponent,

        [Title("Экспорт в Iges")]
        [Icon(typeof(Resources), nameof(Resources.Iges_96))]
        ExportToIges,

        [Title("Обновить свойства")]
        [Icon(typeof(Resources), nameof(Resources.Update_96))]
        UpdateProperties,

        [Title("Сборка листовых")]
        [Icon(typeof(Resources), nameof(Resources.SheetAssembly_24))]
        GetSheetMetallPartsAssmbly,

        //[Title("Реестр КД")]
        //ComponentRegistry,

        [Title("Проводник проектов")]
        ProjectsExplorer,

        //1993
        [Title("Переместить компонент")]
        [Icon(typeof(Resources), nameof(Resources.Move_96))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly)]
        MoveComponentWithTriade,

        [Title("Сохранить файлы в хранилище")]
        [Icon(typeof(Resources), nameof(Resources.CopyStorage_96))]
        CopyFilesToStorage,

        [Title("Сохранить файлы в производство")]
        [Icon(typeof(Resources), nameof(Resources.CopyProd_96))]
        CopyFilesToProd,

        [Title("Обновить чертежи")]
        [Icon(typeof(Resources), nameof(Resources.UpdateDraw_24))]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        UpdateDrawings,

        [Title("Pack'n'Go")]
        //[Icon(typeof(Resources), nameof(Resources.PackNGo_96))] 
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        PackNGo,

        [Title("Взять на редактирование")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        EditComponent,

        [Title("Отменить редактирование")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part, true, RibbonTabTextDisplay_e.TextBelow)]
        CancelEditing,

        // Временно, удалить после миграции хранилища
        [Title("Маркер и отпечаток")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.Assembly | WorkspaceTypes_e.Part)]
        ShowMarkerInfo,

        [Title("Миграция: отчёт")]
        [CommandItemInfo(true, true, WorkspaceTypes_e.NoDocuments)]
        MigrationReport,
    }

    [ComVisible(true)]
    [Guid("8864d08d-f77a-47b9-858f-4af5eea4fd76")]
    public class AgroventAddin : SwAddInEx
    {
        private IAGR_BaseComponent? GetActiveViewModelSafe()
        {
            try
            {
                if (Application.Documents.Active is not ISwDocument3D doc) return null;
                // Get возвращает default-кортеж, если документа нет в кэше, поэтому .Value.ViewModel тут даёт null,
                // а не исключение. Исключение возможно из doc.Path, пока документ закрывается: ловим его.
                return _viewModelCache.Get(doc)?.ViewModel;
            }
            catch { return null; }
        }

        #region DI Services
        private ILogger<AgroventAddin> _logger;
        private IAGR_ComponentVersionService _versionService;
        private DataContext _dbContext;
        private IAGR_CommandService _commandService;
        private IAGR_ViewModelCacheService _viewModelCache;
        private IAGR_ComponentViewModelFactory _viewModelFactory;
        private IUnitOfWork _unitOfWork;
        public AGR_TaskPaneViewModel MainTaskPane { get; private set; }
        public ISwTaskPane<AGR_TaskPaneView> TaskPaneControl { get; private set; }
        public bool NeedClearId { get; set; } = false;
        public IAGR_BaseComponent? LastComponent { get; set; }
        public string LastDocPath { get; set; }

        #endregion

        public override void OnConnect()
        {
            try
            {
                Microsoft.Xaml.Behaviors.Behavior behavior;

                // Инициализация контейнера сервисов
                InitDI();

                // Получение сервисов из контейнера
                _logger = AGR_ServiceContainer.GetService<ILogger<AgroventAddin>>();
                _versionService = AGR_ServiceContainer.GetService<IAGR_ComponentVersionService>();
                _dbContext = AGR_ServiceContainer.GetService<DataContext>();
                _viewModelCache = AGR_ServiceContainer.GetService<IAGR_ViewModelCacheService>();
                _viewModelFactory = AGR_ServiceContainer.GetService<IAGR_ComponentViewModelFactory>();
                _unitOfWork = AGR_ServiceContainer.GetService<IUnitOfWork>();

                // Проверка и создание БД
                //EnsureDatabaseCreated();

                // Решение проблемы с Behaviour
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Инициализация группы команд в SolidWorks
                var commandGroup = CommandManager.AddCommandGroup<AGR_Commands_e>();
                commandGroup.CommandClick += OnCommandClickExecute;
                commandGroup.CommandStateResolve += OnCommandStateResolve;

                //Инициализация сервиса команд
                _commandService = AGR_ServiceContainer.GetService<IAGR_CommandService>();

                // Инициализация модели представления TaskPane
                InitTaskPane();

                InitComponentRegistryTaskPane();


                //Подписки на события
                var swApp = Application.Sw as SldWorks;
                // Single ReferenceNotFound handler (temporary, MarkerMigrationTest): see AGR_MissingReferenceResolver
                var missingRefs = AGR_ServiceContainer.GetService<Agrovent.Services.Migration.AGR_MissingReferenceResolver>();
                swApp.ReferenceNotFoundNotify += missingRefs.OnReferenceNotFound;
                swApp.CommandCloseNotify += SwApp_CommandCloseNotify;

                Application.Documents.RegisterHandler(
                () => new AGR_DocumentHandler(this, _viewModelCache, _viewModelFactory, _unitOfWork));

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
                MainTaskPane = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();
                TaskPaneControl = this.CreateTaskPaneWpf<AGR_TaskPaneView>();
                TaskPaneControl.Control.DataContext = MainTaskPane;
                TaskPaneControl.IsActive = true;
                TaskPaneControl.Control.Focus();

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

                //taskPane.LoadDataCommand.Execute(null);
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
        private async void OnCommandClickExecute(AGR_Commands_e command)
        {
            try
            {
                switch (command)
                {
                    case AGR_Commands_e.ExportToIges:

                    //CopyDocuments();
                    //GetPackAndGO();
                    //ShowAvaArticleInfo();
                    ExportToIGES();
                    break;

                    case AGR_Commands_e.SaveComponent:
                    var user = AGR_ServiceContainer.GetService<IAGR_User>();

                    _commandService.SaveActiveComponentAsync();
                    break;
                    if (Application.Documents.Active is ISwAssembly) SaveActiveAssembly();
                    else SaveActiveComponent();

                    break;

                    case AGR_Commands_e.UpdateProperties:
                    await _commandService.UpdatePropertiesAsync();
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

                    case AGR_Commands_e.CopyFilesToStorage:
                    await _commandService.CopyFilesToStorageAsync();
                    break;

                    case AGR_Commands_e.CopyFilesToProd:
                    await _commandService.CopyFilesToProdAsync();
                    break;

                    case AGR_Commands_e.UpdateDrawings:
                    await _commandService.UpdateDrawingsAsync();
                    break;

                    case AGR_Commands_e.GetSheetMetallPartsAssmbly:
                    _commandService.GetSheetMetallPartsAssmbly();
                    break;

                    case AGR_Commands_e.PackNGo:
                    await _commandService.PackNGoAsync();
                    break;

                    case AGR_Commands_e.EditComponent:
                    await _commandService.EditActiveComponentAsync(); break;

                    case AGR_Commands_e.CancelEditing:
                    await _commandService.CancelEditingAsync(); break;

                    case AGR_Commands_e.ShowMarkerInfo:
                    await _commandService.ShowMarkerInfoAsync(); break;

                    case AGR_Commands_e.MigrationReport:
                    await _commandService.MigrationReportAsync(); break;
                    //case AGR_Commands_e.Test:
                    //Application.Documents.Active.Features.CreateCustomFeature<BoxMacroFeatureDefinition, BoxMacroFeatureData, BoxPropertyPage>();
                    //var doc = Application.Documents.Active;
                    //if (doc.State == Xarial.XCad.Documents.Enums.DocumentState_e.ReadOnly)
                    //{
                    //    var path = doc.Path;

                    //    if (doc is ISwAssembly assembly)
                    //    {
                    //        var comp = assembly.Configurations.Active.Components.AGR_TryFlatten()
                    //            .Select(comp => comp.ReferencedDocument.Path)
                    //            .Distinct();
                    //        foreach (var compPath in comp)
                    //        {
                    //            File.SetAttributes(compPath, FileAttributes.Normal);
                    //        }

                    //    }

                    //    Application.Sw.CloseAllDocuments(true);
                    //    File.SetAttributes(path, FileAttributes.Normal);
                    //    Application.Documents.Open(path, Xarial.XCad.Documents.Enums.DocumentState_e.Default);
                    //}

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
        private void OnCommandStateResolve(AGR_Commands_e cmd, CommandState state)   // CHECK: сигнатура
        {
            // Вызывается SolidWorks часто и синхронно: только чтение из кэша VM, никаких обращений к БД.
            var vm = GetActiveViewModelSafe();

            switch (cmd)
            {
                case AGR_Commands_e.EditComponent:
                state.Enabled = vm != null
                    && vm.IsInDatabase == AGR_ComponentDatabaseState_e.SavedInDataBase
                    && vm.EditState != AGR_EditState_e.EditingByOther
                    // для детали после взятия повторно не нужна; для сборки остаётся доступной (можно добавить компоненты)
                    && !(vm.ComponentType != AGR_ComponentType_e.Assembly && vm.EditState == AGR_EditState_e.EditingByMe);
                break;

                case AGR_Commands_e.CancelEditing:
                state.Enabled = vm != null && vm.HasMyLocks;
                break;

                case AGR_Commands_e.SaveComponent:
                state.Enabled = vm != null && (vm.HasMyLocks || vm.IsInDatabase == AGR_ComponentDatabaseState_e.NotSavedInDB);
                break;
            }
        }
        private int SwApp_CommandCloseNotify(int Command, int reason)
        {
            //если запустили нашу команду сохранить как с очисткой идентификаторов
            if (Command == 620 && NeedClearId)
            {
                var doc = Application.Documents.Active;
                if (doc.Path != LastDocPath)
                {
                    //удаляем из кэша старый документ
                    _viewModelCache.Remove(LastComponent.SwDocument);

                    var pn = doc.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString();

                    //Получаем модель бокового меню
                    var taskPaneVM = AGR_ServiceContainer.GetService<AGR_TaskPaneViewModel>();

                    //обновляем TaskPane, передаем новый активный документ на отображение.
                    var newdocVM = _viewModelCache.GetOrCreate(
                        doc as ISwDocument3D,
                        d => _viewModelFactory.CreateComponent(d));

                    taskPaneVM.BaseComponent = newdocVM;
                    taskPaneVM.ActiveView = newdocVM;

                    newdocVM.AvaArticle = null;
                    newdocVM.Article = "";
                    newdocVM.PartNumber = string.Empty;
                    newdocVM.HashSum = 0;

                    taskPaneVM.OnDocumentActivatedAsync(doc);
                }
                LastComponent = null;
                NeedClearId = false;
            }
            return 0;
        }

        ///////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////
        private int AgroventAddin_ReferenceNotFoundNotify(string FileName)
        {

            return 0;
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

            Application.ShowMessageBox(
                $"Файл сохранен\n{igesDoc}",
                Xarial.XCad.Base.Enums.MessageBoxIcon_e.Info);
        }

        #region TEST
        private IXApplication _app => Application;

        private static XcadMarker NewTestMarker() => new XcadMarker
        {
            PartNumber = "TEST-0001",
            Version = 1,
            Hash = Guid.NewGuid().ToString("N"),
            ComponentId = 1
        };

        private static void Report(string text)
        {
            TestLog.Write(text);
            MessageBox.Show(text, "Marker test");
        }

        // Обработчик записи: срабатывает, когда SolidWorks разрешает писать в поток
        private void OnStreamWriteAvailable(IXDocument doc)
        {
            doc.StreamWriteAvailable -= OnStreamWriteAvailable;
            bool ok = XcadMarkerStorage.TryWrite(doc, NewTestMarker(), out string err);
            TestLog.Write($"StreamWriteAvailable: {doc.Title}: {(ok ? "запись OK" : "ОШИБКА " + err)}");
        }

        // Обработчик чтения: срабатывает при открытии документа
        private void OnStreamReadAvailable(IXDocument doc)
        {
            bool ok = XcadMarkerStorage.TryRead(doc, out XcadMarker m, out string err);
            TestLog.Write($"StreamReadAvailable: {doc.Title}: " +
                (ok ? $"маркер {m.PartNumber} v{m.Version} hash={m.Hash}" : "НЕ ПРОЧИТАН: " + err));
        }

        #region COMMANDS

        #region WriteMarkerCommand
        private ICommand _WriteMarkerCommand;
        public ICommand WriteMarkerCommand => _WriteMarkerCommand
            ??= new RelayCommand(OnWriteMarkerCommandExecuted, CanWriteMarkerCommandExecute);
        private bool CanWriteMarkerCommandExecute(object p) => true;
        private void OnWriteMarkerCommandExecuted(object p)
        {
            // Подписываемся на событие записи и сохраняем документ
            IXDocument doc = _app.Documents.Active;
            if (doc == null) { Report("Нет активного документа"); return; }
            XcadMarkerStorage.CorruptSignature = false;
            doc.StreamWriteAvailable += OnStreamWriteAvailable;
            try
            {
                doc.Save();
                Report("Save() выполнен. Результат записи см. в логе: " + TestLog.FilePath);
            }
            catch (Exception ex) { Report("Save(): " + ex.Message); }
        }
        #endregion

        #region WriteBrokenMarkerCommand
        private ICommand _WriteBrokenMarkerCommand;
        public ICommand WriteBrokenMarkerCommand => _WriteBrokenMarkerCommand
            ??= new RelayCommand(OnWriteBrokenMarkerCommandExecuted, CanWriteBrokenMarkerCommandExecute);
        private bool CanWriteBrokenMarkerCommandExecute(object p) => true;
        private void OnWriteBrokenMarkerCommandExecuted(object p)
        {
            // Сценарий 8: пишем маркер с испорченной подписью
            IXDocument doc = _app.Documents.Active;
            if (doc == null) { Report("Нет активного документа"); return; }
            XcadMarkerStorage.CorruptSignature = true;
            doc.StreamWriteAvailable += OnStreamWriteAvailable;
            try
            {
                doc.Save();
                Report("Сохранено с испорченной подписью. Теперь запустите ReadMarkerCommand.");
            }
            catch (Exception ex) { Report("Save(): " + ex.Message); }
            finally { XcadMarkerStorage.CorruptSignature = false; }
        }
        #endregion

        #region ReadMarkerCommand
        private ICommand _ReadMarkerCommand;
        public ICommand ReadMarkerCommand => _ReadMarkerCommand
            ??= new RelayCommand(OnReadMarkerCommandExecuted, CanReadMarkerCommandExecute);
        private bool CanReadMarkerCommandExecute(object p) => true;
        private void OnReadMarkerCommandExecuted(object p)
        {
            IXDocument doc = _app.Documents.Active;
            if (doc == null) { Report("Нет активного документа"); return; }
            bool ok = XcadMarkerStorage.TryRead(doc, out XcadMarker m, out string err);
            Report(ok ? $"Маркер найден: {m.PartNumber}, v{m.Version}, hash={m.Hash}, id={m.ComponentId}"
                      : "Маркер не прочитан: " + err);
        }
        #endregion

        #region TrackOpenedDocumentsCommand
        private bool _TrackingEnabled;
        private ICommand _TrackOpenedDocumentsCommand;
        public ICommand TrackOpenedDocumentsCommand => _TrackOpenedDocumentsCommand
            ??= new RelayCommand(OnTrackOpenedDocumentsCommandExecuted, CanTrackOpenedDocumentsCommandExecute);
        private bool CanTrackOpenedDocumentsCommandExecute(object p) => !_TrackingEnabled;
        private void OnTrackOpenedDocumentsCommandExecuted(object p)
        {
            // Сценарии 1, 2: после включения открывайте файлы - результат чтения пишется в лог
            _TrackingEnabled = true;
            _app.Documents.DocumentOpened += doc => doc.StreamReadAvailable += OnStreamReadAvailable; // CHECK: имя события DocumentLoaded
            Report("Отслеживание включено. Открывайте файлы, смотрите лог: " + TestLog.FilePath);
        }
        #endregion

        #region SetFileLevelPartnumberCommand
        private ICommand _SetFileLevelPartnumberCommand;
        public ICommand SetFileLevelPartnumberCommand => _SetFileLevelPartnumberCommand
            ??= new RelayCommand(OnSetFileLevelPartnumberCommandExecuted, CanSetFileLevelPartnumberCommandExecute);
        private bool CanSetFileLevelPartnumberCommandExecute(object p) => true;
        private void OnSetFileLevelPartnumberCommandExecuted(object p)
        {
            // Пишем "Обозначение" на вкладку Custom уровня файла
            IXDocument doc = _app.Documents.Active;
            if (doc == null) { Report("Нет активного документа"); return; }
            try
            {
                IXProperty prp = doc.Properties.GetOrPreCreate(AGR_PropertyNames.Partnumber); // CHECK
                prp.Value = "TEST-PN-0001";
                if (!prp.IsCommitted) prp.Commit();
                doc.Save();
                Report("PartNumber записан на уровень файла. Теперь активируйте конфигурацию flat-pattern и проверьте вкладку свойств.");
            }
            catch (Exception ex) { Report("Ошибка: " + ex.Message); }
        }
        #endregion

        #region ShowPartnumberByConfigurationsCommand
        private ICommand _ShowPartnumberByConfigurationsCommand;
        public ICommand ShowPartnumberByConfigurationsCommand => _ShowPartnumberByConfigurationsCommand
            ??= new RelayCommand(OnShowPartnumberByConfigurationsCommandExecuted, CanShowPartnumberByConfigurationsCommandExecute);
        private bool CanShowPartnumberByConfigurationsCommandExecute(object p) => true;
        private void OnShowPartnumberByConfigurationsCommandExecuted(object p)
        {
            // Показывает, где лежит PartNumber: на уровне файла и в каждой конфигурации
            IXDocument doc = _app.Documents.Active;
            if (!(doc is IXDocument3D doc3D)) { Report("Нужна деталь или сборка"); return; }
            var sb = new StringBuilder();
            string name = AGR_PropertyNames.Partnumber;
            sb.AppendLine("Файл: " + (doc.Properties.Exists(name) ? Convert.ToString(doc.Properties[name].Value) : "(нет)")); // CHECK: Exists
            foreach (IXConfiguration conf in doc3D.Configurations)
            {
                bool has = conf.Properties.Exists(name); // CHECK
                sb.AppendLine($"Конфигурация '{conf.Name}': " + (has ? Convert.ToString(conf.Properties[name].Value) : "(нет)"));
            }
            Report(sb.ToString());
        }
        #endregion

        #endregion 
        #endregion

    }
}