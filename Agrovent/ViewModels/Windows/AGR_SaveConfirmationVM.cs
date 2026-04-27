// File: ViewModels/Windows/AGR_SaveConfirmationVM.cs
using Agrovent.Infrastructure.Commands;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Properties;
using Agrovent.ViewModels.Specification;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.DAL;
using Agrovent.Views.Windows;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using System.ComponentModel;
using System.Windows.Data;

namespace Agrovent.ViewModels.Windows
{
    public class AGR_SaveConfirmationVM : BaseViewModel
    {
        private readonly ILogger? _logger;
        private IAGR_BaseComponent _component;

        public AGR_SaveConfirmationVM(IAGR_BaseComponent component, ILogger? logger = null)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _logger = logger;

            InitializeProperties();
        }

        #region Properties
        public bool? DialogResult { get; set; }

        #region Component Info
        public string ComponentName => _component.Name;
        public string PartNumber => _component.PartNumber;
        public string ConfigName => _component.ConfigName;
        public AGR_ComponentType_e ComponentType => _component.ComponentType;
        public AGR_AvaType_e AvaType => _component.AvaType;

        private byte[] _preview;
        public byte[] Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }
        #endregion

        #region Material
        private IAGR_Material _baseMaterial;
        public IAGR_Material BaseMaterial
        {
            get => _baseMaterial;
            set
            {
                if (Set(ref _baseMaterial, value))
                {
                    if (_component is AGR_PartComponentVM partComponent)
                    {
                        partComponent.BaseMaterial = value;
                    }

                    OnPropertyChanged(nameof(MaterialName));
                    OnPropertyChanged(nameof(HasErrors));

                }
            }
        }
        public string MaterialName => 
            (BaseMaterial?.AvaModel?.Article.ToString() + " " + BaseMaterial?.Name) 
            ?? string.Empty;

        #endregion

        #region Color
        private IAGR_Material? _paint;
        public IAGR_Material? Paint
        {
            get => _paint;
            set
            {
                if (Set(ref _paint, value))
                {
                    if (_component is AGR_PartComponentVM partComponent)
                    {
                        partComponent.Paint = value;
                    }
                    if (_component is AGR_AssemblyComponentVM assemComponent)
                    {
                        assemComponent.Paint = value;
                    }

                    OnPropertyChanged(nameof(ColorName));
                    OnPropertyChanged(nameof(HasErrors));

                }
            }
        }

        private bool _noPaint;
        public bool NoPaint
        {
            get => _noPaint;
            set
            {
                if (Set(ref _noPaint, value))
                {
                    if (NoPaint)
                    {
                        //Если есть ошибка "нет артикула" удаляем
                        if (ErrorMessages.Contains(AGR_SaveConfirmationErrors.NoColor)) ErrorMessages.Remove(AGR_SaveConfirmationErrors.NoColor);

                        //Если поставили "нет артикула" убираем артикул из компонента
                        Paint = null;
                    }
                    else
                    {
                        //Если убрали галочку, проверяем, нет ли ошибки "нет артикула" в списке
                        //если нет, добавляем
                        if (!ErrorMessages.Contains(AGR_SaveConfirmationErrors.NoColor)) ErrorMessages.Add(AGR_SaveConfirmationErrors.NoColor);
                    }

                    OnPropertyChanged(nameof(HasErrors));
                }
            }
        }

        public string ColorName => 
            (Paint?.AvaModel?.Article.ToString() + " " + Paint?.Name)
            ?? string.Empty;
        #endregion

        #region Article (for purchased parts)


        #region Property - IAGR_AvaArticleModel AvaArticle
        private IAGR_AvaArticleModel? _AvaArticle;
        public IAGR_AvaArticleModel? AvaArticle
        {
            get => _AvaArticle;
            set
            {
                Set(ref _AvaArticle, value);
                if (_component is AGR_PartComponentVM partComponent)
                {
                    partComponent.AvaArticle = value;
                }
                if (_component is AGR_AssemblyComponentVM assemComponent)
                {
                    assemComponent.AvaArticle = value;
                }
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(HasErrors));
            }
        }
        #endregion

        #region Property - NoArticle
        private bool _NoArticle;
        public bool NoArticle
        {
            get => _NoArticle;
            set
            {
                Set(ref _NoArticle, value);
                if (NoArticle)
                {
                    //Если есть ошибка "нет артикула" удаляем
                    if (ErrorMessages.Contains(AGR_SaveConfirmationErrors.NoArticle)) ErrorMessages.Remove(AGR_SaveConfirmationErrors.NoArticle);

                    //Если поставили "нет артикула" убираем артикул из компонента
                    AvaArticle = null;
                }
                else
                {
                    //Если убрали галочку, проверяем, нет ли ошибки "нет артикула" в списке
                    //если нет, добавляем
                    if (!ErrorMessages.Contains(AGR_SaveConfirmationErrors.NoArticle)) ErrorMessages.Add(AGR_SaveConfirmationErrors.NoArticle);
                }

                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(HasErrors));
            }
        }
        #endregion 
        public string Article => 
            (AvaArticle?.Article.ToString() + " " + AvaArticle?.Name)
            ?? string.Empty;

        #endregion 


        #region Properties Collection (for blank properties)
        private ObservableCollection<IXProperty> _blankProperties;
        public ObservableCollection<IXProperty> BlankProperties
        {
            get => _blankProperties;
            set => Set(ref _blankProperties, value);
        }
        #endregion

        #region Specification Items (for assemblies)
        private ObservableCollection<AGR_SpecificationItemVM> _specificationItems;
        public ObservableCollection<AGR_SpecificationItemVM> SpecificationItems
        {
            get => _specificationItems;
            set
            {
                if (Set(ref _specificationItems, value))
                {
                    OnPropertyChanged(nameof(SpecificationItemsView));
                }
            }
        }

        private CollectionViewSource _specificationItemsViewSource;
        public ICollectionView SpecificationItemsView
        {
            get
            {
                if (_specificationItemsViewSource == null && SpecificationItems != null)
                {
                    _specificationItemsViewSource = new CollectionViewSource();
                    _specificationItemsViewSource.Source = SpecificationItems;
                    _specificationItemsViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AGR_SpecificationItemVM.ComponentType)));
                }
                return _specificationItemsViewSource?.View;
            }
        }

        #endregion

        #region Error Messages
        private ObservableCollection<string> _errorMessages;
        public ObservableCollection<string> ErrorMessages
        {
            get => _errorMessages;
            set => Set(ref _errorMessages, value);
        }

        public bool HasErrors => ErrorMessages?.Any() == true;
        #endregion

        #region IsPart / IsAssembly / IsPurchased
        public bool IsPart => ComponentType == AGR_ComponentType_e.Part ||
                              ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsSheetMetal => ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsAssembly => ComponentType == AGR_ComponentType_e.Assembly;
        public bool IsPurchased => ComponentType == AGR_ComponentType_e.Purchased;
        public bool IsProduced => IsPart || IsAssembly;
        #endregion

        #endregion

        #region Commands

        #region SaveCommand
        private ICommand _SaveCommand;
        public ICommand SaveCommand => _SaveCommand
            ??= new RelayCommand(OnSaveExecuted, CanSaveExecute);
        private bool CanSaveExecute(object p) => !HasErrors;
        private void OnSaveExecuted(object p)
        {
            DialogResult = true;
            var view = p as Window;
            view?.Close();
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

        #region SelectMaterialCommand
        private ICommand _SelectMaterialCommand;
        public ICommand SelectMaterialCommand => _SelectMaterialCommand
            ??= new RelayCommand(OnSelectMaterialExecuted, CanSelectMaterialExecute);
        private bool CanSelectMaterialExecute(object p) => IsProduced;
        private async void OnSelectMaterialExecuted(object p)
        {
            await OpenMaterialSelectionAsync();
        }
        private async Task OpenMaterialSelectionAsync()
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора материала");

                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;
                selectVm.SelectedAvaType = "Товар";
                selectView.ShowDialog();

                var result = selectVm.IsDialogResultAccepted;

                if (result == true && selectVm.SelectedArticle != null)
                {
                    BaseMaterial = new AGR_Material(selectVm.SelectedArticle);

                    if (IsPart)
                    {
                        (_component as AGR_PartComponentVM).BaseMaterial = BaseMaterial;
                    }

                    _logger?.LogInformation("Выбран материал {Material}", BaseMaterial.Name);
                    ValidateComponent();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе материала");
            }
        }
        #endregion

        #region SelectColorCommand
        private ICommand _SelectColorCommand;
        public ICommand SelectColorCommand => _SelectColorCommand
            ??= new RelayCommand(OnSelectColorExecuted, CanSelectColorExecute);
        private bool CanSelectColorExecute(object p) => IsProduced;
        private async void OnSelectColorExecuted(object p)
        {
            await OpenColorSelectionAsync();
        }
        private async Task OpenColorSelectionAsync()
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора цвета/покрытия");

                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = "Краска порошковая ";

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;

                selectView.ShowDialog();

                var result = selectVm.IsDialogResultAccepted;

                if (result == true && selectVm.SelectedArticle != null)
                {
                    Paint = new AGR_Material(selectVm.SelectedArticle);

                    // При выборе цвета снимаем флаг "без покрытия"
                    NoPaint = false;
                    _logger?.LogInformation("Выбран цвет/покрытие {Color}", Paint.Name);
                    ValidateComponent();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе цвета/покрытия");
            }
        }
        #endregion

        #region SelectArticleCommand
        private ICommand _SelectArticleCommand;
        public ICommand SelectArticleCommand => _SelectArticleCommand
            ??= new RelayCommand(OnSelectArticleCommandExecuted, CanSelectArticleCommandExecute);
        private bool CanSelectArticleCommandExecute(object p) => true;
        private async void OnSelectArticleCommandExecuted(object p)
        {
            await SelectArticleAsync();
        }
        private async Task SelectArticleAsync()
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора материала");

                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = "Все типы";
                selectVm.SearchText = ComponentName;

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;
                selectView.ShowDialog();

                var result = selectVm.IsDialogResultAccepted;

                if (result == true && selectVm.SelectedArticle != null)
                {
                    AvaArticle = selectVm.SelectedArticle;

                    _logger?.LogInformation("Выбран артикул {AvaArticle}", AvaArticle.Article);
                    ValidateComponent();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе материала");
            }
        }
        #endregion 

        #endregion

        #region Private Methods

        private void InitializeProperties()
        {
            ErrorMessages = new ObservableCollection<string>();
            Preview = _component.Preview;

            if (_component is AGR_PartComponentVM part)
            {
                BaseMaterial = part.BaseMaterial;
                Paint = part.Paint;
                AvaArticle = part.AvaArticle;

                // Если цвет не установлен, по умолчанию ставим "без покрытия"
                NoPaint = Paint == null;

                // Load blank properties based on component type
                LoadBlankProperties(part.PropertiesCollection);
            }
            else if (_component is AGR_AssemblyComponentVM assembly)
            {
                // Get assembly paint/color
                Paint = assembly.Paint;
                AvaArticle = assembly.AvaArticle;

                // Для сборок тоже устанавливаем флаг "без покрытия" если краски нет
                NoPaint = Paint == null;

                LoadBlankProperties(assembly.PropertiesCollection);

                // Load specification items
                LoadSpecificationItems(assembly);
            }

            ValidateComponent();
        }

        private void LoadBlankProperties(IAGR_PropertiesCollection propertiesCollection)
        {
            BlankProperties = new ObservableCollection<IXProperty>();

            if (propertiesCollection == null) return;

            foreach (var prop in propertiesCollection.Properties)
            {
                BlankProperties.Add(prop);
            }
        }

        private void LoadSpecificationItems(AGR_AssemblyComponentVM assembly)
        {
            SpecificationItems = new ObservableCollection<AGR_SpecificationItemVM>();

            var items = assembly.GetFlatComponents();
            foreach (var item in items)
            {
                if (item is AGR_SpecificationItemVM vm)
                {
                    SpecificationItems.Add(vm);
                }
            }
        }

        private void ValidateComponent()
        {
            ErrorMessages.Clear();

            if (IsPart)
            {
                if (string.IsNullOrEmpty(MaterialName) || string.IsNullOrWhiteSpace(MaterialName))
                {
                    ErrorMessages.Add(AGR_SaveConfirmationErrors.NoMaterial);
                }
            }
            if (IsProduced)
            {

                if (string.IsNullOrEmpty(ColorName) || string.IsNullOrWhiteSpace(ColorName))
                {
                    ErrorMessages.Add(AGR_SaveConfirmationErrors.NoColor);
                }
                if (string.IsNullOrEmpty(Article) || string.IsNullOrWhiteSpace(Article))
                {
                    ErrorMessages.Add(AGR_SaveConfirmationErrors.NoArticle);
                }
            }

            if (IsPurchased)
            {
                if (string.IsNullOrEmpty(Article) || string.IsNullOrWhiteSpace(Article))
                {
                    ErrorMessages.Add(AGR_SaveConfirmationErrors.NoArticle);
                }
            }

            // Validate specification items for assemblies
            if (IsAssembly && SpecificationItems != null)
            {
                foreach (var item in SpecificationItems)
                {
                    // Check material for parts and sheet metal parts
                    if (item.ComponentType == AGR_ComponentType_e.Part ||
                        item.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                    {
                        if (string.IsNullOrEmpty(item.MaterialName))
                        {
                            string errorMsg = $"{item.Name}: не указан материал компонента";
                            if (!ErrorMessages.Contains(errorMsg))
                            {
                                ErrorMessages.Add(errorMsg);
                            }
                        }
                    }

                    // Check article for purchased components
                    if (item.ComponentType == AGR_ComponentType_e.Purchased)
                    {
                        if (item.AvaArticle == null)
                        {
                            string errorMsg = $"{item.Name}: не указан артикул покупного компонента";
                            if (!ErrorMessages.Contains(errorMsg))
                            {
                                ErrorMessages.Add(errorMsg);
                            }
                        }
                    }
                }
            }

            if (NoPaint)
            {
                ErrorMessages.Remove(AGR_SaveConfirmationErrors.NoColor);
            }
            if (NoArticle)
            {
                ErrorMessages.Remove(AGR_SaveConfirmationErrors.NoArticle);
            }

            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(ErrorMessages));

        }

        private string GetUnitForProperty(string propertyName)
        {
            // Определяем единицу измерения на основе имени свойства
            if (propertyName.Contains("Length", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Len", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Длина", StringComparison.OrdinalIgnoreCase))
                return "мм";

            if (propertyName.Contains("Width", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Wid", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Ширина", StringComparison.OrdinalIgnoreCase))
                return "мм";

            if (propertyName.Contains("Thickness", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Thick", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Толщина", StringComparison.OrdinalIgnoreCase))
                return "мм";

            if (propertyName.Contains("Area", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Площадь", StringComparison.OrdinalIgnoreCase))
                return "м²";

            if (propertyName.Contains("Mass", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Масса", StringComparison.OrdinalIgnoreCase))
                return "кг";

            if (propertyName.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Объем", StringComparison.OrdinalIgnoreCase))
                return "м³";

            if (propertyName.Contains("Bends", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Сгибы", StringComparison.OrdinalIgnoreCase))
                return "шт";

            if (propertyName.Contains("Holes", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Отверстия", StringComparison.OrdinalIgnoreCase))
                return "шт";

            return string.Empty;
        }

        #endregion

    }

    public static class AGR_SaveConfirmationErrors
    {
        public const string NoMaterial = "Не указан материал";
        public const string NoColor = "Не указан цвет";
        public const string NoArticle = "Не указан артикул";
    }

}
