// File: DAL/Entities/TechProcess/Workstation.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgroventInfrastructure.Entities.Base;

namespace AgroventInfrastructure.Entities.TechProcess
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