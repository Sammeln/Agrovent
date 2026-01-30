using Agrovent.Infrastructure.Commands;
using System.Diagnostics;
using System.Windows.Input;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;
using System.IO;
using Xarial.XCad.Documents;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Agrovent.DAL;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Agrovent.Infrastructure.Extensions;

namespace Agrovent.ViewModels.Components
{

    public class AGR_PartComponentVM : AGR_FileComponent, IAGR_HasMaterial, IAGR_HasPaint
    {
        private readonly ILogger<AGR_PartComponentVM> _logger; // Добавляем логгер


        #region CTOR
        public AGR_PartComponentVM(ISwDocument3D doc3D, ILogger<AGR_PartComponentVM> logger = null) : base(doc3D)
        {
            _logger = logger;

            // Инициализация свойств
            BaseMaterial = new AGR_Material(doc3D); // Предполагаем, что AGR_Material может быть создан так
            BaseMaterialCount = 0; // Инициализация

            Paint = new AGR_Paint(doc3D); // Предполагаем, что AGR_Paint может быть создан так
            PaintCount = 0; // Инициализация
        }
        #endregion

        #region PROPS

        // Реализация IAGR_HasMaterial
        private IAGR_Material _baseMaterial;
        public IAGR_Material BaseMaterial
        {
            get => _baseMaterial;
            set
            {
                Set(ref _baseMaterial, value);
                mProperties.AGR_TryGetProp(AGR_PropertyNames.Material).Value = value.Name;
            }
        }

        private decimal _baseMaterialCount;
        public decimal BaseMaterialCount
        {
            get => _baseMaterialCount;
            set => Set(ref _baseMaterialCount, value);
        }

        // Реализация IAGR_HasPaint
        private IAGR_Material? _paint;
        public IAGR_Material? Paint
        {
            get => _paint;
            set => Set(ref _paint, value);
        }
        public decimal? PaintCount { get => paintCount; set => paintCount = value; }
        #endregion

        #region METHODS
        /// <summary>
        /// Асинхронно обновляет свойства компонента, включая вычисление BaseMaterialCount.
        /// </summary>
        public async Task UpdatePropertiesAsync()
        {
            //await base.UpdatePropertiesAsync(); // Вызов базового метода для обновления основных свойств

            // Вычисление BaseMaterialCount
            await CalculateAndUpdateMaterialCountAsync();
        }

        /// <summary>
        /// Вычисляет и обновляет BaseMaterialCount на основе BaseMaterial.AvaModel.UOM и свойств компонента.
        /// </summary>
        private async Task CalculateAndUpdateMaterialCountAsync()
        {
            try
            {
                var calculatedCount = await CalculateMaterialCountAsync(BaseMaterial);
                if (calculatedCount.HasValue)
                {
                    BaseMaterialCount = calculatedCount.Value;
                    _logger?.LogInformation($"Calculated BaseMaterialCount for {PartNumber}: {BaseMaterialCount} based on UOM '{BaseMaterial?.AvaModel?.MainUOM}' and properties.");
                }
                else
                {
                    // Если CalculateMaterialCountAsync возвращает null (например, для "шт"), уведомляем пользователя
                    // В реальном приложении лучше использовать механизм уведомлений (например, через Messenger или INotifyPropertyChanged для специального свойства).
                    // MessageBox.Show("Для единицы измерения 'Штука' значение BaseMaterialCount необходимо указать вручную.", "Уведомление", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger?.LogWarning($"BaseMaterialCount requires manual input for UOM 'Штука'/'шт' for component {PartNumber}.");
                    // Можно установить BaseMaterialCount в 0 или null, если не предусмотрено ручного ввода
                    BaseMaterialCount = 0; // Или null, в зависимости от требований
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error calculating BaseMaterialCount for component {PartNumber}.");
                // В реальном приложении может потребоваться обработка ошибки (например, установка BaseMaterialCount в 0 или null)
                BaseMaterialCount = 0; // Или null, в зависимости от требований
            }
        }

        /// <summary>
        /// Вычисляет количество материала на основе его UOM и свойств компонента.
        /// </summary>
        /// <param name="material">Материал, для которого нужно вычислить количество.</param>
        /// <returns>Количество материала или null, если требуется ручной ввод (например, для штук).</returns>
        private async Task<decimal?> CalculateMaterialCountAsync(IAGR_Material? material)
        {
            if (material == null || string.IsNullOrEmpty(material.AvaModel?.MainUOM) || PropertiesCollection == null)
            {
                _logger?.LogWarning("Cannot calculate material count: material, UOM, or PropertiesCollection is null.");
                return null;
            }

            var uom = material.AvaModel.MainUOM.ToLowerInvariant().Trim();

            // Определяем, какое свойство использовать в зависимости от UOM
            IXProperty? sourceProperty = null;
            bool requiresManualInput = false;

            switch (uom)
            {
                case "грамм":
                case "гр":
                    sourceProperty = GetPropertyByName(AGR_PropertyNames.BlankMass); // Предполагаем, что Mass хранится как BlankMass
                    if (sourceProperty != null)
                    {
                        if (decimal.TryParse(sourceProperty.Value?.ToString(), out decimal massGrams))
                        {
                            return Math.Round(massGrams / 1000.0m, 3); // Округляем до 3 знаков после запятой
                        }
                        else
                        {
                            _logger?.LogWarning($"Could not parse mass value '{sourceProperty.Value}' for component {PartNumber}.");
                            return null;
                        }
                    }
                    break;
                case "килограмм":
                case "кг":
                    sourceProperty = GetPropertyByName(AGR_PropertyNames.BlankMass); // Mass уже в кг
                    if (sourceProperty != null)
                    {
                        if (decimal.TryParse(sourceProperty.Value?.ToString(), out decimal massKg))
                        {
                            return Math.Round(massKg, 3); // Округляем до 3 знаков после запятой
                        }
                        else
                        {
                            _logger?.LogWarning($"Could not parse mass value '{sourceProperty.Value}' for component {PartNumber}.");
                            return null;
                        }
                    }
                    break;
                case "метр":
                case "погонный метр":
                case "пог. м":
                case "м":
                    // Используем Length, если доступно, иначе SheetMetall_Length
                    sourceProperty = GetPropertyByName(AGR_PropertyNames.BlankLen) // Для обычных деталей
                                    ?? (PropertiesCollection as AGR_SheetPartPropertiesCollection)?.SheetMetall_Length; // Для листовых
                    if (sourceProperty != null)
                    {
                        if (decimal.TryParse(sourceProperty.Value?.ToString(), out decimal lengthMm))
                        {
                            // Предполагаем, что длина в мм (например, BlankLen), переводим в м
                            return Math.Round(lengthMm / 1000.0m, 3); // Округляем до 3 знаков после запятой
                        }
                        else
                        {
                            _logger?.LogWarning($"Could not parse length value '{sourceProperty.Value}' for component {PartNumber}.");
                            return null;
                        }
                    }
                    break;
                case "м2":
                case "кв.метр":
                case "кв. метр":
                    // Используем SurfaceArea, если доступно, иначе SheetMetall_SurfaceArea
                    sourceProperty = GetPropertyByName(AGR_PropertyNames.BlankArea) // Для обычных деталей (если такое свойство есть)
                                    ?? (PropertiesCollection as AGR_SheetPartPropertiesCollection)?.SheetMetall_SurfaceArea; // Для листовых (предполагаем интерфейс IAGR_SheetPartPropertiesCollection)
                    if (sourceProperty != null)
                    {
                        if (decimal.TryParse(sourceProperty.Value?.ToString(), out decimal areaMm2))
                        //if (decimal.TryParse(sourceProperty.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal areaMm2))
                        {
                            return Math.Round(areaMm2, 3); // Округляем до 3 знаков после запятой
                        }
                        else
                        {
                            _logger?.LogWarning($"Could not parse surface area value '{sourceProperty.Value}' for component {PartNumber}.");
                            return null;
                        }
                    }
                    break;
                case "штука":
                case "шт":
                    requiresManualInput = true;
                    break;
                default:
                    _logger?.LogWarning($"Unknown UOM '{material.AvaModel.MainUOM}' for component {PartNumber}. Cannot calculate material count automatically.");
                    return null; // Или 0, в зависимости от требований
            }

            if (requiresManualInput)
            {
                // Возвращаем null, чтобы вызывающая сторона знала, что нужно ввести вручную
                return null;
            }

            if (sourceProperty == null)
            {
                _logger?.LogWarning($"Required property for UOM '{material.AvaModel.MainUOM}' not found in PropertiesCollection for component {PartNumber}.");
                return null;
            }

            // Этот код не должен сработать, если switch выше корректно обрабатывает все случаи
            _logger?.LogWarning($"Unexpected state while calculating material count for component {PartNumber}.");
            return null;
        }

        /// <summary>
        /// Вспомогательный метод для поиска свойства в коллекции по имени (с игнорированием регистра).
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Найденное свойство или null.</returns>
        private IXProperty? GetPropertyByName(string propertyName)
        {
            if (PropertiesCollection?.Properties == null) return null;

            return PropertiesCollection.Properties
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        } 
        #endregion

        #region COMMANDS

        #region OpenFoldefCommand
        private ICommand _OpenFolderCommand;
        private decimal? paintCount;

        public ICommand OpenFolderCommand => _OpenFolderCommand
            ??= new RelayCommand(OnOpenFolderCommandExecuted, CanOpenFolderCommandExecute);
        private bool CanOpenFolderCommandExecute(object p) => true;
        private void OnOpenFolderCommandExecuted(object p)
        {
            var filePath = p.ToString();
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var folder = Path.GetDirectoryName(filePath);
                if (folder != null)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
            }
        }
        #endregion

        #region SelectBaseMaterialCommand
        private ICommand _SelectBaseMaterialCommand;
        public ICommand SelectBaseMaterialCommand => _SelectBaseMaterialCommand
            ??= new RelayCommand(OnSelectBaseMaterialCommandExecuted, CanSelectBaseMaterialCommandExecute);
        private bool CanSelectBaseMaterialCommandExecute(object p) => true;
        private void OnSelectBaseMaterialCommandExecuted(object p)
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора AvaArticle для компонента {PartNumber}", PartNumber);

                // Получаем IServiceProvider из вашего контейнера (предполагаем, что он доступен)
                // Это может быть AGR_ServiceContainer или другой способ получения провайдера.
                // Пример (может отличаться в вашем проекте):

                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);


                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };

                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                    BaseMaterial = new AGR_Material(selectVm.SelectedArticle);
                    _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, PartNumber);

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion

        #region SelectPaintCommand
        private ICommand _SelectPaintCommand;
        public ICommand SelectPaintCommand => _SelectPaintCommand
            ??= new RelayCommand(OnSelectPaintCommandExecuted, CanSelectPaintCommandExecute);
        private bool CanSelectPaintCommandExecute(object p) => true;
        private void OnSelectPaintCommandExecuted(object p)
        {
            try
            {
                _logger?.LogDebug("Открытие окна выбора AvaArticle для компонента {PartNumber}", PartNumber);

                // Получаем IServiceProvider из вашего контейнера (предполагаем, что он доступен)
                // Это может быть AGR_ServiceContainer или другой способ получения провайдера.
                // Пример (может отличаться в вашем проекте):

                // Получаем нужные сервисы для VM
                var dataContext = AGR_ServiceContainer.GetService<DataContext>();
                var logger = AGR_ServiceContainer.GetService<ILogger<AGR_SelectAvaArticleVM>>();

                // Создаем ViewModel
                var selectVm = new AGR_SelectAvaArticleVM(dataContext, logger);
                selectVm.SearchText = "Краска порошковая";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };


                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
//                    Paint = new AGR_Material(selectVm.SelectedArticle);
                    _logger?.LogInformation("Выбран AvaArticle {Article} для компонента {PartNumber}", selectVm.SelectedArticle.Article, PartNumber);

                    // Обновляем свойства, если это влияет на них (например, BaseMaterialCount)
                    //Task.Run(async () => await UpdatePropertiesAsync()).ConfigureAwait(false); // Вызов асинхронного метода
                }
                else
                {
                    _logger?.LogDebug("Окно выбора AvaArticle закрыто без выбора.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при открытии окна выбора AvaArticle для компонента {PartNumber}", PartNumber);
            }
        }
        #endregion 

        #endregion

        public void Refresh()
        {
            OnPropertyChanged(nameof(CurrentModelFilePath));
            OnPropertyChanged(nameof(CurrentDrawFilePath));
            OnPropertyChanged(nameof(StorageModelFilePath));
            OnPropertyChanged(nameof(StorageDrawFilePath));
            OnPropertyChanged(nameof(ProductionModelFilePath));
            OnPropertyChanged(nameof(ProductionDrawFilePath));

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ConfigName));
            OnPropertyChanged(nameof(PartNumber));
            OnPropertyChanged(nameof(Article));
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(Version));
            OnPropertyChanged(nameof(HashSum));
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(ComponentType));
            OnPropertyChanged(nameof(AvaType));

            switch (ComponentType)
            {
                case AGR_ComponentType_e.Part:
                    PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.SheetMetallPart:
                    PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.Purchased:
                    PropertiesCollection?.Properties.Clear();
                    break;
                case AGR_ComponentType_e.NA:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                default:
                    break;
            }

            PropertiesCollection.UpdateProperties();
            OnPropertyChanged(nameof(PropertiesCollection));
        }


    }

}
