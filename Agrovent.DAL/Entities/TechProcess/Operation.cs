using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;

namespace Agrovent.DAL.Entities.TechProcess
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