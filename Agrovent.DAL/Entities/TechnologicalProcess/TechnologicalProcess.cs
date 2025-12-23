using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.DAL.Entities.TechnologicalProcess
{
    [Table("technological_processes")]
    public class TechnologicalProcess : BaseEntity
    {
        [Required]
        [Column("part_number")]
        [MaxLength(255)]
        public string PartNumber { get; set; }

        // Навигационное свойство к компоненту
        [ForeignKey(nameof(PartNumber))]
        public Component Component { get; set; }

        // Коллекция операций
        public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();
    }
}