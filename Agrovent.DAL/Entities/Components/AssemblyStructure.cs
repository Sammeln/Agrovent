using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.Components;

namespace Agrovent.DAL.Entities.Components
{
     public class AssemblyStructure : DateStampEntity
    {
        // ID родительской версии компонента (сборки)
        public int ParentComponentVersionId { get; set; }

        // Навигационное свойство для родительской версии компонента (сборки)
        [ForeignKey("ParentComponentVersionId")]
        public ComponentVersion ParentComponentVersion { get; set; } = null!; // Указываем, что не null

        // ID дочерней версии компонента (детали или подсборки)
        public int ChildComponentVersionId { get; set; }

        // Навигационное свойство для дочерней версии компонента
        [ForeignKey("ChildComponentVersionId")]
        public ComponentVersion ChildComponentVersion { get; set; } = null!; // Указываем, что не null

        // Количество дочерних компонентов
        public int Quantity { get; set; }

        // Порядок следования (для сохранения порядка в спецификации)
        public int Order { get; set; }

        // Уровень вложенности (опционально, можно вычислять при загрузке)
        // public int Level { get; set; }
    }
}