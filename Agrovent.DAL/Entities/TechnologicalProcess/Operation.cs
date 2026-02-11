using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.TechProcess;

namespace Agrovent.DAL.Entities.TechnologicalProcess
{
    [Table("operations")]
    public class Operation : BaseEntity
    {
        [Required]
        [Column("technological_process_id")]
        public int TechnologicalProcessId { get; set; }
        // Навигационное свойство
        [ForeignKey(nameof(TechnologicalProcessId))]
        public virtual TechnologicalProcess TechnologicalProcess { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(500)]
        public string Name { get; set; }
        // Внешний ключ на участок
        [Required]
        public int WorkstationId { get; set; }
        [ForeignKey(nameof(WorkstationId))]
        public virtual Workstation Workstation { get; set; } = null!;

        [Required]
        [Column("labor_intensity_minutes")]
        public decimal LaborIntensityMinutes { get; set; }

        [Required]
        [Column("sequence_number")]
        public int SequenceNumber { get; set; }

    }
}