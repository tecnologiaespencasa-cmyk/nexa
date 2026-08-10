namespace Nexa.Services.Models;

public class PersonalProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool HasProfilePhoto { get; set; }
    public int ProfilePhotoHorizontalPosition { get; set; } = 50;
    public int ProfilePhotoVerticalPosition { get; set; } = 50;
    public decimal ProfilePhotoZoom { get; set; } = 1m;
}
