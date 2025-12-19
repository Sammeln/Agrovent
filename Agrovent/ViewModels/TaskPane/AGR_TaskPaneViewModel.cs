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

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_TaskPaneViewModel : BaseViewModel
    {
        private readonly ISwApplication _app;
        private readonly IAGR_ComponentViewModelFactory _viewModelFactory;
        private readonly ILogger<AGR_TaskPaneViewModel> _logger;
        private readonly IComponentDataService _componentDataService;

        private CancellationTokenSource _cancellationTokenSource;

        #region Properties
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
            _app.Idle += OnIdle;

            _logger.LogInformation("TaskPaneViewModel initialized");
        }

        private void OnDocumentActivated (IXDocument doc)
        {

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
                    //ActiveComponent.Selections.NewSelection -= OnSelectionChanged;
                    //ActiveComponent.Selections.ClearSelection -= OnSelectionCleared;
                }


                //doc.Selections.NewSelection += OnSelectionChanged;
                //doc.Selections.ClearSelection += OnSelectionCleared;


                if (doc is ISwDocument3D swDoc)
                {
                    ActiveComponent = swDoc;

                    LoadDocumentViewModel(swDoc);
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


        ////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////

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
                    ActiveComponent.Selections.NewSelection -= OnSelectionChangedAsync;
                    ActiveComponent.Selections.ClearSelection -= OnSelectionClearedAsync;
                }


                doc.Selections.NewSelection += OnSelectionChangedAsync;
                doc.Selections.ClearSelection += OnSelectionClearedAsync;


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
                await LoadComponentDataFromDatabaseAsync(viewModel);

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

        private void OnIdle(Xarial.XCad.IXApplication app)
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



#region MyRegion

//using Agrovent.Services;
//using Agrovent.ViewModels.Base;
//using Xarial.XCad.Documents;
//using Xarial.XCad.SolidWorks.Documents;
//using Agrovent.ViewModels.Components;
//using Xarial.XCad.Geometry;
//using Agrovent.Infrastructure.Extensions;
//using Xarial.XCad.SolidWorks;
//using Agrovent.Infrastructure.Interfaces.Components.Base;
//using Microsoft.Extensions.Logging;
//using Agrovent.DAL.Services;
//using System.Threading;

//namespace Agrovent.ViewModels.TaskPane
//{
//    public class AGR_TaskPaneViewModel : BaseViewModel
//    {
//        private readonly ISwApplication _app;
//        private readonly IAGR_ComponentViewModelFactory _viewModelFactory;
//        private readonly ILogger<AGR_TaskPaneViewModel> _logger;
//        private readonly IComponentDataService _componentDataService;

//        private CancellationTokenSource _cancellationTokenSource;

//        #region Properties
//        private ISwDocument3D _ActiveComponent;
//        public ISwDocument3D ActiveComponent
//        {
//            get => _ActiveComponent;
//            set => Set(ref _ActiveComponent, value);
//        }

//        private IAGR_PageView _ActiveView;
//        public IAGR_PageView ActiveView
//        {
//            get => _ActiveView;
//            set => Set(ref _ActiveView, value);
//        }

//        private IAGR_PageView _BaseComponent;
//        public IAGR_PageView BaseComponent
//        {
//            get => _BaseComponent;
//            set => Set(ref _BaseComponent, value);
//        }

//        private IAGR_PageView _Selection;
//        public IAGR_PageView Selection
//        {
//            get => _Selection;
//            set => Set(ref _Selection, value);
//        }

//        private bool _IsLoading;
//        public bool IsLoading
//        {
//            get => _IsLoading;
//            set => Set(ref _IsLoading, value);
//        }

//        private string _LoadingMessage;
//        public string LoadingMessage
//        {
//            get => _LoadingMessage;
//            set => Set(ref _LoadingMessage, value);
//        }
//        #endregion

//        public AGR_TaskPaneViewModel(
//            IAGR_ComponentViewModelFactory viewModelFactory,
//            ILogger<AGR_TaskPaneViewModel> logger,
//            IComponentDataService componentDataService)
//        {
//            _app = AGR_ServiceContainer.GetService<AgroventAddin>().Application;
//            _viewModelFactory = viewModelFactory;
//            _logger = logger;
//            _componentDataService = componentDataService;

//            _cancellationTokenSource = new CancellationTokenSource();

//            _app.Documents.DocumentActivated += OnDocumentActivated;
//            _app.Idle += OnIdle;

//            _logger.LogInformation("TaskPaneViewModel initialized");
//        }

//        private void OnDocumentActivated(IXDocument doc)
//        {
//            try
//            {
//                _logger.LogInformation($"Document activated: {doc?.Title}");

//                if (doc == null)
//                {
//                    ActiveView = null;
//                    return;
//                }

//                // Показываем индикатор загрузки
//                IsLoading = true;
//                LoadingMessage = "Загрузка документа...";

//                // Отписываемся от старых событий выделения
//                if (ActiveComponent != null)
//                {
//                    ActiveComponent.Selections.NewSelection -= OnSelectionChangedAsync;
//                    ActiveComponent.Selections.ClearSelection -= OnSelectionClearedAsync;
//                }

//                // Подписываемся на события выделения нового документа
//                doc.Selections.NewSelection += OnSelectionChangedAsync;
//                doc.Selections.ClearSelection += OnSelectionClearedAsync;

//                // Асинхронно загружаем ViewModel для документа
//                if (doc is ISwDocument3D swDoc)
//                {
//                    ActiveComponent = swDoc;

//                    // Начинаем асинхронную загрузку
//                    LoadViewModel(swDoc);
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogDebug("Document loading cancelled");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error activating document: {doc?.Title}");
//                IsLoading = false;
//            }
//        }
//        private void LoadViewModel(ISwDocument3D document)
//        {
//            try
//            {
//                if (document == null)
//                {
//                    IsLoading = false;
//                    return;
//                }

//                _logger.LogDebug($"Loading ViewModel for: {document.Title}");

//                // В зависимости от типа документа меняем сообщение
//                LoadingMessage = document is ISwAssembly
//                    ? "Загрузка сборки..."
//                    : "Загрузка детали...";

//                // Асинхронно создаем ViewModel
//                var viewModel = _viewModelFactory.CreateComponent(document);

//                // Загружаем MatchModel и проверяем существование в БД
//                var partName = viewModel.Name;
//                var configName = viewModel.ConfigName;
//                var partNumber = viewModel.PartNumber;

//                // Проверяем существование компонента в БД
//                var existsInDb = _componentDataService.ComponentExistsInDatabaseAsync(partNumber).Result;
//                viewModel.IsInDatabase = existsInDb;

//                _logger.LogDebug($"Component {partNumber} exists in DB: {existsInDb}");

//                if (existsInDb)
//                {
//                    // Загружаем последнюю версию из БД
//                    var latestVersion = _componentDataService.GetLatestComponentVersionAsync(partNumber).Result;

//                    if (latestVersion != null)
//                    {
//                        _logger.LogDebug($"Loaded version {latestVersion.Version} for {partNumber}");

//                        // Обновляем свойства из БД
//                        viewModel.Version = latestVersion.Version;
//                        viewModel.HashSum = latestVersion.HashSum;

//                        // Загружаем AvaArticle если есть
//                        if (latestVersion.AvaArticleArticle.HasValue)
//                        {
//                            viewModel.AvaArticle = latestVersion.AvaArticle;
//                            _logger.LogDebug($"Loaded AvaArticle {latestVersion.AvaArticleArticle} for {partNumber}");
//                        }
//                    }
//                }
//                else
//                {
//                    _logger.LogDebug($"Component {partNumber} not found in database");
//                }


//                // Обновляем UI
//                BaseComponent = viewModel;
//                ActiveView = viewModel;

//                _logger.LogInformation($"ViewModel loaded for: {document.Title}");
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogDebug($"Loading cancelled for: {document?.Title}");
//                // Не обновляем UI если загрузка отменена
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error loading ViewModel for: {document?.Title}");
//                // Можно показать ошибку пользователю
//            }
//            finally
//            {
//                IsLoading = false;
//                LoadingMessage = string.Empty;
//            }
//        }
//        private async void OnDocumentActivatedAsync(IXDocument doc)
//        {

//            _cancellationTokenSource?.Cancel();
//            _cancellationTokenSource = new CancellationTokenSource();
//            var cancellationToken = _cancellationTokenSource.Token;

//            try
//            {
//                _logger.LogInformation($"Document activated: {doc?.Title}");

//                if (doc == null)
//                {
//                    ActiveView = null;
//                    return;
//                }


//                IsLoading = true;
//                LoadingMessage = "Загрузка документа...";


//                if (ActiveComponent != null)
//                {
//                    ActiveComponent.Selections.NewSelection -= OnSelectionChangedAsync;
//                    ActiveComponent.Selections.ClearSelection -= OnSelectionClearedAsync;
//                }


//                doc.Selections.NewSelection += OnSelectionChangedAsync;
//                doc.Selections.ClearSelection += OnSelectionClearedAsync;


//                if (doc is ISwDocument3D swDoc)
//                {
//                    ActiveComponent = swDoc;


//                    await LoadDocumentViewModelAsync(swDoc, cancellationToken);
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogDebug("Document loading cancelled");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error activating document: {doc?.Title}");
//                IsLoading = false;
//            }
//        }
//        private async Task LoadDocumentViewModelAsync(ISwDocument3D document, CancellationToken cancellationToken)
//        {
//            try
//            {
//                cancellationToken.ThrowIfCancellationRequested();

//                if (document == null)
//                {
//                    IsLoading = false;
//                    return;
//                }

//                _logger.LogDebug($"Loading ViewModel for: {document.Title}");


//                LoadingMessage = document is ISwAssembly
//                    ? "Загрузка сборки..."
//                    : "Загрузка детали...";

//                // Создаём ViewModel из документа SolidWorks
//                var viewModel = _viewModelFactory.CreateComponent(document);

//                cancellationToken.ThrowIfCancellationRequested();

//                // Загружаем данные из БД
//                LoadingMessage = "Загрузка данных из базы...";
//                await LoadComponentDataFromDatabase(viewModel);

//                cancellationToken.ThrowIfCancellationRequested();

//                BaseComponent = viewModel;
//                ActiveView = viewModel;

//                _logger.LogInformation($"ViewModel loaded for: {document.Title}");
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.LogDebug($"Loading cancelled for: {document?.Title}");

//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error loading ViewModel for: {document?.Title}");

//            }
//            finally
//            {
//                IsLoading = false;
//                LoadingMessage = string.Empty;
//            }
//        }

//        private async Task LoadComponentDataFromDatabase(IAGR_BaseComponent component)
//        {
//            try
//            {
//                var partNumber = component.PartNumber;

//                // Проверяем существование компонента в БД
//                var existsInDb = await _componentDataService.ComponentExistsInDatabaseAsync(partNumber);
//                component.IsInDatabase = existsInDb;

//                _logger.LogDebug($"Component {partNumber} exists in DB: {existsInDb}");

//                if (existsInDb)
//                {
//                    // Загружаем последнюю версию из БД
//                    var latestVersion = await _componentDataService.GetLatestComponentVersionAsync(partNumber);

//                    if (latestVersion != null)
//                    {
//                        _logger.LogDebug($"Loaded version {latestVersion.Version} for {partNumber}");

//                        // Обновляем свойства из БД
//                        component.Version = latestVersion.Version;
//                        component.HashSum = latestVersion.HashSum;

//                        // Загружаем AvaArticle если есть
//                        if (latestVersion.AvaArticleArticle.HasValue)
//                        {
//                            component.AvaArticle = latestVersion.AvaArticle;
//                            _logger.LogDebug($"Loaded AvaArticle {latestVersion.AvaArticleArticle} for {partNumber}");
//                        }
//                    }
//                }
//                else
//                {
//                    _logger.LogDebug($"Component {partNumber} not found in database");
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Error loading component data from database for {component?.PartNumber}");
//            }
//        }


//        private async void OnSelectionChangedAsync(IXDocument doc, Xarial.XCad.IXSelObject selObject)
//        {
//            try
//            {
//                if (selObject is IXFace face && face.Component?.ReferencedDocument is ISwDocument3D swDoc)
//                {
//                    _logger.LogDebug($"Selection changed to: {swDoc.Title}");

//                    // Асинхронно загружаем ViewModel для выбранного компонента
//                    var viewModel = _viewModelFactory.CreateComponent(swDoc);

//                    Selection = viewModel;
//                    ActiveView = viewModel;
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error handling selection change");
//            }
//        }
//        private void OnSelectionClearedAsync(IXDocument doc)
//        {
//            try
//            {
//                _logger.LogDebug("Selection cleared");

//                if (doc is ISwDocument3D swDoc)
//                {
//                    // Возвращаемся к основному виду документа
//                    ActiveComponent = swDoc;
//                    ActiveView = BaseComponent;
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error handling selection clear");
//            }
//        }
//        private void OnIdle(Xarial.XCad.IXApplication app)
//        {
//            try
//            {
//                // Проверяем, если документы закрыты, очищаем кэш
//                if (_app.Documents.Count == 0 && ActiveComponent != null)
//                {
//                    _logger.LogDebug("All documents closed, cleaning up");

//                    ActiveView = null;
//                    BaseComponent = null;
//                    ActiveComponent = null;
//                    Selection = null;

//                    // Можно очистить кэш если нужно
//                    // _viewModelCache.Clear();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error in idle handler");
//            }
//        }
//        public void Dispose()
//        {
//            try
//            {
//                _cancellationTokenSource?.Cancel();
//                _cancellationTokenSource?.Dispose();

//                if (_app != null)
//                {
//                    _app.Documents.DocumentActivated -= OnDocumentActivated;
//                    _app.Idle -= OnIdle;
//                }

//                _logger.LogInformation("TaskPaneViewModel disposed");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error disposing TaskPaneViewModel");
//            }
//        }
//    }
// }


#endregion