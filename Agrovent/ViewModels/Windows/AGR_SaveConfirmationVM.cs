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

namespace Agrovent.ViewModels.Windows
{
    public class AGR_SaveConfirmationVM : BaseViewModel
    {
        private readonly ILogger<AGR_SaveConfirmationVM>? _logger;
        private readonly IAGR_BaseComponent _component;
        private bool? _dialogResult;

        public AGR_SaveConfirmationVM(IAGR_BaseComponent component, ILogger<AGR_SaveConfirmationVM>? logger = null)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _logger = logger;
            
            InitializeProperties();
        }

        #region Properties

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

        #region Material and Color (for parts)
        private IAGR_Material _baseMaterial;
        public IAGR_Material BaseMaterial
        {
            get => _baseMaterial;
            set
            {
                if (Set(ref _baseMaterial, value))
                {
                    OnPropertyChanged(nameof(MaterialName));
                    OnPropertyChanged(nameof(HasErrors));
                    ((RelayCommand)SaveCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private IAGR_Material _paint;
        public IAGR_Material Paint
        {
            get => _paint;
            set
            {
                if (Set(ref _paint, value))
                {
                    OnPropertyChanged(nameof(ColorName));
                    OnPropertyChanged(nameof(HasErrors));
                    ((RelayCommand)SaveCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public string MaterialName => BaseMaterial?.Name ?? string.Empty;
        public string ColorName => Paint?.Name ?? string.Empty;
        #endregion

        #region Article (for purchased parts)
        public string Article
        {
            get => _component.Article;
            set
            {
                _component.Article = value;
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(HasErrors));
                ((RelayCommand)SaveCommand).NotifyCanExecuteChanged();
            }
        }
        #endregion

        #region Properties Collection (for blank properties)
        private ObservableCollection<PropertyDisplayItem> _blankProperties;
        public ObservableCollection<PropertyDisplayItem> BlankProperties
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
            set => Set(ref _specificationItems, value);
        }
        #endregion

        #region Assembly Properties
        private string _assemblyColor;
        public string AssemblyColor
        {
            get => _assemblyColor;
            set => Set(ref _assemblyColor, value);
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
            _dialogResult = true;
            var view = p as Window;
            view?.DialogResult = true;
            view?.Close();
        }
        #endregion

        #region CancelCommand
        private ICommand _CancelCommand;
        public ICommand CancelCommand => _CancelCommand
            ??= new RelayCommand(OnCancelExecuted);
        private void OnCancelExecuted(object p)
        {
            _dialogResult = false;
            var view = p as Window;
            view?.DialogResult = false;
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
                
                // Load blank properties based on component type
                LoadBlankProperties(part.PropertiesCollection);
            }
            else if (_component is AGR_AssemblyComponentVM assembly)
            {
                // Get assembly paint/color
                if (assembly is IAGR_HasPaint hasPaint)
                {
                    Paint = hasPaint.Paint;
                    AssemblyColor = Paint?.Name ?? string.Empty;
                }

                // Load specification items
                LoadSpecificationItems(assembly);
            }

            ValidateComponent();
        }

        private void LoadBlankProperties(IAGR_PropertiesCollection propertiesCollection)
        {
            BlankProperties = new ObservableCollection<PropertyDisplayItem>();
            
            if (propertiesCollection == null) return;

            foreach (var prop in propertiesCollection.Properties)
            {
                BlankProperties.Add(new PropertyDisplayItem
                {
                    Name = prop.Name,
                    Value = prop.Value?.ToString() ?? string.Empty,
                    Unit = GetUnitForProperty(prop.Name)
                });
            }
        }

        private void LoadSpecificationItems(AGR_AssemblyComponentVM assembly)
        {
            SpecificationItems = new ObservableCollection<AGR_SpecificationItemVM>();
            
            var items = assembly.GetChildComponents();
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

            if (IsProduced)
            {
                if (string.IsNullOrEmpty(MaterialName))
                {
                    ErrorMessages.Add("Не указан материал");
                }

                if (IsPart && string.IsNullOrEmpty(ColorName))
                {
                    ErrorMessages.Add("Не указан цвет/покрытие");
                }
            }

            if (IsPurchased)
            {
                if (string.IsNullOrEmpty(Article))
                {
                    ErrorMessages.Add("Не указан артикул для покупной детали");
                }
            }

            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(ErrorMessages));
            ((RelayCommand)SaveCommand).NotifyCanExecuteChanged();
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

        private async Task OpenMaterialSelectionAsync()
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора материала");

                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SelectedAvaType = "Материал";

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;
                
                var result = selectView.ShowDialog();

                if (result == true && selectVm.SelectedArticle != null)
                {
                    BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                    _logger?.LogInformation("Выбран материал {Material}", BaseMaterial.Name);
                    ValidateComponent();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе материала");
            }
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
                
                var result = selectView.ShowDialog();

                if (result == true && selectVm.SelectedArticle != null)
                {
                    Paint = new AGR_Material(selectVm.SelectedArticle);
                    if (IsAssembly)
                    {
                        AssemblyColor = Paint.Name;
                    }
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

        #region Nested Classes

        public class PropertyDisplayItem : BaseViewModel
        {
            private string _name;
            public string Name
            {
                get => _name;
                set => Set(ref _name, value);
            }

            private string _value;
            public string Value
            {
                get => _value;
                set => Set(ref _value, value);
            }

            private string _unit;
            public string Unit
            {
                get => _unit;
                set => Set(ref _unit, value);
            }
        }

        #endregion
    }
}
