using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Models.ViewModels;

public class UserListItemViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class NursingAssistantListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class NursingAssistantCreateViewModel
{
    [Required(ErrorMessage = "El nombre del auxiliar es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    [Display(Name = "Nombre del auxiliar administrativo de enfermeria")]
    public string Name { get; set; } = string.Empty;
}

public class OpsAssistantListItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName1 { get; set; } = string.Empty;
    public string LastName2 { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
}

public class OpsAssistantCreateViewModel
{
    [Required(ErrorMessage = "El nombre del auxiliar OPS es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    [Display(Name = "Nombre del auxiliar OPS")]
    public string Name { get; set; } = string.Empty;
}

public class UserAdministrationIndexViewModel
{
    public List<UserListItemViewModel> Users { get; set; } = [];
    public List<NursingAssistantListItemViewModel> NursingAssistants { get; set; } = [];
    public List<OpsAssistantListItemViewModel> OpsAssistants { get; set; } = [];
    public string OpsSearchTerm { get; set; } = string.Empty;
    public int OpsCurrentPage { get; set; } = 1;
    public int OpsPageSize { get; set; } = 25;
    public int OpsTotalPages { get; set; }
    public int OpsTotalCount { get; set; }
    public NursingAssistantCreateViewModel NewNursingAssistant { get; set; } = new();
    public OpsAssistantCreateViewModel NewOpsAssistant { get; set; } = new();
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 80 caracteres.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "El usuario solo permite letras, numeros, punto, guion y guion bajo.")]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo valido.")]
    [StringLength(150)]
    [Display(Name = "Correo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(80)]
    [Display(Name = "Nombres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Primer apellido")]
    public string LastName1 { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Segundo apellido")]
    public string? LastName2 { get; set; }

    [Required(ErrorMessage = "La cedula es obligatoria.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "La cedula debe tener entre 5 y 20 caracteres.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "La cedula solo permite numeros.")]
    [Display(Name = "Cedula")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "La contrasena debe tener minimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contrasena")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion es obligatoria.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "La confirmacion no coincide con la contrasena.")]
    [Display(Name = "Confirmar contrasena")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UserEditViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 80 caracteres.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "El usuario solo permite letras, numeros, punto, guion y guion bajo.")]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo valido.")]
    [StringLength(150)]
    [Display(Name = "Correo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(80)]
    [Display(Name = "Nombres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Primer apellido")]
    public string LastName1 { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Segundo apellido")]
    public string? LastName2 { get; set; }

    [Required(ErrorMessage = "La cedula es obligatoria.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "La cedula debe tener entre 5 y 20 caracteres.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "La cedula solo permite numeros.")]
    [Display(Name = "Cedula")]
    public string NationalId { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class UserResetPasswordViewModel
{
    [Required]
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "La nueva contrasena debe tener minimo 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contrasena")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion es obligatoria.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmacion no coincide con la contrasena.")]
    [Display(Name = "Confirmar nueva contrasena")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class PermissionOptionViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UserPermissionAssignmentViewModel
{
    [Required]
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public List<PermissionOptionViewModel> AvailablePermissions { get; set; } = [];

    public List<PermissionOptionViewModel> GrantedPermissions { get; set; } = [];

    public List<int> GrantedPermissionIds { get; set; } = [];
}
