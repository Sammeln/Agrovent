// File: ViewModels/MainWindowViewModel.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AGR_PropManager.ViewModels.Base;
using System.Windows.Input;
using AGR_PropManager.Infrastructure.Commands;
using Agrovent.DAL;
using System.Windows.Media.Imaging;
using Agrovent.Infrastructure.Enums;
using AGR_PropManager.Views;
using Agrovent.DAL.Entities.TechProcess;
using AGR_PropManager.ViewModels.Components;
using AGR_PropManager.ViewModels.TechProcess;
using Agrovent.DAL.Services.Repositories;
using System.Windows;
using System.Windows.Controls;

namespace AGR_PropManager.ViewModels.Windows
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly DataContext _dataContext;
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger _logger;

        #region CTOR

        public MainWindowViewModel(
            DataContext dataContext,
            ILogger<MainWindowViewModel>? logger,
            UnitOfWork unitOfWork)
        {
            _dataContext = dataContext;
            _logger = logger;
            _unitOfWork = unitOfWork;
            
            // Инициализируем CollectionViewSource для ClassifierItems
            ClassifierItems_CVS.Source = ClassifierItems;
            ApplyFilter();
        }
        #endregion

        #region Commands

        #region LoadClassifierDataCommand
        private ICommand _LoadClassifierDataCommand;
        public ICommand LoadClassifierDataCommand => _LoadClassifierDataCommand
            ??= new RelayCommand(async (_) => await LoadClassifierDataAsync(), _ => true);
        #endregion

        #region OpenItemTechProcessEditorCommand
        private ICommand _OpenItemTechProcessEditorCommand;
        public ICommand OpenItemTechProcessEditorCommand => _OpenItemTechProcessEditorCommand
            ??= new RelayCommand<ClassifierItemViewModel>(OnOpenItemTechProcessEditorCommandExecuted, CanOpenItemTechProcessEditorCommandExecute);
        private bool CanOpenItemTechProcessEditorCommandExecute(ClassifierItemViewModel? p) => p != null;
        private void OnOpenItemTechProcessEditorCommandExecuted(ClassifierItemViewModel? classifierItem)
        {
            if (classifierItem == null) return;
            
            _logger.LogInformation($"Открытие редактора процесса для компонента {classifierItem.PartNumber}.");
            
            // Создаем компонент из ClassifierItem для передачи в редактор
            var component = new ComponentItemViewModel(_dataContext, _unitOfWork)
            {
                Id = classifierItem.Id,
                PartNumber = classifierItem.PartNumber,
                Name = classifierItem.Name,
                PreviewImage = classifierItem.PreviewImage
            };
            
            var selectedComponents = new ObservableCollection<ComponentItemViewModel> { component };
            var editorViewModel = new TechProcessEditorViewModel(
                selectedComponents,
                _dataContext,
                _logger,
                _unitOfWork);
            
            var editorWindow = new TechProcessEditorWindow(editorViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            editorWindow.ShowDialog();
        }
        #endregion

        #endregion

        #region PROPS

        #region SearchText
        private string _SearchText = "";
        public string SearchText
        {
            get => _SearchText;
            set
            {
                if (Set(ref _SearchText, value))
                {
                    ApplyFilter();
                }
            }
        }
        #endregion

        #region Коллекция ClassifierItems
        private ObservableCollection<ClassifierItemViewModel> _ClassifierItems = new ObservableCollection<ClassifierItemViewModel>();
        public ObservableCollection<ClassifierItemViewModel> ClassifierItems
        {
            get => _ClassifierItems;
            set => Set(ref _ClassifierItems, value);
        }
        #endregion

        #region CollectionViewSource для ClassifierItems
        private CollectionViewSource ClassifierItems_CVS = new CollectionViewSource();
        public ICollectionView ClassifierItemsView => ClassifierItems_CVS?.View;
        #endregion

        #endregion

        #region Methods

        #region Метод загрузки данных классификатора
        public async Task LoadClassifierDataAsync()
        {
            try
            {
                _logger.LogInformation("Загрузка данных классификатора...");
                // Группируем по PartNumber и берем последнюю версию для каждого
                var latestVersions = await _unitOfWork.ComponentRepository.GetAllLatestComponentVersionsAsync();

                // Очищаем коллекцию перед загрузкой, чтобы избежать дублирования
                ClassifierItems?.Clear();

                if (ClassifierItems != null)
                {
                    foreach (var cv in latestVersions)
                    {
                        var item = new ClassifierItemViewModel
                        {
                            Id = cv.Id,
                            PartNumber = cv.Component.PartNumber,
                            Name = cv.Name,
                            SavedDate = cv.CreatedAt,
                            PreviewImage = cv.PreviewImage != null ? LoadImageFromBytes(cv.PreviewImage) : null
                        };
                        ClassifierItems.Add(item);
                    }
                }

                ClassifierItems_CVS.View.Refresh();
                _logger.LogInformation($"Загружено {ClassifierItems.Count} записей классификатора.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных классификатора");
            }
        }
        #endregion

        #region Применение фильтра
        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                ClassifierItems_CVS.Filter = null;
            }
            else
            {
                string searchTextLower = SearchText.ToLower();
                ClassifierItems_CVS.Filter = item =>
                {
                    if (item is not ClassifierItemViewModel classifierItem) return false;
                    
                    bool matchesPartNumber = !string.IsNullOrEmpty(classifierItem.PartNumber) 
                        && classifierItem.PartNumber.ToLower().Contains(searchTextLower);
                    bool matchesName = !string.IsNullOrEmpty(classifierItem.Name) 
                        && classifierItem.Name.ToLower().Contains(searchTextLower);
                    
                    return matchesPartNumber || matchesName;
                };
            }
            ClassifierItems_CVS.View.Refresh();
        }
        #endregion

        #region Вспомогательный метод для загрузки изображения
        private BitmapImage? LoadImageFromBytes(byte[] imageData)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(imageData);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze(); // Оптимизация для UI Thread
                return image;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #endregion
    }
}
