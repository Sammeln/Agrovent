// File: ViewModels/Specification/AGR_AssemblyEditVM.cs
using AGR_PropManager.ViewModels.Windows;
using Agrovent.DAL;
using Agrovent.Infrastructure;
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.Views.Windows;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Agrovent.ViewModels.Specification
{
    public enum AGR_WindowMode_e
    {
        FullSave,
        EditExisting
    }

    /// <summary>
    /// VM для окна AGR_SpecificationWindow в режиме редактирования
    /// уже сохранённой в БД сборки (без версии, без экспорта, без живого документа SW).
    /// </summary>
    public class AGR_AssemblyEditVM : BaseViewModel
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger? _logger;
        private ComponentVersion _assemblyVersion;

        public AGR_WindowMode_e Mode => AGR_WindowMode_e.EditExisting;
        public AGR_AssemblyEditVM(string partNumber, int version, string displayName, IUnitOfWork unitOfWork, ILogger? logger = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger;

            Components = new ObservableCollection<AGR_AssemblyEditItemVM>();
            GroupedComponentsView = CollectionViewSource.GetDefaultView(Components);
            UpdateGroupedView();

            WindowTitle = $"Редактирование сборки: {displayName} ({partNumber})";
            //_ = InitializeAsync(partNumber, version);
        }
        public AGR_AssemblyEditVM(AGR_ComponentRegistryItemVM registryItem, IUnitOfWork unitOfWork, ILogger? logger = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger;

            Components = new ObservableCollection<AGR_AssemblyEditItemVM>();
            GroupedComponentsView = CollectionViewSource.GetDefaultView(Components);
            UpdateGroupedView();

            WindowTitle = $"Редактирование сборки: {registryItem.Name} ({registryItem.PartNumber})";

            //_ = InitializeAsync(registryItem.PartNumber, registryItem.Version);
        }

        #region Инициализация
        public async Task InitializeAsync(string partNumber, int version)
        {
            try
            {
                _assemblyVersion = await _unitOfWork.ComponentRepository.GetComponentVersionForEdit(partNumber, version);
                if (_assemblyVersion == null)
                {
                    _logger?.LogWarning($"Не найдена версия сборки {partNumber} v{version} для редактирования.");
                    Errors = "Не удалось загрузить сборку из БД.";
                    HasErrors = true;
                    return;
                }

                BaseAssemblyPreview = _assemblyVersion.PreviewImage;
                BaseAssemblyName = _assemblyVersion.Name;
                ConfigName = _assemblyVersion.ConfigName;
                BaseAssemblyPartNumber = partNumber;

                _baseAssemblyAvaTypeEnum = _assemblyVersion.AvaType;
                OnPropertyChanged(nameof(BaseAssemblyAvaTypeEnum));

                if (_assemblyVersion.AvaArticle != null)
                {
                    _baseAssemblyArticle = _assemblyVersion.AvaArticle;
                    ArticleString = $"Артикул№ {_assemblyVersion.AvaArticle.Article} {_assemblyVersion.AvaArticle.Name}";
                }
                _noArticle = _assemblyVersion.AvaArticle == null;
                OnPropertyChanged(nameof(NoArticle));

                if (_assemblyVersion.Material?.HasPaint == true && _assemblyVersion.Material.PaintAvaArticle != null)
                {
                    _baseAssemblyPaint = new AGR_Paint(_assemblyVersion.Material.PaintAvaArticle);
                }
                _noPaint = _assemblyVersion.Material?.HasPaint != true;
                OnPropertyChanged(nameof(BaseAssemblyPaintName));
                OnPropertyChanged(nameof(NoPaint));

                await LoadCompositionAsync(partNumber, version);
                ValidateSpecification();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Ошибка при загрузке сборки {partNumber} v{version} для редактирования.");
                Errors = "Ошибка при загрузке данных сборки: " + ex.Message;
                HasErrors = true;
            }
        }

        private async Task LoadCompositionAsync(string partNumber, int version)
        {
            Components.Clear();

            var flatComponents = await _unitOfWork.ComponentRepository.GetFlatAssemblyComponents(partNumber, version);

            var sorted = flatComponents
                .OrderBy(c => c.Entity.ComponentType switch
                {
                    AGR_ComponentType_e.Assembly => 0,
                    AGR_ComponentType_e.SheetMetallPart => 1,
                    AGR_ComponentType_e.Part => 2,
                    AGR_ComponentType_e.Purchased => 3,
                    _ => 4
                })
                .ThenBy(c => c.Entity.Name);

            foreach (var c in sorted)
            {
                Components.Add(new AGR_AssemblyEditItemVM(c.Entity, c.TotalQuantity));
            }

            HasComponents = Components.Count > 0;
        }
        #endregion

        #region PROPS

        public ICollectionView GroupedComponentsView { get; private set; }

        private ObservableCollection<AGR_AssemblyEditItemVM> _components;
        public ObservableCollection<AGR_AssemblyEditItemVM> Components
        {
            get => _components;
            private set => Set(ref _components, value);
        }

        private ObservableCollection<AGR_AssemblyEditItemVM> _selectedComponents = new();
        public ObservableCollection<AGR_AssemblyEditItemVM> SelectedComponents
        {
            get => _selectedComponents;
            set => Set(ref _selectedComponents, value);
        }

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            private set => Set(ref _windowTitle, value);
        }

        private bool _hasComponents;
        public bool HasComponents
        {
            get => _hasComponents;
            private set => Set(ref _hasComponents, value);
        }

        private byte[] _baseAssemblyPreview;
        public byte[] BaseAssemblyPreview
        {
            get => _baseAssemblyPreview;
            set => Set(ref _baseAssemblyPreview, value);
        }

        private string _baseAssemblyName;
        public string BaseAssemblyName
        {
            get => _baseAssemblyName;
            set => Set(ref _baseAssemblyName, value);
        }

        private string _configName;
        public string ConfigName
        {
            get => _configName;
            set => Set(ref _configName, value);
        }

        private string _baseAssemblyPartNumber;
        public string BaseAssemblyPartNumber
        {
            get => _baseAssemblyPartNumber;
            set => Set(ref _baseAssemblyPartNumber, value);
        }

        #region Артикул главной сборки
        private IAGR_AvaArticleModel? _baseAssemblyArticle;
        public IAGR_AvaArticleModel? BaseAssemblyArticle
        {
            get => _baseAssemblyArticle;
            set
            {
                Set(ref _baseAssemblyArticle, value);
                ArticleString = value?.Article != null
                    ? $"Артикул№ {value.Article} {value.Name}"
                    : string.Empty;
                ValidateSpecification();
            }
        }

        private bool _noArticle;
        public bool NoArticle
        {
            get => _noArticle;
            set
            {
                Set(ref _noArticle, value);
                if (value) BaseAssemblyArticle = null;
                ValidateSpecification();
            }
        }

        private string _articleString;
        public string ArticleString
        {
            get => _articleString;
            set => Set(ref _articleString, value);
        }
        #endregion

        private AGR_AvaType_e _baseAssemblyAvaTypeEnum;
        public AGR_AvaType_e BaseAssemblyAvaTypeEnum
        {
            get => _baseAssemblyAvaTypeEnum;
            set => Set(ref _baseAssemblyAvaTypeEnum, value);
        }

        #region Покрытие главной сборки
        private IAGR_Material? _baseAssemblyPaint;
        public IAGR_Material? BaseAssemblyPaint
        {
            get => _baseAssemblyPaint;
            set
            {
                Set(ref _baseAssemblyPaint, value);
                OnPropertyChanged(nameof(BaseAssemblyPaintName));
                ValidateSpecification();
            }
        }
        public string BaseAssemblyPaintName => BaseAssemblyPaint?.Name ?? string.Empty;

        private bool _noPaint;
        public bool NoPaint
        {
            get => _noPaint;
            set
            {
                Set(ref _noPaint, value);
                if (value) BaseAssemblyPaint = null;
                ValidateSpecification();
            }
        }
        #endregion

        private string _errors;
        public string Errors
        {
            get => _errors;
            set => Set(ref _errors, value);
        }

        private bool _hasErrors;
        public bool HasErrors
        {
            get => _hasErrors;
            set => Set(ref _hasErrors, value);
        }

        private string _warnings;
        public string Warnings
        {
            get => _warnings;
            set => Set(ref _warnings, value);
        }

        private bool _hasWarnings;
        public bool HasWarnings
        {
            get => _hasWarnings;
            set => Set(ref _hasWarnings, value);
        }

        private bool _ignoreErrors;
        public bool IgnoreErrors
        {
            get => _ignoreErrors;
            set
            {
                Set(ref _ignoreErrors, value);
                ValidateSpecification();
            }
        }

        public bool? DialogResult { get; set; }

        #endregion

        #region METHODS

        /// <summary>
        /// Валидация для режима редактирования БД — без проверок живых файлов/чертежей,
        /// только то, что реально хранится в БД.
        /// </summary>
        private void ValidateSpecification()
        {
            Errors = null;
            HasErrors = false;
            Warnings = null;
            HasWarnings = false;
            if (IgnoreErrors) return;

            var errorList = new List<string>();

            if (!NoArticle && BaseAssemblyArticle == null)
                errorList.Add("У главной сборки отсутствует артикул.");

            if (!NoPaint && BaseAssemblyPaint == null)
                errorList.Add("У главной сборки не указано покрытие.");

            foreach (var comp in Components)
            {
                if (comp.ComponentType is AGR_ComponentType_e.Part or AGR_ComponentType_e.SheetMetallPart
                    && comp.BaseMaterial == null)
                {
                    errorList.Add($"У компонента \"{comp.Name}\" ({comp.PartNumber}) не установлен материал.");
                }

                if (comp.ComponentType == AGR_ComponentType_e.Purchased && comp.AvaArticle == null)
                {
                    errorList.Add($"У покупного компонента \"{comp.Name}\" отсутствует артикул.");
                }
            }

            if (errorList.Any())
            {
                Errors = string.Join("\n", errorList);
                HasErrors = true;
            }
        }

        private void UpdateGroupedView()
        {
            if (GroupedComponentsView == null) return;
            GroupedComponentsView.GroupDescriptions.Clear();
            GroupedComponentsView.GroupDescriptions.Add(new PropertyGroupDescription("ComponentType"));
            GroupedComponentsView.SortDescriptions.Clear();
            GroupedComponentsView.SortDescriptions.Add(new SortDescription("ComponentType", ListSortDirection.Ascending));
            GroupedComponentsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        private void DeselectAllComponents()
        {
            foreach (var item in Components) item.IsSelected = false;
        }

        #endregion

        #region COMMANDS

        #region SelectComponentCommand
        private ICommand _SelectComponentCommand;
        public ICommand SelectComponentCommand => _SelectComponentCommand
            ??= new RelayCommand(_ =>
            {
                foreach (var item in SelectedComponents) item.IsSelected = !item.IsSelected;
            });
        #endregion

        #region SetBaseMaterialCommand
        private ICommand _SetBaseMaterialCommand;
        public ICommand SetBaseMaterialCommand => _SetBaseMaterialCommand
            ??= new RelayCommand(OnSetBaseMaterialExecuted, CanSetBaseMaterialExecute);

        private bool CanSetBaseMaterialExecute(object p)
        {
            var selected = Components.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) selected = SelectedComponents.ToList();
            return selected.Any() && selected.All(c =>
                c.ComponentType is AGR_ComponentType_e.Part or AGR_ComponentType_e.SheetMetallPart);
        }

        private void OnSetBaseMaterialExecuted(object p)
        {
            var selected = Components.Any(x => x.IsSelected)
                ? Components.Where(x => x.IsSelected).ToList()
                : SelectedComponents.ToList();

            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext) { SelectedAvaType = "Товар" };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in selected)
                        item.BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе материала для компонентов состава.");
            }
            finally
            {
                DeselectAllComponents();
                ValidateSpecification();
            }
        }
        #endregion

        #region SetPaintCommand
        private ICommand _SetPaintCommand;
        public ICommand SetPaintCommand => _SetPaintCommand
            ??= new RelayCommand(OnSetPaintExecuted, CanSetPaintExecute);

        private bool CanSetPaintExecute(object p)
        {
            var selected = Components.Any(x => x.IsSelected) ? Components.Where(x => x.IsSelected) : SelectedComponents;
            return !selected.Any(c => c.ComponentType == AGR_ComponentType_e.Purchased);
        }

        private void OnSetPaintExecuted(object p)
        {
            var selected = Components.Any(x => x.IsSelected)
                ? Components.Where(x => x.IsSelected).ToList()
                : SelectedComponents.ToList();

            if (p?.ToString() == "NoPaint")
            {
                foreach (var item in selected) item.BasePaint = null;
                DeselectAllComponents();
                ValidateSpecification();
                return;
            }

            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext)
                {
                    SearchText = "Краска порошковая",
                    SelectedAvaType = "Товар"
                };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    foreach (var item in selected)
                        item.BasePaint = new AGR_Paint(selectVm.SelectedArticle);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе покрытия для компонентов состава.");
            }
            finally
            {
                DeselectAllComponents();
                ValidateSpecification();
            }
        }
        #endregion

        #region SetAvaArticleCommand
        private ICommand _SetAvaArticleCommand;
        public ICommand SetAvaArticleCommand => _SetAvaArticleCommand
            ??= new RelayCommand(OnSetAvaArticleExecuted, CanSetAvaArticleExecute);

        private bool CanSetAvaArticleExecute(object p) => Components.Count(x => x.IsSelected) <= 1;

        private void OnSetAvaArticleExecuted(object p)
        {
            var comp = SelectedComponents.FirstOrDefault() ?? Components.FirstOrDefault(x => x.IsSelected);
            if (comp == null) return;

            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext);
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

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    comp.AvaArticle = selectVm.SelectedArticle;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе артикула компонента.");
            }
            finally
            {
                DeselectAllComponents();
                ValidateSpecification();
            }
        }
        #endregion

        #region SetAvaTypeCommand
        private ICommand _SetAvaTypeCommand;
        public ICommand SetAvaTypeCommand => _SetAvaTypeCommand
            ??= new RelayCommand(p =>
            {
                var selected = Components.Any(x => x.IsSelected) ? Components.Where(x => x.IsSelected) : SelectedComponents;
                foreach (var component in selected)
                {
                    component.ComponentAvaType = p.ToString() switch
                    {
                        "20021" => AGR_AvaType_e.Purchased,
                        "3" => AGR_AvaType_e.Production,
                        "5" => AGR_AvaType_e.Component,
                        "50625" => AGR_AvaType_e.VirtualComponent,
                        _ => component.ComponentAvaType
                    };
                }
                UpdateGroupedView();
            });
        #endregion

        #region SelectArticleCommand (для главной сборки)
        private ICommand _SelectArticleCommand;
        public ICommand SelectArticleCommand => _SelectArticleCommand
            ??= new RelayCommand(_ => SelectArticle(), _ => !NoArticle);

        private void SelectArticle()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext) { SearchText = BaseAssemblyName };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    BaseAssemblyArticle = selectVm.SelectedArticle;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе артикула для главной сборки.");
            }
        }
        #endregion

        #region SelectPaintCommand (для главной сборки)
        private ICommand _SelectPaintCommand;
        public ICommand SelectPaintCommand => _SelectPaintCommand
            ??= new RelayCommand(_ => SelectPaint(), _ => !NoPaint);

        private void SelectPaint()
        {
            try
            {
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext)
                {
                    SearchText = "Краска порошковая",
                    SelectedAvaType = "Товар"
                };
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm, ShowActivated = true };
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    BaseAssemblyPaint = new AGR_Paint(selectVm.SelectedArticle);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе покрытия для главной сборки.");
            }
        }
        #endregion

        #region CancelCommand
        private ICommand _CancelCommand;
        public ICommand CancelCommand => _CancelCommand ??= new RelayCommand(p =>
        {
            DialogResult = false;
            (p as Window)?.Close();
        });
        #endregion

        #region SaveCommand — здесь ключевое отличие от полного сохранения
        private ICommand _SaveCommand;
        public ICommand SaveCommand => _SaveCommand
            ??= new RelayCommand(OnSaveExecuted, _ => !HasErrors);

        private async void OnSaveExecuted(object p)
        {
            try
            {
                await PersistChangesAsync();
                await _unitOfWork.CompleteAsync();
                DialogResult = true;
                (p as Window)?.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при сохранении изменений сборки в БД.");
                Errors = "Не удалось сохранить изменения: " + ex.Message;
                HasErrors = true;
            }
        }

        private async Task PersistChangesAsync()
        {
            // Главная сборка
            await _unitOfWork.ComponentRepository.UpdateComponentVersionEditAsync(new AGR_ComponentEditData
            {
                ComponentVersionId = _assemblyVersion.Id,
                AvaType = BaseAssemblyAvaTypeEnum,
                AvaArticleArticle = BaseAssemblyArticle?.Article,
                PaintAvaArticleId = (BaseAssemblyPaint?.AvaModel as AvaArticleModel)?.Article,
                PaintName = BaseAssemblyPaint?.Name
            });

            // Компоненты состава
            var edits = Components.Select(c => new AGR_ComponentEditData
            {
                ComponentVersionId = c.Entity.Id,
                AvaType = c.ComponentAvaType,
                AvaArticleArticle = c.AvaArticle?.Article,
                MaterialAvaArticleId = (c.BaseMaterial?.AvaModel as AvaArticleModel)?.Article,
                MaterialName = c.BaseMaterial?.Name,
                PaintAvaArticleId = (c.BasePaint?.AvaModel as AvaArticleModel)?.Article,
                PaintName = c.BasePaint?.Name
            });

            await _unitOfWork.ComponentRepository.UpdateComponentVersionEditsAsync(edits);
            await _unitOfWork.CompleteAsync();
        }
        #endregion

        #endregion
    }
}