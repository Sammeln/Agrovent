using Agrovent.DAL.Entities.Base;

namespace Agrovent.DAL.Entities.Components
{
    public class Component : BaseEntity
    {
        // Основной идентификатор (PartNumber)
        public string PartNumber { get; set; }

        // Навигационные свойства
        public ICollection<ComponentVersion> Versions { get; set; } = new List<ComponentVersion>();

        // Метод для получения последней версии
        public ComponentVersion? GetLatestVersion()
            => Versions.OrderByDescending(v => v.Version).FirstOrDefault();
    }
}