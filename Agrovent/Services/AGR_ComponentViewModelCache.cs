// AGR_ComponentViewModelCache.cs
using System.Collections.Concurrent;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Services;
using Agrovent.ViewModels.Components;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Agrovent.Services
{
    public class AGR_ComponentViewModelCache : IAGR_ComponentViewModelCache
    {
        private readonly ConcurrentDictionary<string, WeakReference<IAGR_BaseComponent>> _viewModelCache;
        private readonly ILogger<AGR_ComponentViewModelCache> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public AGR_ComponentViewModelCache(ILogger<AGR_ComponentViewModelCache> logger)
        {
            _viewModelCache = new ConcurrentDictionary<string, WeakReference<IAGR_BaseComponent>>();
            _logger = logger;
        }

        public async Task<IAGR_BaseComponent> GetOrCreateViewModelAsync(IXDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var swDoc = document as ISwDocument3D;
            if (swDoc == null)
                throw new InvalidOperationException("Document must be a SolidWorks 3D document");

            var cacheKey = GetCacheKey(swDoc);

            try
            {
                // Пытаемся получить из кэша
                if (_viewModelCache.TryGetValue(cacheKey, out var weakRef) &&
                    weakRef.TryGetTarget(out var cachedViewModel))
                {
                    _logger.LogDebug($"ViewModel found in cache: {cacheKey}");
                    return cachedViewModel;
                }

                // Если не нашли в кэше, создаем новую
                await _semaphore.WaitAsync();
                try
                {
                    // Двойная проверка под блокировкой
                    if (_viewModelCache.TryGetValue(cacheKey, out weakRef) &&
                        weakRef.TryGetTarget(out cachedViewModel))
                    {
                        _logger.LogDebug($"ViewModel found in cache after lock: {cacheKey}");
                        return cachedViewModel;
                    }

                    _logger.LogInformation($"Creating new ViewModel for: {cacheKey}");

                    IAGR_BaseComponent viewModel = swDoc switch
                    {
                        ISwPart part => new AGR_PartComponentVM(part),
                        ISwAssembly assembly => new AGR_AssemblyComponentVM(assembly),
                        _ => throw new NotSupportedException($"Document type not supported: {swDoc.GetType()}")
                    };

                    // Добавляем в кэш с WeakReference
                    _viewModelCache[cacheKey] = new WeakReference<IAGR_BaseComponent>(viewModel);

                    // Очищаем кэш от "мертвых" ссылок
                    CleanupDeadReferences();

                    return viewModel;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting or creating ViewModel for {cacheKey}");
                throw;
            }
        }

        public async Task<IAGR_BaseComponent> GetViewModelByPathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var cacheKey = Path.GetFullPath(filePath).ToLowerInvariant();

            if (_viewModelCache.TryGetValue(cacheKey, out var weakRef) &&
                weakRef.TryGetTarget(out var viewModel))
            {
                return viewModel;
            }

            return null;
        }

        public void Clear()
        {
            _viewModelCache.Clear();
            _logger.LogInformation("ViewModel cache cleared");
        }

        public bool RemoveViewModel(string key)
        {
            return _viewModelCache.TryRemove(key, out _);
        }

        public (int Total, int Part, int Assembly) GetCacheStatistics()
        {
            int aliveCount = 0, partCount = 0, assemblyCount = 0;

            foreach (var kvp in _viewModelCache)
            {
                if (kvp.Value.TryGetTarget(out var vm))
                {
                    aliveCount++;
                    if (vm is AGR_PartComponentVM)
                        partCount++;
                    else if (vm is AGR_AssemblyComponentVM)
                        assemblyCount++;
                }
            }

            return (aliveCount, partCount, assemblyCount);
        }

        private string GetCacheKey(ISwDocument3D document)
        {
            // Используем полный путь к файлу как ключ
            // Также можно добавить хеш содержимого для учета изменений
            return Path.GetFullPath(document.Path).ToLowerInvariant();
        }

        private void CleanupDeadReferences()
        {
            // Очищаем кэш только если он стал слишком большим
            if (_viewModelCache.Count > 100)
            {
                var deadKeys = _viewModelCache
                    .Where(kvp => !kvp.Value.TryGetTarget(out _))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in deadKeys)
                {
                    _viewModelCache.TryRemove(key, out _);
                }

                if (deadKeys.Count > 0)
                {
                    _logger.LogDebug($"Cleaned up {deadKeys.Count} dead references from cache");
                }
            }
        }
    }
}