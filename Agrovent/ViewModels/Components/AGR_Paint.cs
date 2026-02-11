using Agrovent.DAL.Entities.Components;
using Agrovent.DAL;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;
using Microsoft.EntityFrameworkCore;

namespace Agrovent.ViewModels.Components
{
    internal class AGR_Paint2 : BaseViewModel, IAGR_Material
    {
        public string Name { get; set; }
        public string Article { get; set; }
        public string UOM { get; set; }
        public IAGR_AvaArticleModel AvaModel { get; set; }

        public AGR_Paint2(ISwDocument3D doc3D)
        {
            var colorProp = doc3D.Configurations.Active.Properties.GetOrPreCreate(AGR_PropertyNames.Color);
            if (!colorProp.IsCommitted) colorProp.Commit(CancellationToken.None);
            Name = colorProp.Value.ToString();

        }
    }

    public class AGR_Paint : BaseViewModel, IAGR_Material
    {
        private readonly ILogger<AGR_Material>? _logger; // Добавим логгер (опционально)

        #region CTOR
        public AGR_Paint(ISwDocument3D doc3D, ILogger<AGR_Material>? logger = null)
        {
            _logger = logger;
            // Инициализация Name из документа
            Name = doc3D.Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Color).Value?.ToString() ?? string.Empty;
            // Article и UOM остаются пустыми или null до тех пор, пока AvaModel не будет установлен
           // TryLoadAvaModelFromNameAsync().Wait();

        }

        public AGR_Paint(AvaArticleModel avaArticle, ILogger<AGR_Material>? logger = null)
        {
            _logger = logger;
            AvaModel = avaArticle;
        }
        #endregion

        #region PROPS
        #region Name
        private string _name = "";
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }
        #endregion

        #region Article
        private string _article = "";
        public string Article
        {
            get => _article;
            set => Set(ref _article, value);
        }
        #endregion

        #region UOM
        private string _uom = "";
        public string UOM
        {
            get => _uom;
            set => Set(ref _uom, value);
        }
        #endregion

        #region AvaModel
        private IAGR_AvaArticleModel? _avaModel;
        public IAGR_AvaArticleModel? AvaModel
        {
            get => _avaModel;
            set
            {
                if (Set(ref _avaModel, value))
                {
                    // Обновляем Name, Article, UOM из AvaModel
                    if (value != null)
                    {
                        Name = value.Name ?? string.Empty;
                        Article = value.Article.ToString() ?? string.Empty; // Предполагаем, что Article может быть int
                        UOM = value.MainUOM ?? string.Empty; // Или UOM, в зависимости от структуры AvaArticleModel
                    }
                    else
                    {
                        // Если AvaModel сброшен, сбрасываем и производные свойства
                        Name = string.Empty;
                        Article = string.Empty;
                        UOM = string.Empty;
                    }
                }
            }
        }
        #endregion
        #endregion

        #region METHODS
        // --- НОВЫЙ МЕТОД: Асинхронное получение AvaModel по Name ---
        // Этот метод должен вызываться извне (например, из ComponentViewModelFactory или ComponentVersionService)
        // и принимать DbContext или IAGR_ComponentRepository
        public async Task<bool> TryLoadAvaModelFromNameAsync()
        {
            DataContext dbContext = AGR_ServiceContainer.GetService<DataContext>();

            if (string.IsNullOrEmpty(Name))
            {
                _logger?.LogDebug("TryLoadAvaModelFromNameAsync: Name is null or empty, skipping lookup.");
                return false;
            }

            try
            {
                _logger?.LogDebug($"TryLoadAvaModelFromNameAsync: Looking up AvaArticle for Name '{Name}'");

                // Ищем в AvaArticles по Name
                var material = await dbContext.AvaArticles
                    .Where(x => x.Name.Contains("Краска",StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefaultAsync(x => x.Name.Contains(Name));

                if (material != null)
                {
                    _logger?.LogDebug($"TryLoadAvaModelFromNameAsync: Found AvaArticle with Article {material.Article} for Name '{Name}'");
                    AvaModel = material; // Устанавливаем AvaModel, что автоматически обновит Name, Article, UOM
                    return true;
                }
                else
                {
                    _logger?.LogInformation($"TryLoadAvaModelFromNameAsync: AvaArticle with Name '{Name}' not found in database.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"TryLoadAvaModelFromNameAsync: Error looking up AvaArticle for Name '{Name}'");
                // Не выбрасываем исключение, а возвращаем false
                return false;
            }
        }

        // --- АЛЬТЕРНАТИВНЫЙ МЕТОД: Если у вас есть IAGR_ComponentRepository ---
        // public async Task<bool> TryLoadAvaModelFromNameAsync(IAGR_ComponentRepository repository)
        // {
        //     if (string.IsNullOrEmpty(Name))
        //     {
        //         _logger?.LogDebug("TryLoadAvaModelFromNameAsync: Name is null or empty, skipping lookup.");
        //         return false;
        //     }
        //
        //     try
        //     {
        //         _logger?.LogDebug($"TryLoadAvaModelFromNameAsync: Looking up AvaArticle for Name '{Name}' via repository");
        //         var material = await repository.GetAvaArticleByNameAsync(Name); // Предполагаем, что метод существует
        //
        //         if (material != null)
        //         {
        //             _logger?.LogDebug($"TryLoadAvaModelFromNameAsync: Found AvaArticle with Article {material.Article} for Name '{Name}'");
        //             AvaModel = material;
        //             return true;
        //         }
        //         else
        //         {
        //             _logger?.LogInformation($"TryLoadAvaModelFromNameAsync: AvaArticle with Name '{Name}' not found via repository.");
        //             return false;
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger?.LogError(ex, $"TryLoadAvaModelFromNameAsync: Error looking up AvaArticle for Name '{Name}' via repository");
        //         return false;
        //     }
        // } 
        #endregion
    }

}
