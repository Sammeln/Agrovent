using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.ViewModels.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public interface IAGR_ViewModelCacheService
    {
        IAGR_BaseComponent GetOrCreate(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent> factory);
        void Remove(ISwDocument3D document);
        void Clear();
        int Count { get; }
    }

    public class AGR_ViewModelCacheService : IAGR_ViewModelCacheService
    {
        private readonly ConcurrentDictionary<string, (ISwDocument3D Document, IAGR_BaseComponent ViewModel)> _viewModelCache;

        public AGR_ViewModelCacheService()
        {
            _viewModelCache = new ConcurrentDictionary<string, (ISwDocument3D Document, IAGR_BaseComponent ViewModel)>();
        }

        public IAGR_BaseComponent GetOrCreate(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent> factory)
        {
            var key = document.Title;
            var cached = _viewModelCache.GetOrAdd(key, _ => (document, factory(document)));

            // Обновляем ссылку на документ, если она изменилась
            if (!ReferenceEquals(cached.Document, document))
            {
                _viewModelCache[key] = (document, cached.ViewModel);
            }

            return cached.ViewModel;
        }

        public void Remove(ISwDocument3D document)
        {
            try
            {
                if (document.IsAlive != false && _viewModelCache.Count > 0)
                {
                    _viewModelCache.TryRemove(document.Title, out _);
                }
            }
            catch (COMException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Clear()
        {
            _viewModelCache.Clear();
        }

        /// <inheritdoc />
        public int Count => _viewModelCache.Count;
    }
}
