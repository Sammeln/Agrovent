using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;

namespace Agrovent.DAL.Entities.TechnologicalProcess
{
    [Table("operations")]
    public class Operation : BaseEntity
    {
        [Required]
        [Column("technological_process_id")]
        public int TechnologicalProcessId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(500)]
        public string Name { get; set; }

        [Required]
        [Column("section")]
        [MaxLength(255)]
        public string Section { get; set; }

        [Required]
        [Column("labor_intensity_minutes")]
        public decimal LaborIntensityMinutes { get; set; }

        [Required]
        [Column("sequence_number")]
        public int SequenceNumber { get; set; }

        // Навигационное свойство
        [ForeignKey(nameof(TechnologicalProcessId))]
        public virtual TechnologicalProcess TechnologicalProcess { get; set; }
    }
}