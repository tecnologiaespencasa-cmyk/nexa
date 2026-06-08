using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre de usuario no puede superar 80 caracteres.")]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [StringLength(200, ErrorMessage = "La contraseña supera la longitud máxima permitida.")]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Mantener sesión iniciada")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
