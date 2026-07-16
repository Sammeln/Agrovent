using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.ViewModels.Components;
using Xarial.XCad.Geometry;
using Xarial.XCad.SolidWorks;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using Agrovent.DAL.Services;
using SolidWorks.Interop.sldworks;
using Agrovent.Infrastructure.Enums;
using Agrovent.DAL;
using System.Diagnostics;
using Agrovent.DAL.Services.Repositories;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_TaskPaneViewModel : BaseViewModel
    {
        #region FIELDS
        private readonly ISwApplication _app;
        private readonly IAGR_ComponentViewModelFactory _viewModelFactory;
        private readonly ILogger<AGR_TaskPaneViewModel> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAGR_ViewModelCacheService _viewModelCache;

        private bool Initialized = false;
        private CancellationTokenSource _cancellationTokenSource;
        #endregion

        #region PROPS
        private ISwDocument3D _ActiveComponent;
        public ISwDocument3D ActiveComponent
        {
            get => _ActiveComponent;
            set => Set(ref _ActiveComponent, value);
        }

        private IAGR_PageView _ActiveView;
        public IAGR_PageView ActiveView
        {
            get => _ActiveView;
            set => Set(ref _ActiveView, value);
        }

        private IAGR_PageView _BaseComponent;
        public IAGR_PageView BaseComponent
        {
            get => _BaseComponent;
            set => Set(ref _BaseComponent, value);
        }

        private IAGR_PageView _Selection;
        public IAGR_PageView Selection
        {
            get => _Selection;
            set => Set(ref _Selection, value);
        }

        private bool _IsLoading;
        public bool IsLoading
        {
            get => _IsLoading;
            set => Set(ref _IsLoading, value);
        }

        private string _LoadingMessage;
        public string LoadingMessage
        {
            get => _LoadingMessage;
            set => Set(ref _LoadingMessage, value);
        }

        private int _LoadingProgress;
        public int LoadingProgress
        {
            get => _LoadingProgress;
            set => Set(ref _LoadingProgress, value);
        }

        private int _TotalComponents;
        public int TotalComponents
        {
            get => _TotalComponents;
            set => Set(ref _TotalComponents, value);
        }
        #endregion

        #region CTOR
        public AGR_TaskPaneViewModel(
            IAGR_ComponentViewModelFactory viewModelFactory,
            ILogger<AGR_TaskPaneViewModel> logger,
            IComponentDataService componentDataService,
            IAGR_ComponentRepository componentRepo,
            IUnitOfWork unitOfWork,
            IAGR_ViewModelCacheService cacheService)
        {
            _app = AGR_ServiceContainer.GetService<AgroventAddin>().Application;
            _viewModelFactory = viewModelFactory;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _viewModelCache = cacheService;
            _cancellationTokenSource = new CancellationTokenSource();
            if (!Initialized)
            {
                _app.Documents.DocumentActivated += OnDocumentActivatedAsync;
                _app.Idle += OnIdle;
                Initialized = true;
                (_app.Sw as SldWorks).CommandOpenPreNotify += AGR_TaskPaneViewModel_CommandOpenPreNotify;
            }

            _logger.LogInformation("TaskPaneViewModel initialized");
        }

        public AGR_TaskPaneViewModel() { }
        #endregion

        private int AGR_TaskPaneViewModel_CommandOpenPreNotify(int Command, int UserCommand)
        {
            Debug.Print("Command: " + Command + ", UserCommand: " + UserCommand);
            return 0;
        }

        #region Subscribe / unsubscribe events for active doc
        private void SubsribeEvents(IXDocument doc)
        {
            doc.Selections.NewSelection += OnSelectionChangedAsync;
            doc.Selections.ClearSelection += OnSelectionClearedAsync;

            var viewModel = _viewModelCache.GetOrCreate(doc as ISwDocument3D, d => _viewModelFactory.CreateComponent(d));
            if (viewModel != null)
            {
                viewModel.PartnumberChanged += OnPartnumberChangedAsync;
            }
        }

        private async void OnPartnumberChangedAsync(object? sender, EventArgs e)
        {
            if (sender is IAGR_BaseComponent component)
            {
                try
                {
                    _logger.LogInformation($"PartNumber changed for {component.Name}. New PartNumber: {component.PartNumber}");

                    // Сбрасываем статус, чтобы LoadComponentDataFromDatabaseAsync заново проверил БД
                    component.IsInDatabase = AGR_ComponentDatabaseState_e.NotLoaded;

                    if (!string.IsNullOrEmpty(component.PartNumber))
                    {
                        // Запускаем проверку нового партнамбера в базе
                        await LoadComponentDataFromDatabaseAsync(component);
                    }
                    else
                    {
                        // Если партнамбер очистился, просто помечаем как несохраненный
                        component.IsInDatabase = AGR_ComponentDatabaseState_e.NotSavedInDB;
                    }

                    // Обновляем ActiveView, если это текущий активный компонент, 
                    // чтобы UI подхватил изменения свойств (IsInDatabase, Version, пути к файлам и т.д.)
                    if (ActiveComponent == component.SwDocument)
                    {
                        ActiveView = component;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling PartnumberChanged for {component.PartNumber}");
                }
            }
        }

        private void UnsubsribeEvents(IXDocument doc)
        {
            if (ActiveComponent != null)
            {
                ActiveComponent.Selections.NewSelection -= OnSelectionChangedAsync;
                ActiveComponent.Selections.ClearSelection -= OnSelectionClearedAsync;
            }
            var viewModel = _viewModelCache.GetOrCreate(doc as ISwDocument3D, d => _viewModelFactory.CreateComponent(d));
            if (viewModel != null)
            {
                viewModel.PartnumberChanged -= OnPartnumberChangedAsync;
            }
        }
        #endregion

        public async void OnDocumentActivatedAsync(IXDocument doc)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                _logger.LogInformation($"Document activated: {doc?.Title}");

                if (doc == null)
                {
                    ActiveView = null;
                    return;
                }

                IsLoading = true;
                LoadingProgress = 0;
                TotalComponents = 0;
                LoadingMessage = "Загрузка документа...";

                if (ActiveComponent != null)
                {
                    UnsubsribeEvents(doc);
                }

                SubsribeEvents(doc);

                if (doc is ISwDocument3D swDoc)
                {
                    ActiveComponent = swDoc;
                    await LoadDocumentViewModelAsync(swDoc, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Document loading cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating document: {doc?.Title}");
                IsLoading = false;
            }
        }

        private async Task LoadDocumentViewModelAsync(ISwDocument3D document, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (document == null)
                {
                    IsLoading = false;
                    return;
                }

                _logger.LogDebug($"Loading ViewModel for: {document.Title}");

                LoadingMessage = document is ISwAssembly
                    ? "Загрузка сборки..."
                    : "Загрузка детали...";
                LoadingProgress = 10;

                IAGR_BaseComponent viewModel = default;

                // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Выносим создание ViewModel в фоновый поток
                LoadingMessage = "Создание структуры компонента...";
                //viewModel = await Task.Run(() =>
                //{
                //    return _viewModelCache.GetOrCreate(document, d => _viewModelFactory.CreateComponent(d));
                //}, cancellationToken);

                viewModel = _viewModelCache.GetOrCreate(document, d => _viewModelFactory.CreateComponent(d));
                cancellationToken.ThrowIfCancellationRequested();

                // 🔥 КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Если это сборка, выносим обход компонентов в фон
                if (viewModel is AGR_AssemblyComponentVM assemblyComponentVM)
                {
                    LoadingMessage = "Загрузка компонентов сборки...";
                    LoadingProgress = 30;

                    //await Task.Run(() =>
                    //{
                    //    assemblyComponentVM.GetChildComponents();
                    //}, cancellationToken);
                    //assemblyComponentVM.GetChildComponents();
                    assemblyComponentVM.GetSimpleTopComponentsList();
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Загружаем данные из БД
                LoadingMessage = "Загрузка данных из базы...";
                LoadingProgress = 60;

                if (viewModel.IsInDatabase == AGR_ComponentDatabaseState_e.NotLoaded)
                {
                    if (!string.IsNullOrEmpty(viewModel.PartNumber)
                                && !string.IsNullOrEmpty(viewModel.SwDocument.Path))
                    {
                        await LoadComponentDataFromDatabaseAsync(viewModel);
                    }
                    else
                    {
                        await CheckComponentByHashAsync(viewModel);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                LoadingProgress = 100;
                BaseComponent = viewModel;
                ActiveView = viewModel;

                _logger.LogInformation($"ViewModel loaded for: {document.Title}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"Loading cancelled for: {document?.Title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading ViewModel for: {document?.Title}");
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
                LoadingProgress = 0;
            }
        }

        public async Task LoadComponentDataFromDatabaseAsync(IAGR_BaseComponent component)
        {
            try
            {
                var partNumber = component.PartNumber;

                var latestVersion = await _unitOfWork.ComponentRepository.GetLatestComponentVersion(partNumber);

                var existsInDb = latestVersion != null;

                _logger.LogDebug($"Component {partNumber} exists in DB: {existsInDb}");

                if (existsInDb && latestVersion != null)
                {
                    component.IsInDatabase = AGR_ComponentDatabaseState_e.SavedInDataBase;
                    _logger.LogDebug($"Loaded version {latestVersion.Version} for {partNumber}");

                    component.ComponentVersion = latestVersion;
                    component.Version = latestVersion.Version;
                    component.HashSum = latestVersion.HashSum;
                    component.AvaArticle = latestVersion.AvaArticle;
                    component.AvaType = latestVersion.AvaType;

                    var fileComponent = component as AGR_FileComponent;
                    fileComponent.StorageModelFilePath = latestVersion.Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.StorageModel)?.FilePath ?? "";
                    fileComponent.StorageDrawFilePath = latestVersion.Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.StorageDrawing)?.FilePath ?? "";
                    fileComponent.ProductionModelFilePath = latestVersion.Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.ProductionModel)?.FilePath ?? "";
                    fileComponent.ProductionDrawFilePath = latestVersion.Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.ProductionDrawing)?.FilePath ?? "";
                    var parentAssemblies = await _unitOfWork.ComponentRepository.GetRootAssembliesForChildAsync(latestVersion);

                    foreach (var prop in latestVersion.Properties)
                    {
                        var compProp = component.PropertiesCollection.Properties.FirstOrDefault(p => p.Name == prop.Name);
                        if (compProp != null)
                        {
                            compProp.Value = prop.Value;
                        }
                    }


                    component.ParentAssemblies.Clear();
                    foreach (var item in parentAssemblies)
                    {
                        AGR_ComponentRegistryItemVM componentSpec = new AGR_ComponentRegistryItemVM(item);
                        component.ParentAssemblies.Add(componentSpec);
                    }
                }
                else
                {
                    component.IsInDatabase = AGR_ComponentDatabaseState_e.NotSavedInDB;
                    _logger.LogDebug($"Component {partNumber} not found in database");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading component data from database for {component?.PartNumber}");
            }
        }

        private async Task CheckComponentByHashAsync(IAGR_BaseComponent component)
        {
            try
            {
                int hashSum = component.CalculateComponentHash();
                var name = component.Name;

                if (component.SwDocument is ISwAssembly assembly)
                {
                    if (assembly.Configurations.Active.Components.Count == 0) return;
                }
                if (component.SwDocument is ISwPart part)
                {
                    if (part.Features.Count == 21) return;
                }

                var existingComponent = await _unitOfWork.ComponentRepository.FindComponentByHash(hashSum);

                if (existingComponent != null)
                {
                    if (existingComponent.Name == component.Name)
                    {
                        var res = _app.ShowMessageBox(
                            $"В базе найден такой компонент - {existingComponent.Component.PartNumber}\nНужно или переименовать компонент или использовать сохраненное обозначение\nИспользовать обозначение?",
                            Xarial.XCad.Base.Enums.MessageBoxIcon_e.Question,
                            Xarial.XCad.Base.Enums.MessageBoxButtons_e.YesNo);

                        if (res == Xarial.XCad.Base.Enums.MessageBoxResult_e.Yes)
                        {
                            component.PartNumber = existingComponent.Component.PartNumber;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking component by hash for {component?.Name}");
            }
        }

        private async void OnSelectionChangedAsync(IXDocument doc, Xarial.XCad.IXSelObject selObject)
        {
            try
            {
                if (selObject is IXFace face && face.Component?.ReferencedDocument is ISwDocument3D swDoc)
                {
                    _logger.LogDebug($"Selection changed to: {swDoc.Title}");

                    //var viewModel = await Task.Run(() =>
                    //    _viewModelCache.GetOrCreate(swDoc, d => _viewModelFactory.CreateComponent(d)));
                    var viewModel = _viewModelCache.GetOrCreate(swDoc, d => _viewModelFactory.CreateComponent(d));

                    if (viewModel.IsInDatabase == AGR_ComponentDatabaseState_e.NotLoaded)
                    {
                        await LoadComponentDataFromDatabaseAsync(viewModel);
                    }

                    Selection = viewModel;
                    ActiveView = viewModel;
                }
                else if (selObject is IXComponent component)
                {
                    swDoc = component.ReferencedDocument as ISwDocument3D;
                    _logger.LogDebug($"Selection changed to: {swDoc.Title}");

                    //var viewModel = await Task.Run(() =>
                    //    _viewModelCache.GetOrCreate(swDoc, d => _viewModelFactory.CreateComponent(d)));

                    var viewModel = _viewModelCache.GetOrCreate(swDoc, d => _viewModelFactory.CreateComponent(d));

                    await LoadComponentDataFromDatabaseAsync(viewModel);

                    if (viewModel is AGR_AssemblyComponentVM assemblyComponentVM)
                    {
                        assemblyComponentVM.GetSimpleTopComponentsList();
                        // 🔥 Выносим обход в фон
                        //await Task.Run(() => assemblyComponentVM.GetChildComponents());
                    }

                    Selection = viewModel;
                    ActiveView = viewModel;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling selection change");
            }
        }

        private void OnSelectionClearedAsync(IXDocument doc)
        {
            try
            {
                _logger.LogDebug("Selection cleared");

                if (doc is ISwDocument3D swDoc)
                {
                    ActiveComponent = swDoc;
                    ActiveView = BaseComponent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling selection clear");
            }
        }

        private async void OnIdle(Xarial.XCad.IXApplication app)
        {
            try
            {
                if (_app.Documents.Count == 0 && ActiveComponent != null)
                {
                    _logger.LogDebug("All documents closed, cleaning up");
                    _viewModelCache.Clear();
                    ActiveView = null;
                    BaseComponent = null;
                    ActiveComponent = null;
                    Selection = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in idle handler");
            }
        }

        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();

                if (_app != null)
                {
                    _app.Documents.DocumentActivated -= OnDocumentActivatedAsync;
                    _app.Idle -= OnIdle;
                }

                _logger.LogInformation("TaskPaneViewModel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing TaskPaneViewModel");
            }
        }
    }
}