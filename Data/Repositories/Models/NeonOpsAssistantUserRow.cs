namespace IntranetPrueba.Data.Repositories.Models;

public class NeonOpsAssistantUserRow
{
    public bool IsActive { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string LastName2 { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;

    public string FullName
    {
        get
        {
            var names = new[] { FirstName, LastName1, LastName2 }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(' ', names);
        }
    }
}
