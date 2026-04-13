using System.Collections.ObjectModel;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.ViewModels.Base;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.ViewModels.Specification;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.Services;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Properties;
using Agrovent.Infrastructure.Interfaces.Properties;
using System.Diagnostics;
using System.Windows.Input;
using Agrovent.Infrastructure.Commands;
using System.IO;
using Xarial.XCad.UI;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Drawing;
using Xarial.XCad.SolidWorks;
using System.Windows.Media.Imaging;
using Agrovent.DAL;
using Agrovent.ViewModels.Windows;
using Agrovent.Views.Windows;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_AssemblyComponentVM : AGR_FileComponent, IAGR_Assembly, IAGR_HasPaint
    {

        private readonly ILogger<AGR_PartComponentVM> _logger; // Добавляем логгер

        #region Property - SelectedItem
        private IAGR_BaseComponent _SelectedItem;
        public IAGR_BaseComponent SelectedItem
        {
            get => _SelectedItem;
            set => Set(ref _SelectedItem, value);
        }
        #endregion

        #region Property - ObservableCollection<AGR_SpecificationItemVM> _AGR_TopComponents
        private ObservableCollection<AGR_SpecificationItemVM> _AGR_TopComponents;
        public ObservableCollection<AGR_SpecificationItemVM> AGR_TopComponents
        {
            get => _AGR_TopComponents;
            set => Set(ref _AGR_TopComponents, value);
        }
        #endregion

        // Реализация IAGR_HasPaint
        private IAGR_Material? _paint;
        public IAGR_Material? Paint
        {
            get => _paint;
            set => Set(ref _paint, value);
        }

        #region PaintCount
        private decimal? _PaintCount;
        public decimal? PaintCount { get => _PaintCount; set => _PaintCount = value; }
        #endregion

        #region METHODS
        public IEnumerable<IAGR_SpecificationItem> GetChildComponents()
        {
            // Получаем компоненты верхнего уровня
            //var topComponents = (mDocument as ISwAssembly).Configurations.Active.Components.AGR_ActiveComponents().AGR_BaseComponents();

            var topComponents = (mDocument as ISwAssembly).Configurations.Active.Components.AGR_BaseComponents(true);
            // Группируем и создаем SpecificationItemVM для верхнего уровня
            var groupedTop = topComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new AGR_SpecificationItemVM(g.First(), g.Count()));
            AGR_TopComponents = new ObservableCollection<AGR_SpecificationItemVM>(groupedTop);
            return AGR_TopComponents;
        }
        public IEnumerable<IAGR_SpecificationItem> GetFlatComponents()
        {

            // Получаем все компоненты (плоский список)
            //var flatComponents = (mDocument as ISwAssembly).Configurations.Active.Components.AGR_TryFlatten().AGR_BaseComponents();
            var flatComponents = (mDocument as ISwAssembly).Configurations.Active.Components.TryFlatten().AGR_BaseComponents(true);
            // Группируем и создаем SpecificationItemVM для плоского списка
            var groupedFlat = flatComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new AGR_SpecificationItemVM(g.First(), g.Count()));
            return groupedFlat;
        }

        public void Refresh()
        {
            GetChildComponents();
        }

        #endregion

        #region COMMANDS

        #region OpenFoldefCommand
        private ICommand _OpenFolderCommand;
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
                selectVm.SearchText = "Краска порошковая ";

                // Создаем View и устанавливаем DataContext
                var selectView = new AGR_SelectAvaArticleView { DataContext = selectVm };


                selectView.ShowActivated = true;
                // Открываем окно модально
                selectView.ShowDialog();

                // Если окно закрыто с результатом OK и элемент выбран
                if (selectVm.IsDialogResultAccepted == true && selectVm.SelectedArticle != null)
                {
                    // Присваиваем выбранный AvaArticleModel в BaseMaterial.AvaModel
                    Paint = new AGR_Material(selectVm.SelectedArticle);
                    mProperties.FirstOrDefault(p => p.Name == AGR_PropertyNames.Color).Value = Paint.Name;
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

        #region CTOR
        public AGR_AssemblyComponentVM(ISwDocument3D doc3D) : base(doc3D)
        {
            Paint = new AGR_Paint(doc3D); // Предполагаем, что AGR_Paint может быть создан так
            PaintCount = 0; // Инициализация
        } 
        #endregion
    }
}