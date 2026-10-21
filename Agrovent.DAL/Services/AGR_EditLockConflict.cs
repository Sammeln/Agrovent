namespace Agrovent.DAL.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AgroventInfrastructure.Entities.Base;
    using AgroventInfrastructure.Entities.Components;
    using AgroventInfrastructure.Enums;
    using AgroventInfrastructure.Interfaces;
    using AgroventInfrastructure.Interfaces.Components.Base;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Npgsql;

    public class AGR_EditLockConflict
    {
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime TakenAt { get; set; }
    }

    /// <summary>Состояние компонента для окна выбора: Id, версия и замок.</summary>
    public class AGR_EditComponentInfo
    {
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public int Version { get; set; }
        public AGR_EditState_e State { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
    }

    public interface IAGR_EditLockService
    {
        /// <summary>Пакетно: по PartNumber возвращает Id, последнюю версию и замок.
        /// Отсутствующих в БД в словаре нет.</summary>
        Task<Dictionary<string, AGR_EditComponentInfo>> GetEditInfoAsync(IEnumerable<string> partNumbers);

        Task<int> GetCurrentUserIdAsync();

        /// <summary>Берёт замки одной транзакцией (всё или ничего). Свои замки конфликтом не считаются
        /// и не пересоздаются. Пустой список = успех.</summary>
        Task<IReadOnlyList<AGR_EditLockConflict>> TryTakeAsync(
            IReadOnlyCollection<(int ComponentId, int BaseVersion)> items, string machineName);

        /// <summary>Снимает замки текущего пользователя. Чужие не трогает.</summary>
        Task ReleaseAsync(IEnumerable<int> componentIds);

        Task<Dictionary<int, ComponentEditLock>> GetLocksAsync(IEnumerable<int> componentIds);

        /// <summary>Заполняет EditState, EditLockedBy и HasMyLocks в VM одним-двумя лёгкими запросами.
        /// vm.ComponentVersion должен быть загружен (IsInDatabase == SavedInDataBase).</summary>
        Task RefreshEditStateAsync(IAGR_BaseComponent vm);
    }

    public class AGR_EditLockService : IAGR_EditLockService
    {
        private const string UniqueViolation = "23505"; // PostgreSQL

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAGR_User _currentUser;
        private readonly SemaphoreSlim _userGate = new(1, 1);
        private int? _userId;

        public AGR_EditLockService(IServiceScopeFactory scopeFactory, IAGR_User currentUser)
        {
            _scopeFactory = scopeFactory;
            _currentUser = currentUser;
        }

        public async Task<int> GetCurrentUserIdAsync()
        {
            if (_userId.HasValue) return _userId.Value;

            await _userGate.WaitAsync();
            try
            {
                if (_userId.HasValue) return _userId.Value;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();

                var user = await db.Users.FirstOrDefaultAsync(u =>
                    u.FirstName == _currentUser.FirstName
                    && u.LastName == _currentUser.LastName
                    && u.Patronymic == _currentUser.Patronymic);

                if (user == null)
                {
                    user = new UserEntity
                    {
                        FirstName = _currentUser.FirstName,
                        LastName = _currentUser.LastName,
                        Patronymic = _currentUser.Patronymic
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                _userId = user.Id;
                return user.Id;
            }
            finally { _userGate.Release(); }
        }

        public async Task<IReadOnlyList<AGR_EditLockConflict>> TryTakeAsync(
            IReadOnlyCollection<(int ComponentId, int BaseVersion)> items, string machineName)
        {
            var userId = await GetCurrentUserIdAsync();
            var ids = items.Select(i => i.ComponentId).ToList();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            // 1. Быстрая проверка: чужие замки (чтобы показать конкретные имена)
            var conflicts = await GetConflictsAsync(db, ids, userId);
            if (conflicts.Count > 0) return conflicts;

            // 2. Вставка только тех, где замка ещё нет. Гонку двух пользователей ловит уникальный индекс.
            var alreadyMine = await db.ComponentEditLocks
                .Where(l => ids.Contains(l.ComponentId) && l.UserId == userId)
                .Select(l => l.ComponentId)
                .ToListAsync();

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                foreach (var (componentId, baseVersion) in items.Where(i => !alreadyMine.Contains(i.ComponentId)))
                {
                    db.ComponentEditLocks.Add(new ComponentEditLock
                    {
                        ComponentId = componentId,
                        UserId = userId,
                        BaseVersion = baseVersion,
                        MachineName = machineName
                    });
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return Array.Empty<AGR_EditLockConflict>();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == UniqueViolation)
            {
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                return await GetConflictsAsync(db, ids, userId); // кто успел раньше
            }
        }

        private static async Task<List<AGR_EditLockConflict>> GetConflictsAsync(DataContext db, List<int> ids, int userId)
        {
            return await db.ComponentEditLocks
                .AsNoTracking()
                .Where(l => ids.Contains(l.ComponentId) && l.UserId != userId)
                .Select(l => new AGR_EditLockConflict
                {
                    ComponentId = l.ComponentId,
                    PartNumber = l.Component.PartNumber,
                    UserName = l.User.LastName + " " + l.User.FirstName,
                    TakenAt = l.CreatedAt
                })
                .ToListAsync();
        }

        public async Task ReleaseAsync(IEnumerable<int> componentIds)
        {
            var userId = await GetCurrentUserIdAsync();
            var ids = componentIds.ToList();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var locks = await db.ComponentEditLocks
                .Where(l => ids.Contains(l.ComponentId) && l.UserId == userId)
                .ToListAsync();
            db.ComponentEditLocks.RemoveRange(locks);
            await db.SaveChangesAsync();
        }

        public async Task<Dictionary<int, ComponentEditLock>> GetLocksAsync(IEnumerable<int> componentIds)
        {
            var ids = componentIds.ToList();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            return await db.ComponentEditLocks
                .AsNoTracking()
                .Include(l => l.User)
                .Where(l => ids.Contains(l.ComponentId))
                .ToDictionaryAsync(l => l.ComponentId);
        }

        public async Task<Dictionary<string, AGR_EditComponentInfo>> GetEditInfoAsync(IEnumerable<string> partNumbers)
        {
            var pns = partNumbers
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList();
            var result = new Dictionary<string, AGR_EditComponentInfo>();
            if (pns.Count == 0) return result;

            var userId = await GetCurrentUserIdAsync();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Последняя версия в БД. Версию файла (из маркера) подставляет вызывающий: окно выбора (AGR_EditSelectionViewModel)
            var components = await db.Components
                .AsNoTracking()
                .Where(c => pns.Contains(c.PartNumber))
                .Select(c => new
                {
                    c.Id,
                    c.PartNumber,
                    Version = c.Versions.Max(v => (int?)v.Version)
                })
                .ToListAsync();

            var ids = components.Select(c => c.Id).ToList();
            var locks = await db.ComponentEditLocks
                .AsNoTracking()
                .Where(l => ids.Contains(l.ComponentId))
                .Select(l => new { l.ComponentId, l.UserId, Name = l.User.LastName + " " + l.User.FirstName, l.CreatedAt })
                .ToDictionaryAsync(l => l.ComponentId);

            foreach (var c in components)
            {
                if (c.Version == null) continue; // компонент без версий для редактирования не годится

                var info = new AGR_EditComponentInfo
                {
                    ComponentId = c.Id,
                    PartNumber = c.PartNumber,
                    Version = c.Version.Value,
                    State = AGR_EditState_e.Free
                };
                if (locks.TryGetValue(c.Id, out var l))
                {
                    info.State = l.UserId == userId ? AGR_EditState_e.EditingByMe : AGR_EditState_e.EditingByOther;
                    info.LockedBy = l.Name;
                    info.LockedAt = l.CreatedAt;
                }
                result[c.PartNumber] = info;
            }
            return result;
        }

        public async Task RefreshEditStateAsync(IAGR_BaseComponent vm)
        {
            var version = vm?.ComponentVersion;
            if (version == null)
            {
                if (vm != null)
                {
                    vm.EditState = AGR_EditState_e.Unknown;
                    vm.EditLockedBy = null;
                    vm.HasMyLocks = false;
                }
                return;
            }

            var userId = await GetCurrentUserIdAsync();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Замок на сам компонент
            var own = await db.ComponentEditLocks
                .AsNoTracking()
                .Where(l => l.ComponentId == version.ComponentId)
                .Select(l => new { l.UserId, Name = l.User.LastName + " " + l.User.FirstName })
                .FirstOrDefaultAsync();

            if (own == null) { vm.EditState = AGR_EditState_e.Free; vm.EditLockedBy = null; }
            else if (own.UserId == userId) { vm.EditState = AGR_EditState_e.EditingByMe; vm.EditLockedBy = null; }
            else { vm.EditState = AGR_EditState_e.EditingByOther; vm.EditLockedBy = own.Name; }

            // Для сборки HasMyLocks = есть мой замок на любом потомке (по структуре из БД)
            if (vm.ComponentType != AGR_ComponentType_e.Assembly)
            {
                vm.HasMyLocks = vm.EditState == AGR_EditState_e.EditingByMe;
                return;
            }

            var myLockedIds = await db.ComponentEditLocks
                .AsNoTracking()
                .Where(l => l.UserId == userId)
                .Select(l => l.ComponentId)
                .ToListAsync();

            if (myLockedIds.Count == 0) { vm.HasMyLocks = false; return; }
            if (myLockedIds.Contains(version.ComponentId)) { vm.HasMyLocks = true; return; }

            // Обход структуры по уровням, только Id (без Include свойств и файлов)
            var seenVersions = new HashSet<int> { version.Id };
            var frontier = new List<int> { version.Id };
            while (frontier.Count > 0)
            {
                var children = await db.AssemblyStructures
                    .AsNoTracking()
                    .Where(s => frontier.Contains(s.ParentComponentVersionId))
                    .Select(s => new { s.ChildComponentVersionId, s.ChildComponentVersion.ComponentId })
                    .ToListAsync();

                if (children.Any(c => myLockedIds.Contains(c.ComponentId))) { vm.HasMyLocks = true; return; }

                frontier = children
                    .Select(c => c.ChildComponentVersionId)
                    .Where(id => seenVersions.Add(id))
                    .ToList();
            }
            vm.HasMyLocks = false;
        }
    }
}