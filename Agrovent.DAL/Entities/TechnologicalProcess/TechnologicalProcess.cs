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
        public string PartNumber { get; set; } = string.Empty; // Убедимся, что не null

        // Навигационное свойство к компоненту по PartNumber
        // ВАЖНО: Это создаст внешний ключ на Component.PartNumber
        // Убедитесь, что в Component есть PK PartNumber или UNIQUE индекс на PartNumber
        [ForeignKey(nameof(PartNumber))]
        public virtual Component Component { get; set; } = null!; // Указываем, что не null

        // Коллекция операций
        public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();
    }
}