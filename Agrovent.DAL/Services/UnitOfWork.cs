using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xarial.XCad.Documents;

namespace Agrovent.DAL
{
    public interface IUnitOfWork : IDisposable
    {
        IAGR_ComponentRepository ComponentRepository { get; }
        IAGR_TechnologicalProcessRepository TechProcessRepository { get; }
        Task<int> CompleteAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly DataContext _context;
        private IDbContextTransaction _transaction;
        private bool _disposed = false;

        public IAGR_ComponentRepository ComponentRepository { get; }
        public IAGR_TechnologicalProcessRepository TechProcessRepository { get; }

        public UnitOfWork(DataContext context, IAGR_ComponentRepository componentRepository, IAGR_TechnologicalProcessRepository techProcessRepository)
        {
            _context = context;
            ComponentRepository = componentRepository;
            TechProcessRepository = techProcessRepository;
        }

        public async Task<int> CompleteAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Логируем ошибки базы данных
                throw new Exception("Ошибка при сохранении изменений в базе данных", ex);
            }
        }
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
            return _transaction;
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

     

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _context.Dispose();
                }
                _disposed = true;
            }
        }
    }
}