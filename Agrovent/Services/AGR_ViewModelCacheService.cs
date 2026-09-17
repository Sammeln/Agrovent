using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using Agrovent.DAL;
using Agrovent.Infrastructure.Extensions;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Components.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    public interface IAGR_ViewModelCacheService
    {
        public ConcurrentDictionary<string, (ISwDocument3D Document, IAGR_BaseComponent ViewModel)> ViewModelsDictonary { get; }
         IAGR_BaseComponent? GetOrCreate(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent?> factory);
         Task<IAGR_BaseComponent?> GetOrCreateAsync(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent?> factory);

        (ISwDocument3D Document, IAGR_BaseComponent ViewModel)? Get(ISwDocument3D doc);

        void Remove(ISwDocument3D document);
        void Clear();
        int Count { get; }
    }

    public class AGR_ViewModelCacheService : IAGR_ViewModelCacheService
    {
        public ConcurrentDictionary<string, (ISwDocument3D Document, IAGR_BaseComponent ViewModel)> ViewModelsDictonary { get; private set; }
        private IUnitOfWork _unitOfWork;
        public AGR_ViewModelCacheService(IUnitOfWork unitOfWork)
        {
            ViewModelsDictonary = new ConcurrentDictionary<string, (ISwDocument3D Document, IAGR_BaseComponent ViewModel)>();
            _unitOfWork = unitOfWork;
        }


        public IAGR_BaseComponent? GetOrCreate(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent?> factory)
        {
            if (document is null) return null;
            var key = document.Path;
            //var pn = document.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString();
            //пытаемся получить componentVersion для открытой 3д модели
            //var cv = GetComponentVersionAsync(pn).Result;

            var cached = ViewModelsDictonary.GetOrAdd(key, _ => (document, factory(document)));
            // Обновляем ссылку на документ, если она изменилась
            if (!ReferenceEquals(cached.Document, document))
            {
                ViewModelsDictonary[key] = (document, cached.ViewModel);
            }

            return cached.ViewModel;
        }

        public async Task<IAGR_BaseComponent?> GetOrCreateAsync(ISwDocument3D document, Func<ISwDocument3D, IAGR_BaseComponent?> factory)
        {
            if (document is null) return null;
            var key = document.Path;
            var pn = document.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value.ToString();
            //пытаемся получить componentVersion для открытой 3д модели
            //var cv = await GetComponentVersionAsync(pn);

            var cached = ViewModelsDictonary.GetOrAdd(key, _ => (document, factory(document)));
            // Обновляем ссылку на документ, если она изменилась
            if (!ReferenceEquals(cached.Document, document))
            {
                ViewModelsDictonary[key] = (document, cached.ViewModel);
            }

            return cached.ViewModel;
        }

        public (ISwDocument3D Document, IAGR_BaseComponent ViewModel)? Get(ISwDocument3D doc)
        {
            if (doc is null) return null;
            var VM = ViewModelsDictonary.GetValueOrDefault(doc.Path);
            
            return VM;

        }

        private async Task<ComponentVersion?> GetComponentVersionAsync(string partnumber)
        {
            return await _unitOfWork.ComponentRepository.GetLatestComponentVersion(partnumber);
        }

        public void Remove(ISwDocument3D document)
        {
            try
            {
                if (document.IsAlive != false && ViewModelsDictonary.Count > 0)
                {
                    ViewModelsDictonary.TryRemove(document.Path, out _);
                }
            }
            catch (COMException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Clear()
        {
            ViewModelsDictonary.Clear();
        }

        /// <inheritdoc />
        public int Count => ViewModelsDictonary.Count;
    }
}
