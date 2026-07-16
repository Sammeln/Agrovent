using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AGR_PropManager;
using AGR_PropManager.ViewModels.Components;
using Agrovent.DAL;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Helpers;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using AgroventInfrastructure.Enums;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationViewModel : BaseViewModel
    {
        private readonly AGR_AssemblyComponentVM _baseComponent;
        private readonly ILogger _logger;
        private readonly IUnitOfWork _unitOfWork;

        #region CTOR
        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent, IUnitOfWork unitOfWork)
        {
            //_logger = AGR_ServiceContainer.GetService<ILogger>();
            _baseComponent = baseComponent;
            _unitOfWork = unitOfWork;
            WindowTitle = $"Спецификация: {baseComponent.Name} ({baseComponent.PartNumber})";
            Initialize();
        }
        private async void Initialize()
        {

            // Инициализируем пустую коллекцию
            Components = new ObservableCollection<AGR_SpecificationItemVM>();
            GroupedComponentsView = CollectionViewSource.GetDefaultView(Components);
            UpdateGroupedView();

            // Инициализируем свойства главной сборки
            BaseAssemblyPreview = _baseComponent.Preview;
            BaseAssemblyName = _baseComponent.Name;
            BaseAssemblyPartNumber = _baseComponent.PartNumber;
            BaseAssemblyArticle = _baseComponent.AvaArticle;
            BaseAssemblyAvaType = _baseComponent.AvaType.ToString();
            BaseAssemblyAvaTypeEnum = _baseComponent.AvaType;
            BaseAssemblyPaint = _baseComponent.Paint;

            // Загружаем компоненты асинхронно
            await LoadComponentsAsync();
            // Загружаем материалы асинхронно
            await LoadMaterialsForComponentsAsync();

            await LoadPaintForComponentsAsync();
            // Загружаем AvaArticle для покупных компонентов асинхронно
            await LoadAvaArticlesForPurchasedComponentsAsync();

            // Выполняем валидацию после загрузки всех данных
            ValidateSpecification();
        }
        #endregion

        #region PROPS
        
        #region Коллекция для отображения с группировкой
        public ICollectionView GroupedComponentsView { get; private set; }

        private ObservableCollection<AGR_SpecificationItemVM> _components;
        public ObservableCollection<AGR_SpecificationItemVM> Components
        {
            get => _components;
            private set
            {
                if (Set(ref _components, value))
                {
                    UpdateGroupedView();
                }
            }
        }
        #endregion
        public string ConfigName => _baseComponent.ConfigName;

        #region Коллекция для хранения выбранных компонентов
        private ObservableCollection<AGR_SpecificationItemVM> _SelectedComponents = new ObservableCollection<AGR_SpecificationItemVM>();
        public ObservableCollection<AGR_SpecificationItemVM> SelectedComponents
        {
            get => _SelectedComponents;
            set => Set(ref _SelectedComponents, value);
        }
        #endregion

        #region WindowTitle
        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            private set => Set(ref _windowTitle, value);
        }
        #endregion

        #region HasComponents
        private bool _hasComponents;
        public bool HasComponents
        {
            get => _hasComponents;
            private set => Set(ref _hasComponents, value);
        }
        #endregion

        #region BaseAssemblyPreview
        private byte[] _baseAssemblyPreview;
        public byte[] BaseAssemblyPreview
        {
            get => _baseAssemblyPreview;
            set => Set(ref _baseAssemblyPreview, value);
        }
        #endregion

        #region BaseAssemblyName
        private string _baseAssemblyName;
        public string BaseAssemblyName
        {
            get => _baseAssemblyName;
            set => Set(ref _baseAssemblyName, value);
        }
        #endregion

        #region BaseAssemblyPartNumber
        private string _baseAssemblyPartNumber;
        public string BaseAssemblyPartNumber
        {
            get => _baseAssemblyPartNumber;
            set => Set(ref _baseAssemblyPartNumber, value);
        }
        #endregion

        #region Артикул главной сборки

        #region BaseAssemblyArticle
        private IAGR_AvaArticleModel? _baseAssemblyArticle;
        public IAGR_AvaArticleModel? BaseAssemblyArticle
        {
            get => _baseAssemblyArticle;
            set
            {
                Set(ref _baseAssemblyArticle, value);
                _baseComponent.AvaArticle = value;
                if (value?.Article != null)
                {
                    ArticleString = "Артикул№ " + BaseAssemblyArticle?.Article.ToString() + " " + BaseAssemblyArticle?.Name;
                }
                else
                {
                    ArticleString = string.Empty;
                }
            }
        }
        #endregion

        #region NoArticle
        private bool _noArticle;
        public bool NoArticle
        {
            get => _noArticle;
            set
            {
                Set(ref _noArticle, value);
                BaseAssemblyArticle = null;
                ValidateSpecification();
            }
        }

        #endregion

        #region ArticleString
        private string _ArticleString;
        public string ArticleString
        {
            get => _ArticleString;
            set => Set(ref _ArticleString, value);
        }
        #endregion 

        #endregion

        #region Тип AVA главной сборки
        private string _baseAssemblyAvaType;
        public string BaseAssemblyAvaType
        {
            get => _baseAssemblyAvaType;
            set => Set(ref _baseAssemblyAvaType, value);
        }

        private AGR_AvaType_e _baseAssemblyAvaTypeEnum;
        public AGR_AvaType_e BaseAssemblyAvaTypeEnum
        {
            get => _baseAssemblyAvaTypeEnum;
            set
            {
                if (Set(ref _baseAssemblyAvaTypeEnum, value))
                {
                    _baseComponent.AvaType = value;
                    OnPropertyChanged(nameof(BaseAssemblyAvaType));
                }
            }
        }
        #endregion

        #region BaseAssemblyPaint
        private IAGR_Material? _BaseAssemblyPaint;
        public IAGR_Material? BaseAssemblyPaint
        {
            get => _BaseAssemblyPaint;
            set
            {
                Set(ref _BaseAssemblyPaint, value);
                _baseComponent.Paint = value;
                if (BaseAssemblyPaint?.AvaModel != null)
                {
                    PaintString = BaseAssemblyPaint.AvaModel.Name;
                }
                else PaintString = string.Empty;
                OnPropertyChanged(nameof(BaseAssemblyPaintName));
            }
        }

        public string BaseAssemblyPaintName => BaseAssemblyPaint?.Name ?? string.Empty;
        #endregion 

        #region NoPaint
        private bool _noPaint;
        public bool NoPaint
        {
            get => _noPaint;
            set
            {
                Set(ref _noPaint, value);
                BaseAssemblyPaint = null;
                ValidateSpecification();
            }
        }
        #endregion

        #region PaintString 
        private string? _PaintString;
        public string? PaintString
        {
            get => _PaintString;
            set => Set(ref _PaintString, value);
        }
        #endregion 

        #region Errors

        private string _errors;
        public string Errors
        {
            get => _errors;
            set => Set(ref _errors, value);
        }
        #endregion

        #region HasErrors
        private bool _hasErrors;
        public bool HasErrors
        {
            get => _hasErrors;
            set => Set(ref _hasErrors, value);
        }
        #endregion

        #region Warnings

        private string _Warnings;
        public string Warnings
        {
            get => _Warnings;
            set => Set(ref _Warnings, value);
        }
        #endregion

        #region HasWarnings
        private bool _hasWarnings;
        public bool HasWarnings
        {
            get => _hasWarnings;
            set => Set(ref _hasWarnings, value);
        }
        #endregion

        #region Property - IgnoreErrors
        private bool _IgnoreErrors = false;
        public bool IgnoreErrors
        {
            get => _IgnoreErrors;
            set
            {
                Set(ref _IgnoreErrors, value);
                ValidateSpecification();
            }
        }
        #endregion 

        #endregion

        #region События
        public event Action CloseWindow;
        public event Action<bool?> DialogResultChanged;
        
        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (Set(ref _dialogResult, value))
                {
                    DialogResultChanged?.Invoke(_dialogResult);
                }
            }
        }
        #endregion

        #region METHODS
        private void ValidateSpecification()
        {

            Errors = null;
            HasErrors = false;
            Warnings = null;
            HasWarnings = false;
            if (IgnoreErrors == true) return;

            var errorList = new List<string>();
            var warningList = new List<string>();

            // Проверка: есть ли артикул у главной сборки (если не установлен чекбокс "без артикула")
            if (!NoArticle && string.IsNullOrEmpty(_baseComponent.AvaArticle?.Article.ToString()))
            {
                errorList.Add("У главной сборки отсутствует артикул.");
            }

            if (!NoPaint && string.IsNullOrEmpty(_baseComponent.Paint?.Article.ToString()))
            {
                errorList.Add("У главной сборки не указано покрытие.");
            }

            // Проверка наличия файла чертежа у главной сборки (если это не покупное)
            if (_baseComponent.ComponentType != AGR_ComponentType_e.Purchased)
            {
                if (_baseComponent is AGR_FileComponent baseFileComp)
                {
                    var drawPath = baseFileComp.GetDrawFilePath();
                    if (string.IsNullOrEmpty(drawPath))
                    {
                        errorList.Add($"У главной сборки \"{_baseComponent.Name}\" ({_baseComponent.PartNumber}) отсутствует файл чертежа.");
                    }
                    else
                    {
                        // Проверка актуальности чертежа
                        var drawWarning = CheckDrawingUpToDate(_baseComponent, drawPath);
                        if (!string.IsNullOrEmpty(drawWarning))
                        {
                            warningList.Add(drawWarning);
                        }
                    }
                }
            }

            var assembly = _baseComponent.SwDocument as ISwAssembly;
            var docsReadOnly = assembly.Configurations.Active.Components.AGR_TryFlatten()
                .Where(x => (File.Exists(x.ReferencedDocument.Path)
                        && File.GetAttributes(x.ReferencedDocument.Path)
                               .HasFlag(FileAttributes.ReadOnly)) == true)
                .Select(x => Path.GetFileName(x.ReferencedDocument.Path)).Distinct()
                .ToList();

            //Проверяем файлы только для чтения
            if (docsReadOnly.Count != 0)
            {
                foreach (var item in docsReadOnly)
                {
                    errorList.Add($"Файл {Path.GetFileName(item)} Только для чтения! Сохранение невозможно");
                }
            }

            // Проверка: материал у деталей и листовых деталей
            foreach (var comp in Components)
            {
                if (comp.ComponentType == AGR_ComponentType_e.Part || comp.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                {
                    if (comp.BaseMaterial == null)
                    {
                        errorList.Add($"У компонента \"{comp.Name}\" ({comp.PartNumber}) не установлен материал.");
                    }
                }


                // Проверка: артикул у покупных компонентов
                if (comp.ComponentType == AGR_ComponentType_e.Purchased)
                {
                    if (comp.AvaArticle == null && string.IsNullOrEmpty(comp.Article))
                    {
                        errorList.Add($"У покупного компонента \"{comp.Name}\" отсутствует артикул.");
                    }
                }

                // Проверка наличия файла чертежа у производимых компонентов (всё что не покупное)
                if (comp.ComponentType != AGR_ComponentType_e.Purchased)
                {
                    if (comp.Component is AGR_FileComponent fileComp)
                    {
                        var drawPath = fileComp.GetDrawFilePath();
                        if (string.IsNullOrEmpty(drawPath))
                        {
                            errorList.Add($"У производимого компонента \"{comp.Name}\" ({comp.PartNumber}) отсутствует файл чертежа.");
                        }
                        else
                        {
                            // Проверка актуальности чертежа
                            var drawWarning = CheckDrawingUpToDate(comp.Component, drawPath);
                            if (!string.IsNullOrEmpty(drawWarning))
                            {
                                warningList.Add(drawWarning);
                            }
                        }
                    }
                }
            }

            if (errorList.Any())
            {
                Errors = string.Join("\n", errorList);
                HasErrors = true;
            }
            if (warningList.Any())
            {
                Warnings = string.Join("\n", warningList);
                HasErrors = true;
                HasWarnings = true;
            }
        }
        private string CheckDrawingUpToDate(IAGR_BaseComponent component, string drawFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(drawFilePath) || !File.Exists(drawFilePath))
                    return null;

                var modelPath = component is AGR_FileComponent fileComp ? fileComp.CurrentModelFilePath : null;
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                    return null;

                var drawTime = File.GetLastWriteTime(drawFilePath);
                var modelTime = File.GetLastWriteTime(modelPath);

                if (modelTime > drawTime)
                {
                    return $"Чертеж компонента \"{component.Name}\" ({component.PartNumber}) возможно не обновлен (модель сохранена позже чертежа).";
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"Ошибка при проверке актуальности чертежа для компонента {component.PartNumber}");
                return null;
            }
        }
        private async Task LoadComponentsAsync()
        {
            if (_baseComponent.GetChildComponents() == null)
                return;

            try
            {
                var allComponents = _baseComponent.GetFlatComponents(true).ToList();

                var sortedComponents = allComponents
                    .OrderBy(c => c.ComponentType switch
                    {
                        AGR_ComponentType_e.Assembly => 0,
                        AGR_ComponentType_e.SheetMetallPart => 1,
                        AGR_ComponentType_e.Part => 2,
                        AGR_ComponentType_e.Purchased => 3,
                        _ => 4
                    })
                    .ToList();
                foreach (var component in sortedComponents)
                {
                    Components.Add(component as AGR_SpecificationItemVM);
                }

                //UpdateStatistics();
                HasComponents = Components.Count > 0;
            }
            catch (Exception ex)
            {
                // Логируем ошибку
                Console.WriteLine($"Ошибка загрузки компонентов: {ex.Message}");
            }
        }
        private async Task LoadMaterialsForComponentsAsync()
        {
            try
            {
                // 1. Собрать уникальные имена материалов из нужных компонентов
                var componentsWithMaterial = Components
                    .Where(c => (c.ComponentType == AGR_ComponentType_e.Part || c.ComponentType == AGR_ComponentType_e.SheetMetallPart) 
                    && !string.IsNullOrEmpty(c.BaseMaterial?.Name))
                    .ToList();

                if (!componentsWithMaterial.Any()) return; // Нечего загружать

                var uniqueMaterialNames = componentsWithMaterial
                    .Select(c => c.BaseMaterial.Name)
                    .Distinct()
                    .ToList();

                //_logger.LogInformation($"Поиск AvaArticle для {uniqueMaterialNames.Count} уникальных наименований материалов.");

                // 2. Использовать UnitOfWork и ComponentRepository для поиска
                // Начинаем транзакцию только для чтения, если это поддерживается и имеет смысл, иначе просто вызываем метод репозитория.
                // var transaction = await _unitOfWork.BeginTransactionAsync(); // Не обязательно для SELECT
                var materialNameToAvaArticleMap = await _unitOfWork.ComponentRepository.GetAvaArticlesByNameAsync(uniqueMaterialNames);
                // await transaction.RollbackAsync(); // Откатываем, так как это был SELECT

                //_logger.LogInformation($"Найдено {materialNameToAvaArticleMap.Count} AvaArticle по наименованиям.");

                // 3. Обновить соответствующие VM
                foreach (var specItem in componentsWithMaterial)
                {
                    if (materialNameToAvaArticleMap.TryGetValue(specItem.BaseMaterial.Name, out var avaArticleModel))
                    {
                        specItem.BaseMaterial = new AGR_Material(avaArticleModel);
                    }
                    else
                    {
                        specItem.BaseMaterial = null;
                        //_logger.LogWarning($"AvaArticle не найден для материала '{specItem.MaterialName}' компонента {specItem.PartNumber}.");
                    }
                }

               // _logger.LogInformation("Загрузка AvaArticle для материалов завершена.");
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, "Ошибка при загрузке AvaArticle для материалов компонентов спецификации.");
                // Можно показать сообщение пользователю, если необходимо
            }
        }
        private async Task LoadPaintForComponentsAsync()
        {
            try
            {
                // 1. Собрать уникальные имена материалов из нужных компонентов
                var componentsWithMaterial = Components
                    .Where(c => (c.ComponentType != AGR_ComponentType_e.Purchased && c.ComponentType != AGR_ComponentType_e.NA) 
                            && !string.IsNullOrEmpty(c.BasePaint?.Name))
                    .ToList();

                if (!componentsWithMaterial.Any()) return; // Нечего загружать

                var uniqueMaterialNames = componentsWithMaterial
                    .Select(c => c.BasePaint.Name)
                    .Distinct()
                    .ToList();

                //_logger.LogInformation($"Поиск AvaArticle для {uniqueMaterialNames.Count} уникальных наименований материалов.");

                // 2. Использовать UnitOfWork и ComponentRepository для поиска
                // Начинаем транзакцию только для чтения, если это поддерживается и имеет смысл, иначе просто вызываем метод репозитория.
                // var transaction = await _unitOfWork.BeginTransactionAsync(); // Не обязательно для SELECT
                var materialNameToAvaArticleMap = await _unitOfWork.ComponentRepository.GetAvaArticlesByNameAsync(uniqueMaterialNames);
                // await transaction.RollbackAsync(); // Откатываем, так как это был SELECT

                //_logger.LogInformation($"Найдено {materialNameToAvaArticleMap.Count} AvaArticle по наименованиям.");

                // 3. Обновить соответствующие VM
                foreach (var specItem in componentsWithMaterial)
                {
                    if (materialNameToAvaArticleMap.TryGetValue(specItem.BasePaint.Name, out var avaArticleModel))
                    {
                        specItem.BasePaint = new AGR_Material(avaArticleModel);
                    }
                    else
                    {
                        specItem.BasePaint = null;
                        //_logger.LogWarning($"AvaArticle не найден для материала '{specItem.MaterialName}' компонента {specItem.PartNumber}.");
                    }
                }

                // _logger.LogInformation("Загрузка AvaArticle для материалов завершена.");
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Ошибка при загрузке AvaArticle для материалов компонентов спецификации.");
                // Можно показать сообщение пользователю, если необходимо
            }
        }
        private async Task LoadAvaArticlesForPurchasedComponentsAsync()
        {
            try
            {
                // 1. Собрать компоненты типа Purchased, у которых AvaArticle == null и Article не пуст
                var componentsToSearch = Components
                    //.Where(c => c.ComponentType == AGR_ComponentType_e.Purchased && c.AvaArticle == null &&  !string.IsNullOrEmpty(c.Component.Article))
                    .Where(c => c.AvaArticle == null &&  !string.IsNullOrEmpty(c.Component.Article))
                    .ToList();

                if (!componentsToSearch.Any()) return; // Нечего загружать

                //_logger.LogInformation($"Поиск AvaArticle для {componentsToSearch.Count} покупных компонентов по Article.");

                // 2. Для каждого компонента, выполнить поиск по Article и обновить VM
                foreach (var specItem in componentsToSearch)
                {
                    int articleNumber = int.Parse(specItem.Component.Article); // Уже проверено на HasValue
                    //_logger.LogDebug($"Поиск AvaArticle для компонента {specItem.PartNumber} по Article {articleNumber}.");

                    var avaArticleModel = await _unitOfWork.ComponentRepository.GetAvaArticleByArticleNumberAsync(articleNumber);

                    if (avaArticleModel != null)
                    {
                        specItem.AvaArticle = avaArticleModel; // Устанавливаем найденный AvaArticleModel в VM
                        //_logger.LogDebug($"Установлен AvaArticle ({avaArticleModel.Article}: {avaArticleModel.Name}) для компонента {specItem.PartNumber} по Article {articleNumber}.");
                    }
                    else
                    {
                        //_logger.LogWarning($"AvaArticle не найден в БД по Article '{articleNumber}' для компонента {specItem.PartNumber}.");
                    }
                }

                //_logger.LogInformation("Загрузка AvaArticle для покупных компонентов завершена.");
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Ошибка при загрузке AvaArticle для покупных компонентов спецификации.");
                // Можно показать сообщение пользователю, если необходимо
            }
        }
        private void UpdateGroupedView()
        {
            if (GroupedComponentsView != null)
            {

                // Настраиваем группировку по типу компонента
                GroupedComponentsView.GroupDescriptions.Clear();
                GroupedComponentsView.GroupDescriptions.Add(
                    new PropertyGroupDescription("ComponentType"));

                // Сортируем группы по заданному порядку
                GroupedComponentsView.SortDescriptions.Clear();
                GroupedComponentsView.SortDescriptions.Add(
                    new SortDescription("ComponentType", ListSortDirection.Ascending));
                GroupedComponentsView.SortDescriptions.Add(
                    new SortDescription("Name", ListSortDirection.Ascending));
                OnPropertyChanged(nameof(GroupedComponentsView));

            }
        }
        public void Refresh()
        {
            // Загрузка компонентов и материалов теперь асинхронная.
            // Для простоты, можно вызвать Task.Run, но лучше использовать более сложные шаблоны.
            // Обратите внимание, что обновления UI должны происходить в основном потоке.
            Task.Run(async () =>
            {
                await LoadComponentsAsync();
                await LoadMaterialsForComponentsAsync();
                // Возможно, потребуется обновление UI в основном потоке, например, через Dispatcher.Invoke
            }).ContinueWith(t => {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Ошибка во время обновления спецификации.");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext()); // Или использовать подходящий способ для UI обновления
        }
        private void DeselectAllComponents()
        {
            foreach (var item in Components)
            {
                item.IsSelected = false;
            }
        }
        #endregion

        #region COMMANDS

        #region SelectComponentCommand
        private ICommand _SelectComponentCommand;
        public ICommand SelectComponentCommand => _SelectComponentCommand
            ??= new RelayCommand(OnSelectComponentCommandExecuted, CanSelectComponentCommandExecute);
        private bool CanSelectComponentCommandExecute(object p) => true;
        private void OnSelectComponentCommandExecuted(object p)
        {
            if (p is null)
            {

            }

            foreach (var item in SelectedComponents)
            {
                item.IsSelected = !item.IsSelected;
            }
        }
        #endregion

        #region SetBaseMaterialCommand
        private ICommand _SetBaseMaterialCommand;
        public ICommand SetBaseMaterialCommand => _SetBaseMaterialCommand
            ??= new RelayCommand(OnSetBaseMaterialCommandExecuted, CanSetBaseMaterialCommandExecute);
        private bool CanSetBaseMaterialCommandExecute(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true);
            if (_selectedComponents.Count() == 0) _selectedComponents = SelectedComponents;
            if (_selectedComponents.Any(p => p.Component.ComponentType != AGR_ComponentType_e.Part && p.Component.ComponentType != AGR_ComponentType_e.SheetMetallPart))
            {
                return false;
            }
            return true;
        }
        private void OnSetBaseMaterialCommandExecuted(object p)
        {
            var _selectedComponents = Components.Any(x => x.IsSelected) ?
                 Components.Where(x => x.IsSelected)
                : SelectedComponents;

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = "Товар";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, Topmost = true };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in _selectedComponents)
                    {
                        item.BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                        OnPropertyChanged(nameof(item.PartnumberOrArticle));
                        //var part = item.Component as AGR_PartComponentVM;
                        // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                        //part.BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                        //part.mProperties.FirstOrDefault(p => p.Name == AGR_PropertyNames.Material).Value = part.BaseMaterial.Name;
                        _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, item.PartNumber);

                        //OnPropertyChanged(nameof(part.BaseMaterial));
                        //OnPropertyChanged(nameof(item.MaterialName));
                    }

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
                DeselectAllComponents();
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion

        #region SetPaintCommand
        private ICommand _SetPaintCommand;
        public ICommand SetPaintCommand => _SetPaintCommand
            ??= new RelayCommand(OnSetPaintCommandExecuted, CanSetPaintCommandExecute);
        public bool CanSetPaintCommandExecute(object p)
        {
            var _selectedComponents = Components.Any(x => x.IsSelected) ?
                             Components.Where(x => x.IsSelected)
                            : SelectedComponents;
            if (_selectedComponents.Any(
                p => p.Component.ComponentType == AGR_ComponentType_e.Purchased))
            {
                return false;
            }
            return true;
        }
        private void OnSetPaintCommandExecuted(object p)
        {
            var _selectedComponents = Components.Any(x => x.IsSelected) ?
                             Components.Where(x => x.IsSelected)
                            : SelectedComponents;
            if (p.ToString() == "NoPaint")
            {
                foreach (var item in _selectedComponents)
                {
                    item.BasePaint = null;
                }
                DeselectAllComponents();
                return;
            }

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = "Краска порошковая";
                selectVm.SelectedAvaType = "Товар";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, Topmost = true };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in _selectedComponents)
                    {
                        item.BasePaint = new AGR_Paint(selectVm.SelectedArticle);
                        OnPropertyChanged(nameof(item.PartnumberOrArticle));
                        _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, item.PartNumber);

                    }

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
                DeselectAllComponents();
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion

        #region SetAvaArticleCommand
        private ICommand _SetAvaArticleCommand;
        public ICommand SetAvaArticleCommand => _SetAvaArticleCommand
            ??= new RelayCommand(OnSetAvaArticleCommandExecuted, CanSetAvaArticleCommandExecute);
        private bool CanSetAvaArticleCommandExecute(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true);
            //Нельзя устанавливать один и тот же артикул разным компонентам
            if (_selectedComponents.Count() > 1) return false;
            return true;
        }
        private void OnSetAvaArticleCommandExecuted(object p)
        {
            var comp = SelectedComponents.FirstOrDefault();

            try
            {
                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);

                if (comp.ComponentType != AGR_ComponentType_e.Purchased)
                {
                    selectVm.SelectedAvaType = AGR_AvaTypeNames.Component;
                    selectVm.SearchText = comp.PartNumber;
                }
                else
                {
                    selectVm.SelectedAvaType = AGR_AvaTypeNames.Purchased;
                    selectVm.SearchText = comp.Name;
                }


                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, Topmost = true };
                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    comp.AvaArticle = selectVm.SelectedArticle;
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                //_logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }

            DeselectAllComponents();
        }
        #endregion

        #region SetAvaTypeCommand
        private ICommand _SetAvaTypeCommand;
        public ICommand SetAvaTypeCommand => _SetAvaTypeCommand
            ??= new RelayCommand(OnSetAvaTypeCommandExecuted, CanSetAvaTypeCommandExecute);
        private bool CanSetAvaTypeCommandExecute(object p) => true;
        private void OnSetAvaTypeCommandExecuted(object p)
        {
            var _selectedComponents = Components.Where(x => x.IsSelected == true).Count() > 0 ?
                Components.Where(x => x.IsSelected == true)
                : SelectedComponents;

            foreach (var component in _selectedComponents)
            {

                switch (p.ToString())
                {
                    case "20021":
                    component.ComponentAvaType = AGR_AvaType_e.Purchased;
                    break;
                    case "3":
                    component.ComponentAvaType = AGR_AvaType_e.Production;
                    break;
                    case "5":
                    component.ComponentAvaType = AGR_AvaType_e.Component;
                    break;
                    case "50625":
                    component.ComponentAvaType = AGR_AvaType_e.VirtualComponent;
                    break;
                }
            }


            UpdateGroupedView();

        }
        #endregion

        #region CancelCommand
        private ICommand _CancelCommand;
        public ICommand CancelCommand => _CancelCommand
            ??= new RelayCommand(OnCancelExecuted);
        private void OnCancelExecuted(object p)
        {
            DialogResult = false;
            var view = p as Window;
            view?.Close();
        }
        #endregion

        #region SaveCommand
        private ICommand _SaveCommand;
        public ICommand SaveCommand => _SaveCommand
            ??= new RelayCommand(OnSaveCommandExecuted, CanSaveCommandExecute);
        private bool CanSaveCommandExecute(object p) => HasErrors == false;
        private void OnSaveCommandExecuted(object p)
        {
            DialogResult = true;
            var view = p as Window;
            view?.Close();
        }
        #endregion

        #region Для базовой сборки - SelectArticleCommand
        private ICommand _SelectArticleCommand;
        public ICommand SelectArticleCommand => _SelectArticleCommand
            ??= new RelayCommand(OnSelectArticleCommandExecuted, CanSelectArticleCommandExecute);
        private bool CanSelectArticleCommandExecute(object p) => NoArticle == false;
        private void OnSelectArticleCommandExecuted(object p)
        {
            SelectArticle();
        }
        private void SelectArticle()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = _baseComponent.Name;

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, Topmost = true };
                selectView.ShowActivated = true;
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    BaseAssemblyArticle = selectVm.SelectedArticle;
                }
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе артикула для главной сборки.");
            }
        }

        #endregion 

        #region Для базовой сборки -  SelectPaintCommand
        private ICommand _SelectPaintCommand;
        public ICommand SelectPaintCommand => _SelectPaintCommand
            ??= new RelayCommand(OnSelectPaintCommandExecuted, CanSelectPaintCommandExecute);
        private bool CanSelectPaintCommandExecute(object p) => NoPaint == false;
        private void OnSelectPaintCommandExecuted(object p)
        {
            SelectPaint();
        }
        private void SelectPaint()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = "Краска порошковая";
                selectVm.SelectedAvaType = "Товар";

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, Topmost = true };
                selectView.ShowActivated = true;
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    BaseAssemblyPaint = new AGR_Material(selectVm.SelectedArticle);
                    //_baseComponent.Paint = new AGR_Material(selectVm.SelectedArticle);
                }
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе покрытия для главной сборки.");
            }
        }
        #endregion 

        #endregion
    }
}
