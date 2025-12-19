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

namespace Agrovent.ViewModels.Components
{
    public class AGR_AssemblyComponentVM : AGR_FileComponent, IAGR_Assembly
    {
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

        #region METHODS
        public IEnumerable<IAGR_SpecificationItem> GetChildComponents()
        {
            // Получаем компоненты верхнего уровня
            var topComponents = (mDocument as ISwAssembly).Configurations.Active.Components.AGR_ActiveComponents().AGR_BaseComponents();
            // Группируем и создаем SpecificationItemVM для верхнего уровня
            var groupedTop = topComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new AGR_SpecificationItemVM(g.First(), g.Count()));
            AGR_TopComponents = new ObservableCollection<AGR_SpecificationItemVM>(groupedTop);
            return AGR_TopComponents;
        }
        public async Task SaveToDatabaseAsync()
        {
            var versionService = AGR_ServiceContainer.GetService<IAGR_ComponentVersionService>();
            await versionService.CheckAndSaveComponentAsync(this);
        }
        public IEnumerable<IAGR_SpecificationItem> GetFlatComponents()
        {
            // Получаем все компоненты (плоский список)
            var flatComponents = (mDocument as ISwAssembly).Configurations.Active.Components.AGR_TryFlatten().AGR_BaseComponents();
            // Группируем и создаем SpecificationItemVM для плоского списка
            var groupedFlat = flatComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new AGR_SpecificationItemVM(g.First(), g.Count()));
            return groupedFlat;
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

        #endregion

        #region CTOR
        public AGR_AssemblyComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            var assem = swDocument3D as ISwAssembly;




        } 
        #endregion
    }
}