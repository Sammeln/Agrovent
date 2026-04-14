namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_User
    {
        string FirstName { get; set; }
        string LastName { get; set; }
        string Patronymic { get; set; }

        string Initials { get; }
        string FullName { get; }
    }
}
