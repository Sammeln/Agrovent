using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;


namespace Agrovent.DAL.Entities.Components
{
    public class ComponentVersion : BaseEntity
    {
        // Связь с компонентом
        public int ComponentId { get; set; }
        [ForeignKey("ComponentId")]
        public Component Component { get; set; }

        // Версия (начинается с 1)
        public int Version { get; set; }

        // Уникальный хеш модели
        public int HashSum { get; set; }

        // Основные свойства
        public string Name { get; set; }
        public string ConfigName { get; set; }


        // Ссылка на артикул Ava
        public int? AvaArticleArticle { get; set; }

        [ForeignKey("AvaArticleArticle")]
        public AvaArticleModel? AvaArticle { get; set; }

        // Типы
        public AGR_ComponentType_e ComponentType { get; set; }
        public AGR_AvaType_e AvaType { get; set; }

        // Навигационные свойства
        public ICollection<ComponentProperty> Properties { get; set; } = new List<ComponentProperty>();
        public ComponentMaterial? Material { get; set; }
        public ICollection<ComponentFile> Files { get; set; } = new List<ComponentFile>();

        // Метод для проверки, является ли версия последней
        public bool IsLatestVersion()
        {
            if (Component == null) return false;
            return Version == Component.Versions.Max(v => v.Version);
        }
    }
}
