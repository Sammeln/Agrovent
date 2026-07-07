// File: DAL/Entities/TechProcess/TemplateOperation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgroventInfrastructure.Entities.Base;

namespace AgroventInfrastructure.Entities.TechProcess
{
    [Table("TemplateOperations")]
    public class TemplateOperation : BaseEntity
    {
        public int? AvaId { get; set; }

        public string Name { get; set; } = string.Empty;

        // Внешний ключ на участок
        public int WorkstationId { get; set; }
        [ForeignKey(nameof(WorkstationId))]
        public virtual Workstation Workstation { get; set; } = null!;

        [Column(TypeName = "decimal(10,4)")]
        public decimal CostPerHour { get; set; }

    }
}