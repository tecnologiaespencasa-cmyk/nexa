using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Nexa.Models.ViewModels;

public class ProfileIndexViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = "Sin rol asignado";
    public bool HasProfilePhoto { get; set; }
    public ChangeProfileEmailViewModel EmailChange { get; set; } = new();
    public ChangeProfilePasswordViewModel PasswordChange { get; set; } = new();
    public ChangeProfilePhotoViewModel PhotoChange { get; set; } = new();
}

public class ChangeProfileEmailViewModel
{
    [Required(ErrorMessage = "El nuevo correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [StringLength(150)]
    [Display(Name = "Nuevo correo electrónico")]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña actual para confirmar el cambio.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public class ChangeProfilePasswordViewModel
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "La nueva contraseña debe tener mínimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmación no coincide con la nueva contraseña.")]
    [Display(Name = "Confirmar nueva contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangeProfilePhotoViewModel
{
    [Required(ErrorMessage = "Selecciona una foto de perfil.")]
    [Display(Name = "Foto de perfil")]
    public IFormFile? Photo { get; set; }

    [Range(0, 100)]
    [Display(Name = "Encuadre vertical")]
    public int VerticalPosition { get; set; } = 50;
}
