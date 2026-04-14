namespace Agrovent.Models
{
    public class AGR_User : IAGR_User
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
    }
}
