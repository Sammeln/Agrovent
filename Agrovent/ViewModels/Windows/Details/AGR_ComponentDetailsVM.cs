// File: ViewModels/Windows/Details/AGR_ComponentDetailsVM.cs
using Agrovent.DAL;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components; // Для AGR_ComponentRegistryItemVM
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace Agrovent.ViewModels.Windows.Details
{
    public class AGR_ComponentDetailsVM : BaseViewModel
    {
        private readonly AGR_ComponentRegistryItemVM _registryItem;
        private readonly IUnitOfWork _unitOfWork;

        public AGR_ComponentDetailsVM(AGR_ComponentRegistryItemVM registryItem, IUnitOfWork unitOfWork)
        {
            _registryItem = registryItem ?? throw new ArgumentNullException(nameof(registryItem));
            _unitOfWork = unitOfWork;


            // Заполняем свойства из _registryItem
            Name = _registryItem.Name;
            PartNumber = _registryItem.PartNumber;
            CreatedAt = _registryItem.CreatedAt;
            ComponentTypeDisplay = _registryItem.ComponentTypeDisplay;
            AvaTypeDisplay = _registryItem.AvaTypeDisplay;
            Version = _registryItem.Version;
            StoragePath = _registryItem.StoragePath;
            Preview = _registryItem.Preview; // BitmapImage

            // Загрузка дополнительных данных (материал, покраска, техпроцесс) из _registryItem.ComponentVersion
            LoadAdditionalDetails();
        }

        public string Name { get; }
        public string PartNumber { get; }
        public DateTime CreatedAt { get; }
        public string ComponentTypeDisplay { get; }
        public string AvaTypeDisplay { get; }
        public int Version { get; }
        public string StoragePath { get; }
        public BitmapImage? Preview { get; }

        // Свойства для дополнительных данных
        public string MaterialName { get; private set; } = "N/A";
        public string MaterialUOM { get; private set; } = "N/A";
        public string MaterialArticle { get; private set; } = "N/A";
        public string PaintName { get; private set; } = "N/A";
        public string PaintArticle { get; private set; } = "N/A";
        public ObservableCollection<string> TechProcessSteps { get; } = new();

        private async void LoadAdditionalDetails()
        {
            // Предположим, что _registryItem.ComponentVersion содержит все необходимые данные
            var compVer = await _unitOfWork.ComponentRepository.GetLatestComponentVersion(PartNumber); // Предполагаем, что это свойство есть в AGR_ComponentRegistryItemVM

            if (compVer != null)
            {
                // Материал
                if (compVer.Material != null)
                {
                    MaterialName = compVer.Material.BaseMaterial ?? "N/A";
                    // Предполагаем, что UOM хранится в MaterialModel
                    //MaterialUOM = compVer.Material.UOM ?? "N/A"; !!!
                    // Article из связанного AvaArticle
                    //MaterialArticle = compVer.Material.AvaArticle?.Article?.ToString() ?? "N/A"; !!!
                }

                // Покраска (предполагаем, что это AvaArticleModel, связанное с ComponentVersion или Material)
                // Здесь нужно смотреть структуру ваших сущностей.
                // Например, если покраска хранится как AvaArticleModel в ComponentVersion.Paint (гипотетически)
                // var paintArticle = compVer.Paint?.AvaArticle; // Замените на реальное свойство
                // if (paintArticle != null)
                // {
                //     PaintName = paintArticle.Name;
                //     PaintArticle = paintArticle.Article?.ToString();
                // }

                //Техпроцесс(предполагаем, что есть связь с TechnologicalProcess)
                 var techProcess = compVer.Component.TechnologicalProcess; // Замените на реальное свойство
                if (techProcess?.Operations != null)
                {
                    foreach (var op in techProcess.Operations.OrderBy(o => o.SequenceNumber))
                    {
                        TechProcessSteps.Add($"{op.Name} ({op.CostPerHour} мин)");
                    }
                    OnPropertyChanged(nameof(TechProcessSteps));
                }
            }
        }
    }
}