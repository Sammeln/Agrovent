// ComponentViewModelFactory.cs (если нужно создавать ViewModel с зависимостями)
using System.Collections.ObjectModel;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Services;
using Agrovent.ViewModels.Components;
using Agrovent.ViewModels.Specification;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public interface IAGR_ComponentViewModelFactory
    {
        AGR_AssemblyComponentVM CreateAssemblyComponent(ISwDocument3D document);
        Task<AGR_AssemblyComponentVM> CreateAssemblyComponentAsync(ISwDocument3D document);
        Task<IAGR_BaseComponent> CreateComponentAsync(ISwDocument3D document);
        AGR_PartComponentVM CreatePartComponent(ISwDocument3D document);
        Task<AGR_PartComponentVM> CreatePartComponentAsync(ISwDocument3D document);
    }

    public class AGR_ComponentViewModelFactory : IAGR_ComponentViewModelFactory
    {
        private readonly IAGR_ComponentViewModelCache _cache;
        private readonly ILogger<AGR_ComponentViewModelFactory> _logger;

        public AGR_ComponentViewModelFactory(
            IAGR_ComponentViewModelCache cache,
            ILogger<AGR_ComponentViewModelFactory> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<AGR_AssemblyComponentVM> CreateAssemblyComponentAsync(ISwDocument3D document)
        {
            _logger.LogDebug($"Creating assembly component async: {document.Title}");

            try
            {
                // Используем кэш
                var viewModel = await _cache.GetOrCreateViewModelAsync(document) as AGR_AssemblyComponentVM;

                // Если это новая ViewModel, асинхронно загружаем компоненты
                if (viewModel?.AGR_TopComponents?.Count == 0)
                {
                    await Task.Run(() => LoadAssemblyComponents(viewModel, document));
                }

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating assembly component: {document.Title}");
                throw;
            }
        }

        public async Task<AGR_PartComponentVM> CreatePartComponentAsync(ISwDocument3D document)
        {
            _logger.LogDebug($"Creating part component async: {document.Title}");

            try
            {
                // Используем кэш
                return await _cache.GetOrCreateViewModelAsync(document) as AGR_PartComponentVM;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating part component: {document.Title}");
                throw;
            }
        }

        public async Task<IAGR_BaseComponent> CreateComponentAsync(ISwDocument3D document)
        {
            _logger.LogDebug($"Creating component async: {document.Title}");

            return document switch
            {
                ISwPart part => await CreatePartComponentAsync(part),
                ISwAssembly assembly => await CreateAssemblyComponentAsync(assembly),
                _ => throw new NotSupportedException($"Document type not supported: {document.GetType()}")
            };
        }

        // Синхронные методы для обратной совместимости
        public AGR_AssemblyComponentVM CreateAssemblyComponent(ISwDocument3D document)
        {
            _logger.LogDebug($"Creating assembly component sync: {document.Title}");

            try
            {
                var viewModel = new AGR_AssemblyComponentVM(document as ISwAssembly);
                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating assembly component sync: {document.Title}");
                throw;
            }
        }

        public AGR_PartComponentVM CreatePartComponent(ISwDocument3D document)
        {
            _logger.LogDebug($"Creating part component sync: {document.Title}");

            try
            {
                return new AGR_PartComponentVM(document as ISwPart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating part component sync: {document.Title}");
                throw;
            }
        }

        private void LoadAssemblyComponents(AGR_AssemblyComponentVM viewModel, ISwDocument3D document)
        {
            try
            {
                _logger.LogDebug($"Loading assembly components for: {document.Title}");

                var assembly = document as ISwAssembly;
                if (assembly == null) return;

                // Оптимизация: получаем компоненты только верхнего уровня
                var topComponents = assembly.Configurations.Active.Components.AGR_ActiveComponents();

                // Группируем и создаем SpecificationItemVM для верхнего уровня
                var groupedTop = topComponents
                    .GroupBy(c => new { c.ReferencedDocument?.Title, c.ReferencedConfiguration?.Name })
                    .Select(g =>
                    {
                        try
                        {
                            var firstComp = g.First();
                            if (firstComp.ReferencedDocument is ISwDocument3D swDoc)
                            {
                                // Создаем или получаем ViewModel для компонента
                                var componentViewModel = Task.Run(async () =>
                                    await CreateComponentAsync(swDoc)).Result;

                                return new AGR_SpecificationItemVM(componentViewModel, g.Count());
                            }
                            return null;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error creating specification item");
                            return null;
                        }
                    })
                    .Where(item => item != null)
                    .ToList();

                // Обновляем коллекцию в UI потоке
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewModel.AGR_TopComponents = new ObservableCollection<AGR_SpecificationItemVM>(groupedTop);
                    _logger.LogDebug($"Assembly components loaded: {groupedTop.Count} items");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading assembly components: {document.Title}");
                throw;
            }
        }
    }
}