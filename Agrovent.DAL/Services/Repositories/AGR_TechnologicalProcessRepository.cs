using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities.TechProcess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agrovent.DAL.Services.Repositories
{
    public interface IAGR_TechnologicalProcessRepository
    {
        // Получить техпроцесс по PartNumber компонента
        Task<TechnologicalProcess?> GetByPartNumberAsync(string partNumber);

        // Получить или создать техпроцесс для компонента
        Task<TechnologicalProcess> GetOrCreateForComponentAsync(string partNumber);

        // Добавить операцию в существующий техпроцесс
        Task<Operation> AddOperationAsync(TechnologicalProcess process, TemplateOperation templateOp, int sequenceNumber);

        // Удалить операцию
        Task RemoveOperationAsync(int operationId);

        // Обновить операцию
        Task UpdateOperationAsync(Operation operation);

        Task<List<TemplateOperation>> GetTemplateOperationAsync();
        // Сохранить изменения (или использовать UoW)
        // Task<int> SaveChangesAsync();
    }

    public class AGR_TechnologicalProcessRepository : IAGR_TechnologicalProcessRepository
    {
        private readonly DataContext _context;
        private readonly ILogger<AGR_TechnologicalProcessRepository> _logger; // Опционально

        public AGR_TechnologicalProcessRepository(DataContext context, ILogger<AGR_TechnologicalProcessRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TechnologicalProcess?> GetByPartNumberAsync(string partNumber)
        {
            return await _context.TechProcesses
                .Include(tp => tp.Operations)
                .FirstOrDefaultAsync(tp => tp.PartNumber == partNumber);
        }

        public async Task<TechnologicalProcess> GetOrCreateForComponentAsync(string partNumber)
        {
            var existing = await GetByPartNumberAsync(partNumber);
            if (existing != null)
            {
                return existing;
            }

            var newProcess = new TechnologicalProcess { PartNumber = partNumber };
            _context.TechProcesses.Add(newProcess);
            await _context.SaveChangesAsync(); // Или передать управление UoW
            _logger?.LogInformation($"Created new TechnologicalProcess for PartNumber: {partNumber}");
            return newProcess;
        }

        public async Task<Operation> AddOperationAsync(TechnologicalProcess process, TemplateOperation templateOp, int sequenceNumber)
        {
            var newOp = new Operation
            {
                TechnologicalProcessId = process.Id,
                Name = templateOp.Name,
                WorkstationName = templateOp.Workstation?.Name ?? templateOp.WorkstationId.ToString(), // Или как-то иначе получить имя
                CostPerHour = -1,
                SequenceNumber = sequenceNumber,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Operations.Add(newOp);
            await _context.SaveChangesAsync(); // Или передать управление UoW
            _logger?.LogDebug($"Added Operation '{newOp.Name}' (Seq: {newOp.SequenceNumber}) to TechnologicalProcess for PartNumber: {process.PartNumber}");
            return newOp;
        }

        public async Task<List<TemplateOperation>> GetTemplateOperationAsync()
        {
            return await _context.TemplateOperations
               .Include(to => to.Workstation)
               .ToListAsync();
        }

        public async Task RemoveOperationAsync(int operationId)
        {
            var operation = await _context.Operations.FindAsync(operationId);
            if (operation != null)
            {
                _context.Operations.Remove(operation);
                await _context.SaveChangesAsync(); // Или передать управление UoW
            }
        }

        public async Task UpdateOperationAsync(Operation operation)
        {
            _context.Operations.Update(operation);
            await _context.SaveChangesAsync(); // Или передать управление UoW
        }
        // ... другие методы
    }


}
