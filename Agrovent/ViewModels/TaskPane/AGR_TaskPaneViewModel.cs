using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.ViewModels.Components;
using Xarial.XCad.Geometry;
using Agrovent.Infrastructure.Extensions;
using Xarial.XCad.SolidWorks;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using Agrovent.DAL.Services;
using System.Windows.Controls;
using SolidWorks.Interop.sldworks;
using Agrovent.Infrastructure.Enums;
using Microsoft.VisualStudio.Shell.Interop;

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_TaskPaneViewModel : BaseViewModel
    {
        #region FIELDS
        private readonly ISwApplication _app;
        private readonly IAGR_ComponentViewModelFactory _viewModelFactory;
        private readonly ILogger<AGR_TaskPaneViewModel> _logger;
        private readonly IComponentDataService _componentDataService; 
        
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
        #endregion

        public AGR_TaskPaneViewModel(
            IAGR_ComponentViewModelFactory viewModelFactory,
            ILogger<AGR_TaskPaneViewModel> logger,
            IComponentDataService componentDataService)
        {
            _app = AGR_ServiceContainer.GetService<AgroventAddin>().Application;
            _viewModelFactory = viewModelFactory;
            _logger = logger;
            _componentDataService = componentDataService;
            _cancellationTokenSource = new CancellationTokenSource();

            _app.Documents.DocumentActivated += OnDocumentActivatedAsync;
            //_app.Documents.DocumentOpened += Documents_DocumentOpened;
            //_app.Documents.DocumentLoaded += Documents_DocumentLoaded;

            _app.Idle += OnIdle;

            _logger.LogInformation("TaskPaneViewModel initialized");
        }


        #region Subscribe / unsubscribe events for active doc

        private void SubsribeEvents(IXDocument doc)
        {
            doc.Selections.NewSelection += OnSelectionChangedAsync;
            doc.Selections.ClearSelection += OnSelectionClearedAsync;

            if (doc is ISwPart part)
            {
                SubsribePartEvents(doc as ISwPart);
            }


        }
        private void UnsubsribeEvents(IXDocument doc)
        {
            ActiveComponent.Selections.NewSelection -= OnSelectionChangedAsync;
            ActiveComponent.Selections.ClearSelection -= OnSelectionClearedAsync;

            if (doc is ISwPart part)
            {
                UnSubsribePartEvents(doc as ISwPart);
            }
        }

        private void SubsribePartEvents(ISwPart part)
        {
            (part.Part as PartDoc).FeatureManagerTreeRebuildNotify += AGR_TaskPaneViewModel_FeatureManagerTreeRebuildNotify;
            (part.Part as PartDoc).FileSavePostNotify += AGR_TaskPaneViewModel_FileSavePostNotify;

        }
        private void UnSubsribePartEvents(ISwPart? part)
        {
            (part.Part as PartDoc).FeatureManagerTreeRebuildNotify -= AGR_TaskPaneViewModel_FeatureManagerTreeRebuildNotify;
            (part.Part as PartDoc).FileSavePostNotify -= AGR_TaskPaneViewModel_FileSavePostNotify;
        }
        #endregion
        private int AGR_TaskPaneViewModel_FileSavePostNotify(int saveType, string FileName)
        {
            if (saveType != 1)
            {
                var newDoc = _app.Documents.PreCreateFromPath(FileName);
                newDoc.Commit(CancellationToken.None);
                if (newDoc != null)
                {
                    (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = "";
                    (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Article).Value = "";
                    (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value = "";
                }
            }
            return 1;
        }
        private int AGR_TaskPaneViewModel_FeatureManagerTreeRebuildNotify()
        {
            if (ActiveView is AGR_PartComponentVM partVM)
            {
                partVM.Refresh();
                partVM.UpdatePropertiesAsync();
            }
            return 0;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private async Task LoadDocumentViewModel(ISwDocument3D document)
        {
            try
            {
                if (document == null)
                {
                    IsLoading = false;
                    return;
                }

                _logger.LogDebug($"Loading ViewModel for: {document.Title}");


                LoadingMessage = document is ISwAssembly
                    ? "Загрузка сборки..."
                    : "Загрузка детали...";

                // Создаём ViewModel из документа SolidWorks
                var viewModel = _viewModelFactory.CreateComponent(document);


                // Загружаем данные из БД
                LoadingMessage = "Загрузка данных из базы...";
                await LoadComponentDataFromDatabase(viewModel);


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
            }
        }
        private async Task LoadComponentDataFromDatabase(IAGR_BaseComponent component)
        {
            try
            {
                var partNumber = component.PartNumber;

                // Проверяем существование компонента в БД
                var existsInDb = _componentDataService.ComponentExistsInDatabase(partNumber);
                component.IsInDatabase = existsInDb;

                _logger.LogDebug($"Component {partNumber} exists in DB: {existsInDb}");

                if (existsInDb)
                {
                    // Загружаем последнюю версию из БД
                    var latestVersion = _componentDataService.GetLatestComponentVersion(partNumber);

                    if (latestVersion != null)
                    {
                        _logger.LogDebug($"Loaded version {latestVersion.Version} for {partNumber}");

                        // Обновляем свойства из БД
                        component.Version = latestVersion.Version;
                        component.HashSum = latestVersion.HashSum;
                        component.ComponentType = latestVersion.ComponentType;

                        // Загружаем AvaArticle если есть
                        if (latestVersion.AvaArticleArticle.HasValue)
                        {
                            component.AvaArticle = latestVersion.AvaArticle;
                            _logger.LogDebug($"Loaded AvaArticle {latestVersion.AvaArticleArticle} for {partNumber}");
                        }
                    }
                }
                else
                {
                    _logger.LogDebug($"Component {partNumber} not found in database");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading component data from database for {component?.PartNumber}");
            }
        }

        private async void OnDocumentActivatedAsync(IXDocument doc)
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

                // Создаём ViewModel из документа SolidWorks
                var viewModel = _viewModelFactory.CreateComponent(document);
                if (viewModel is AGR_AssemblyComponentVM assemblyComponentVM)
                {
                    assemblyComponentVM.GetChildComponents();
                }
                cancellationToken.ThrowIfCancellationRequested();

                // Загружаем данные из БД
                LoadingMessage = "Загрузка данных из базы...";
                if (!string.IsNullOrEmpty(viewModel.PartNumber))
                {
                    await LoadComponentDataFromDatabaseAsync(viewModel);
                }
                else
                {
                    //await CheckComponentByHashAsync(viewModel);
                }

                cancellationToken.ThrowIfCancellationRequested();

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
            }
        }
        private async Task LoadComponentDataFromDatabaseAsync(IAGR_BaseComponent component)
        {
            try
            {
                var partNumber = component.PartNumber;

                // Проверяем существование компонента в БД
                var existsInDb = await _componentDataService.ComponentExistsInDatabaseAsync(partNumber);
                component.IsInDatabase = existsInDb;

                _logger.LogDebug($"Component {partNumber} exists in DB: {existsInDb}");

                if (existsInDb)
                {
                    // Загружаем последнюю версию из БД
                    var latestVersion = await _componentDataService.GetLatestComponentVersionAsync(partNumber);

                    if (latestVersion != null)
                    {
                        _logger.LogDebug($"Loaded version {latestVersion.Version} for {partNumber}");

                        // Обновляем свойства из БД
                        component.Version = latestVersion.Version;
                        component.HashSum = latestVersion.HashSum;

                        // Загружаем AvaArticle если есть
                        if (latestVersion.AvaArticleArticle.HasValue)
                        {
                            component.AvaArticle = latestVersion.AvaArticle;
                            _logger.LogDebug($"Loaded AvaArticle {latestVersion.AvaArticleArticle} for {partNumber}");
                        }
                    }
                }
                else
                {
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

                var existingComponent = _componentDataService.ComponentExistsInDatabaseAsync(hashSum, name).Result;
                if (existingComponent != null)
                {
                    var res  = _app.ShowMessageBox($"В базе найден такой компонент - {existingComponent.Component.PartNumber}\nНужно или переименовать компонент или использовать сохраненное обозначение\nИспользовать обозначение?",
                                        Xarial.XCad.Base.Enums.MessageBoxIcon_e.Question,
                                        Xarial.XCad.Base.Enums.MessageBoxButtons_e.YesNo);
                    if (res == Xarial.XCad.Base.Enums.MessageBoxResult_e.Yes)
                    {
                        component.PartNumber = existingComponent.Component.PartNumber;
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        private async void OnSelectionChangedAsync(IXDocument doc, Xarial.XCad.IXSelObject selObject)
        {
            try
            {
                if (selObject is IXFace face && face.Component?.ReferencedDocument is ISwDocument3D swDoc)
                {
                    _logger.LogDebug($"Selection changed to: {swDoc.Title}");


                    var viewModel = _viewModelFactory.CreateComponent(swDoc);
                    await LoadComponentDataFromDatabase(viewModel);

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

                    ActiveView = null;
                    BaseComponent = null;
                    ActiveComponent = null;
                    Selection = null;
                }
                //if (ActiveView is AGR_AssemblyComponentVM assemVM)
                //{
                //    assemVM.Refresh();
                //}
                //if (ActiveView is AGR_PartComponentVM partVM)
                //{
                //    partVM.Refresh();
                //    await partVM.UpdatePropertiesAsync();
                //}
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