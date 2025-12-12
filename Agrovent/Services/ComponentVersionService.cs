using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Repositories;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Components;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Agrovent.Services
{
    public class ComponentVersionService : IAGR_ComponentVersionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ComponentVersionService> _logger;
        private readonly DataContext _context;

        public ComponentVersionService(IUnitOfWork unitOfWork, ILogger<ComponentVersionService> logger, DataContext context)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _context = context;
        }

        public async Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Начинаем сохранение компонента: {component.PartNumber}");

                // 1. Проверяем, изменился ли компонент
                var hasChanged = await _unitOfWork.ComponentRepository.HasComponentChanged(component);

                if (!hasChanged)
                {
                    _logger.LogInformation($"Компонент не изменился: {component.PartNumber}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 2. Получаем существующую версию (в рамках той же транзакции)
                var existingVersion = await _unitOfWork.ComponentRepository.GetExistingVersion(component);

                if (existingVersion != null)
                {
                    _logger.LogInformation($"Компонент уже существует: {component.PartNumber} v{existingVersion.Version}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 3. Сохраняем компонент
                var savedVersion = await _unitOfWork.ComponentRepository.SaveComponent(component, component.HashSum);

                // 4. Сохраняем все изменения
                var savedCount = await _unitOfWork.CompleteAsync();
                _logger.LogInformation($"Сохранено {savedCount} записей для компонента {component.PartNumber}");

                // 5. Проверяем, что Id установлены
                if (savedVersion.Id == 0)
                {
                    _logger.LogError($"Id компонента {component.PartNumber} не установлен!");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation($"Компонент успешно сохранен: {component.PartNumber}, Id: {savedVersion.Id}");
                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, $"Ошибка при сохранении компонента: {component.PartNumber}");
                throw;
            }
        }

        public async Task<bool> CheckAndSaveAssemblyAsync(AGR_AssemblyComponentVM assembly)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Начинаем сохранение сборки: {assembly.PartNumber}");

                // 1. Проверяем, изменилась ли сборка
                var hasChanged = await _unitOfWork.ComponentRepository.HasComponentChanged(assembly);

                if (!hasChanged)
                {
                    _logger.LogInformation($"Сборка не изменилась: {assembly.PartNumber}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 2. Получаем существующую версию сборки
                var existingVersion = await _unitOfWork.ComponentRepository.GetExistingVersion(assembly);

                if (existingVersion != null)
                {
                    _logger.LogInformation($"Сборка уже существует: {assembly.PartNumber} v{existingVersion.Version}");
                    await _unitOfWork.RollbackTransactionAsync();
                    return false;
                }

                // 3. Сохраняем структуру сборки
                await _unitOfWork.ComponentRepository.SaveAssemblyStructure(assembly, assembly.AGR_TopComponents);

                // 4. Сохраняем все изменения и коммитим транзакцию
                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation($"Сборка успешно сохранена: {assembly.PartNumber}");
                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, $"Ошибка при сохранении сборки: {assembly.PartNumber}");
                throw;
            }
        }

        public async Task<ComponentVersion?> GetComponentVersionAsync(string partNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос версии компонента: {partNumber} v{version}");

                // Этот метод только для чтения, поэтому не используем транзакцию
                return await _unitOfWork.ComponentRepository.GetComponentVersion(partNumber, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении версии компонента: {partNumber}");
                throw;
            }
        }

        public async Task<bool> HasComponentChangedAsync(IAGR_BaseComponent component)
        {
            try
            {
                _logger.LogDebug($"Проверка изменений компонента: {component.PartNumber}");

                // Этот метод только для чтения, поэтому не используем транзакцию
                return await _unitOfWork.ComponentRepository.HasComponentChanged(component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке изменений компонента: {component.PartNumber}");
                throw;
            }
        }

        // Дополнительный метод для получения последней версии компонента
        public async Task<ComponentVersion?> GetLatestComponentVersionAsync(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос последней версии компонента: {partNumber}");
                return await _unitOfWork.ComponentRepository.GetLatestComponentVersion(partNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента: {partNumber}");
                throw;
            }
        }

        // Дополнительный метод для получения структуры сборки
        public async Task<List<AssemblyStructure>> GetAssemblyStructureAsync(string assemblyPartNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос структуры сборки: {assemblyPartNumber} v{version}");
                return await _unitOfWork.ComponentRepository.GetAssemblyStructure(assemblyPartNumber, version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении структуры сборки: {assemblyPartNumber}");
                throw;
            }
        }
    }
}