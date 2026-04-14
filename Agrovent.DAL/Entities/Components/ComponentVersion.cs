using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.Projects;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;


namespace Agrovent.DAL.Entities.Components
{
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class ComponentVersion : DateStampEntity
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
        public byte[] PreviewImage { get; set; }

        // Ссылка на артикул Ava
        public int? AvaArticleArticle { get; set; }

        [ForeignKey("AvaArticleArticle")]
        public AvaArticleModel? AvaArticle { get; set; }

        // Типы
        public AGR_ComponentType_e ComponentType { get; set; }
        public AGR_AvaType_e AvaType { get; set; }

        // Пользователь, сохранивший эту версию
        public int SavedByUserId { get; set; }
        [ForeignKey("SavedByUserId")]
        public virtual UserEntity SavedByUser { get; set; }

        // Навигационные свойства
        public ComponentMaterial? Material { get; set; }
        public ICollection<ComponentProperty> Properties { get; set; } = new List<ComponentProperty>();
        public ICollection<ComponentFile> Files { get; set; } = new List<ComponentFile>();
        public virtual ICollection<ProjectComponent> ProjectComponents { get; set; } = new List<ProjectComponent>(); 

        // Метод для проверки, является ли версия последней
        public bool IsLatestVersion()
        {
            if (Component == null) return false;
            return Version == Component.Versions.Max(v => v.Version);
        }
    }
}
