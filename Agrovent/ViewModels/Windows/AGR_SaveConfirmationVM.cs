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
        private void InitializeProperties()
        {
            ErrorMessages = string.Empty;
            Preview = _component.Preview;

            var part = _component as AGR_PartComponentVM;

            BaseMaterial = part.BaseMaterial;
            Paint = part.Paint;
            AvaArticle = part.AvaArticle;
            BaseAvaTypeEnum = part.AvaType;
            BaseAvaType = part.AvaType.ToString();

            // Если цвет не установлен, по умолчанию ставим "без покрытия"
            NoPaint = Paint == null;

            // Load blank properties based on component type
            LoadBlankProperties(part.PropertiesCollection);
            ValidateComponent();
        }

        #region Properties
        public bool? DialogResult { get; set; }

        #region Component Info
        public string ComponentName => _component.Name;
        public string PartNumber => _component.PartNumber;
        public string ConfigName => _component.ConfigName;
        public AGR_ComponentType_e ComponentType => _component.ComponentType;

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
                Set(ref _noPaint, value);
                if (value == true)
                {
                    Paint = null;
                }
                OnPropertyChanged(nameof(HasErrors));
                ValidateComponent();
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
                if (value == true)
                {
                    AvaArticle = null;
                }
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(HasErrors));
                ValidateComponent();
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

        #region Error Messages
        private string _errorMessages;
        public string ErrorMessages
        {
            get => _errorMessages;
            set => Set(ref _errorMessages, value);
        }

        #region HasErrors
        private bool _hasErrors;
        public bool HasErrors
        {
            get => _hasErrors;
            set => Set(ref _hasErrors, value);
        }
        #endregion

        #endregion

        #region IsPart / IsSheetMetal / IsPurchased
        public bool IsPart => ComponentType == AGR_ComponentType_e.Part ||
                              ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsSheetMetal => ComponentType == AGR_ComponentType_e.SheetMetallPart;
        public bool IsPurchased => ComponentType == AGR_ComponentType_e.Purchased 
                                || BaseAvaTypeEnum == AGR_AvaType_e.DontBuy;
        #endregion

        public string PropertiesIsVisible => IsPurchased ? "Collapsed" : "Visible";

        #region Тип ава
        private string _baseAvaType;
        public string BaseAvaType
        {
            get => _baseAvaType;
            set => Set(ref _baseAvaType, value);
        }

        private AGR_AvaType_e _baseAvaTypeEnum;
        public AGR_AvaType_e BaseAvaTypeEnum
        {
            get => _baseAvaTypeEnum;
            set
            {
                if (Set(ref _baseAvaTypeEnum, value))
                {
                    _component.AvaType = value;
                    OnPropertyChanged(nameof(BaseAvaType));
                    OnPropertyChanged(nameof(PropertiesIsVisible));
                    ValidateComponent();
                }
            }
        }
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
        private bool CanSelectMaterialExecute(object p) => IsPart;
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
        private bool CanSelectColorExecute(object p) => IsPart;
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
                selectVm.SearchText = _component.Name;

                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };
                selectView.ShowActivated = true;
                selectView.ShowDialog();

                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    AvaArticle = selectVm.SelectedArticle;
                }
                ValidateComponent();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе артикула для главной сборки.");
            }
        }

        #endregion

        #endregion

        #region Private Methods



        private void LoadBlankProperties(IAGR_PropertiesCollection propertiesCollection)
        {
            BlankProperties = new ObservableCollection<IXProperty>();

            if (propertiesCollection == null) return;

            foreach (var prop in propertiesCollection.Properties)
            {
                BlankProperties.Add(prop);
            }
        }

        private void ValidateComponent()
        {
            ErrorMessages = string.Empty;
            var errorList = new List<string>();

            if (!IsPurchased)
            {

                if (string.IsNullOrEmpty(MaterialName) || string.IsNullOrWhiteSpace(MaterialName))
                {
                    errorList.Add(AGR_SaveConfirmationErrors.NoMaterial);
                }
                if (NoPaint != true && (string.IsNullOrEmpty(ColorName) || string.IsNullOrWhiteSpace(ColorName)))
                {
                    errorList.Add(AGR_SaveConfirmationErrors.NoColor);
                }
                if (NoPaint != true && (string.IsNullOrEmpty(Article) || string.IsNullOrWhiteSpace(Article)))
                {
                    errorList.Add(AGR_SaveConfirmationErrors.NoArticle);
                }
            }

            if (IsPurchased)
            {
                if (string.IsNullOrEmpty(Article) || string.IsNullOrWhiteSpace(Article))
                {
                    errorList.Add(AGR_SaveConfirmationErrors.NoArticle);
                }
            }

            if (errorList.Any())
            {
                ErrorMessages = string.Join("\n", errorList);
                HasErrors = true;
            }
            else
            {
                HasErrors = false;
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
