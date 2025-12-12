// IAGR_ComponentViewModelCache.cs
using System.Threading.Tasks;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Xarial.XCad.Documents;

namespace Agrovent.Infrastructure.Interfaces.Services
{
    public interface IAGR_ComponentViewModelCache
    {
        /// <summary>
        /// Получить ViewModel по документу или создать новую
        /// </summary>
        Task<IAGR_BaseComponent> GetOrCreateViewModelAsync(IXDocument document);

        /// <summary>
        /// Получить ViewModel по пути к файлу
        /// </summary>
        Task<IAGR_BaseComponent> GetViewModelByPathAsync(string filePath);

        /// <summary>
        /// Очистить кэш
        /// </summary>
        void Clear();

        /// <summary>
        /// Удалить ViewModel из кэша
        /// </summary>
        bool RemoveViewModel(string key);

        /// <summary>
        /// Статистика кэша
        /// </summary>
        (int Total, int Part, int Assembly) GetCacheStatistics();
    }
}