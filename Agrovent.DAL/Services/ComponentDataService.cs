using System.Threading.Tasks;
using Agrovent.DAL.Entities.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Base;

namespace Agrovent.DAL.Services
{
    public interface IComponentDataService
    {
        public ComponentVersion? GetLatestComponentVersion(string partNumber);
        Task<ComponentVersion> GetLatestComponentVersionAsync(string partNumber);
        public AvaArticleModel? GetAvaArticle(int article);
        Task<AvaArticleModel> GetAvaArticleAsync(int article);
        public bool ComponentExistsInDatabase(string partNumber);
        Task<bool> ComponentExistsInDatabaseAsync(string partNumber);
    }

    public class ComponentDataService : IComponentDataService
    {
        private readonly DataContext _context;
        private readonly ILogger<ComponentDataService> _logger;

        public ComponentDataService(DataContext context, ILogger<ComponentDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public ComponentVersion? GetLatestComponentVersion(string partNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partNumber))
                    return null;
                var component = _context.Components
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.AvaArticle)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Properties)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Material)
                    .FirstOrDefault(c => c.PartNumber == partNumber);
                if (component == null)
                    return null;
                return component.Versions
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента {partNumber}");
                return null;
            }
        }
        public async Task<ComponentVersion> GetLatestComponentVersionAsync(string partNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partNumber))
                    return null;

                var component = await _context.Components
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.AvaArticle)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Properties)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Material)
                    .FirstOrDefaultAsync(c => c.PartNumber == partNumber);

                if (component == null)
                    return null;

                return component.Versions
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента {partNumber}");
                return null;
            }
        }

        public AvaArticleModel? GetAvaArticle(int article)
        {
            try
            {
                return _context.AvaArticles
                    .FirstOrDefault(a => a.Article == article);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении AvaArticle {article}");
                return null;
            }
        }

        public async Task<AvaArticleModel> GetAvaArticleAsync(int article)
        {
            try
            {
                return await _context.AvaArticles
                    .FirstOrDefaultAsync(a => a.Article == article);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении AvaArticle {article}");
                return null;
            }
        }

        public bool ComponentExistsInDatabase(string partNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partNumber))
                    return false;
                return _context.Components
                    .Any(c => c.PartNumber == partNumber);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке существования компонента {partNumber}");
                return false;
            }
        }
        public async Task<bool> ComponentExistsInDatabaseAsync(string partNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partNumber))
                    return false;

                return await _context.Components
                    .AnyAsync(c => c.PartNumber == partNumber);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке существования компонента {partNumber}");
                return false;
            }
        }
    }
}