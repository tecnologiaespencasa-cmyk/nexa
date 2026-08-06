namespace IntranetPrueba.Services.Models;

public class PersonalProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool HasProfilePhoto { get; set; }
}
