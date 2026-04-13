// File: DAL/Entities/TechProcess/Workstation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.TechProcess;

namespace Agrovent.DAL.Entities.TechProcess
{
    [Table("Workstations")]
    public class Workstation : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int AvaId { get; set; }

        // Обратная навигация к операциям
        public virtual ICollection<TemplateOperation> TemplateOperations { get; set; } = new List<TemplateOperation>();
    }
}