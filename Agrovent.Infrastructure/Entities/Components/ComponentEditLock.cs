using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroventInfrastructure.Entities.Components
{
    /// <summary>Замок "компонент взят на редактирование". Один компонент - один замок.</summary>
    public class ComponentEditLock : Base.DateStampEntity
    {
        public int ComponentId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(ComponentId))]
        public Component Component { get; set; } = null!;

        public int UserId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserId))]
        public Base.UserEntity User { get; set; } = null!;

        /// <summary>Версия, на основе которой идёт работа (из маркера локального файла).</summary>
        public int BaseVersion { get; set; }

        public string MachineName { get; set; } = "";
        // CreatedAt из DateStampEntity используется как время взятия на редактирование.
    }
}
