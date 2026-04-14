using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities.Components;

namespace Agrovent.DAL.Entities.Base
{
    public class UserEntity : BaseEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Patronymic { get; set; }

        public string Initials
        {
            get
            {
                var initials = "";
                if (!string.IsNullOrEmpty(LastName))
                    initials += LastName + " ";
                
                if (!string.IsNullOrEmpty(FirstName))
                    initials += FirstName[0] + ".";
                
                if (!string.IsNullOrEmpty(Patronymic))
                    initials += Patronymic[0] + ".";

                return initials.Trim();
            }
        }

        public string FullName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(LastName))
                    parts.Add(LastName);
                if (!string.IsNullOrEmpty(FirstName))
                    parts.Add(FirstName);
                if (!string.IsNullOrEmpty(Patronymic))
                    parts.Add(Patronymic);

                return string.Join(" ", parts);
            }
        }

        // Навигационное свойство
        public virtual ICollection<ComponentVersion> SavedComponentVersions { get; set; } = new List<ComponentVersion>();
    }
}
