using Agrovent.DAL;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_Material2 : BaseViewModel, IAGR_Material
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                Set(ref _name, value);
            }
        }

        private string _article;
        public string Article 
        { 
            get => _article; 
            set => _article = value; 
        }

        public string UOM { get; set; }

        private IAGR_AvaArticleModel avaModel;
        public IAGR_AvaArticleModel AvaModel
        {
            get => avaModel;
            set
            {
                if(Set(ref avaModel, value))
                {
                    Name = AvaModel.Name;
                    Article = AvaModel.Article.ToString();
                    UOM = AvaModel.MainUOM;
                }

            }

        }

        private void TryGetArticle()
        {
            try
            {

                using (var db = new DAL.DataContext())
                {
                    var material = db.AvaArticles.FirstOrDefault(x => x.Name == Name);
                    if (material != null)
                    {
                        AvaModel = material;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public AGR_Material2(ISwDocument3D doc3D)
        {
            Name = doc3D.Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Material).Value.ToString();
            TryGetArticle();
        }

        public AGR_Material2(AvaArticleModel avaArticle)
        {
            AvaModel = avaArticle;
        }
    }

    public class AGR_Material : BaseViewModel, IAGR_Material
    {
        private readonly ILogger<AGR_Material>? _logger; // Добавим логгер (опционально)

        #region CTOR
        public AGR_Material(ISwDocument3D doc3D, ILogger<AGR_Material>? logger = null)
        {
            _logger = logger;
            // Инициализация Name из документа
            Name = doc3D.Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Material).Value?.ToString() ?? string.Empty;
            // Article и UOM остаются пустыми или null до тех пор, пока AvaModel не будет установлен
            //TryLoadAvaModelFromNameAsync().Wait();
        }

        public AGR_Material(AvaArticleModel avaArticle, ILogger<AGR_Material>? logger = null)
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
                var material = await dbContext.AvaArticles.FirstOrDefaultAsync(x => x.Name == Name);

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
