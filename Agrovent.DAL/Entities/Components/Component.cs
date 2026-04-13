using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.TechProcess;

namespace Agrovent.DAL.Entities.Components
{
    public class Component : DateStampEntity
    {
        // Основной идентификатор (PartNumber)
        public string PartNumber { get; set; }

        // Навигационные свойства
        public ICollection<ComponentVersion> Versions { get; set; } = new List<ComponentVersion>();
        public virtual TechnologicalProcess? TechnologicalProcess { get; set; }

        // Метод для получения последней версии
        public ComponentVersion? GetLatestVersion()
            => Versions.OrderByDescending(v => v.Version).FirstOrDefault();
    }
}