using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.Components;

namespace Agrovent.DAL.Entities.Components
{
    public class AssemblyStructure : BaseEntity
    {
        // Сборка (родитель)
        public int AssemblyVersionId { get; set; }
        
        // Навигационное свойство для сборки
        [ForeignKey("AssemblyVersionId")]
        public ComponentVersion AssemblyVersion { get; set; }

        // Компонент в составе (ребенок)
        public int ComponentVersionId { get; set; }
        [ForeignKey("ComponentVersionId")]
        public ComponentVersion ComponentVersion { get; set; }

        // Количество
        public int Quantity { get; set; }

        // Уровень вложенности (0 - верхний уровень)
        public int Level { get; set; }

        // Родительская структура (для вложенности)
        public int? ParentStructureId { get; set; }
        [ForeignKey("ParentStructureId")]
        public AssemblyStructure? ParentStructure { get; set; }

        // Дочерние структуры
        public ICollection<AssemblyStructure> ChildStructures { get; set; } = new List<AssemblyStructure>();

        // Порядок следования (для сохранения порядка в спецификации)
        public int OrderIndex { get; set; }

        // Метод для получения полного пути
        public string GetFullPath()
        {
            var path = new List<string>();
            var current = this;

            while (current != null)
            {
                path.Insert(0, current.ComponentVersion.Name);
                current = current.ParentStructure;
            }

            return string.Join(" → ", path);
        }
    }
}