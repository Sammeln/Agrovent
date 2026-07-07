using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Interfaces.Entities.TechProcess;

namespace AgroventInfrastructure.Entities.TechProcess
{
    [Table("Operations")]
    public class Operation : DateStampEntity
    {
        public string WorkstationName { get; set; }
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,4)")]
        public decimal CostPerHour { get; set; }

        [Required]
        public int SequenceNumber { get; set; }

        // Внешний ключ на техпроцесс
        [Required]
        public int TechnologicalProcessId { get; set; }
        [ForeignKey(nameof(TechnologicalProcessId))]
        public virtual TechnologicalProcess TechnologicalProcess { get; set; } = null!;
    }
}