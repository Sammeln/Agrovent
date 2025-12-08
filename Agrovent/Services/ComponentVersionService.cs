using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Repositories;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Components;
using Microsoft.Extensions.Logging;

namespace Agrovent.Services
{
    public  class ComponentVersionService : IAGR_ComponentVersionService
    {
        private readonly IComponentRepository _repository;
        private readonly ILogger<ComponentVersionService> _logger;

        public ComponentVersionService(IComponentRepository repository, ILogger<ComponentVersionService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component)
        {
            try
            {
                // Проверяем, изменился ли компонент
                var hasChanged = await _repository.HasComponentChanged(component);

                if (!hasChanged)
                {
                    _logger.LogInformation($"Компонент не изменился: {component.PartNumber}");
                    return false;
                }

                // Получаем существующую версию
                var existingVersion = await _repository.GetExistingVersion(component);

                if (existingVersion != null)
                {
                    _logger.LogInformation($"Компонент уже существует: {component.PartNumber} v{existingVersion.Version}");
                    return false;
                }

                // Сохраняем новую версию
                await _repository.SaveComponent(component, component.HashSum);
                _logger.LogInformation($"Компонент сохранен: {component.PartNumber}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении компонента: {component.PartNumber}");
                throw;
            }
        }

        public async Task<bool> CheckAndSaveAssemblyAsync(AGR_AssemblyComponentVM assembly)
        {
            try
            {
                // Проверяем, изменилась ли сборка
                var hasChanged = await _repository.HasComponentChanged(assembly);

                if (!hasChanged)
                {
                    _logger.LogInformation($"Сборка не изменилась: {assembly.PartNumber}");
                    return false;
                }

                // Сохраняем структуру сборки
                await _repository.SaveAssemblyStructure(assembly, assembly.AGR_TopComponents);
                _logger.LogInformation($"Структура сборки сохранена: {assembly.PartNumber}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении сборки: {assembly.PartNumber}");
                throw;
            }
        }

        public async Task<ComponentVersion?> GetComponentVersionAsync(string partNumber, int version)
        {
            return await _repository.GetComponentVersion(partNumber, version);
        }

        public async Task<bool> HasComponentChangedAsync(IAGR_BaseComponent component)
        {
            return await _repository.HasComponentChanged(component);
        }
    }
}
