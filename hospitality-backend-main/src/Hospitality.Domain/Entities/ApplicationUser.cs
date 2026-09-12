using Microsoft.AspNetCore.Identity;

namespace Hospitality.Domain.Entities;

public class ApplicationUser : IdentityUser, IAuditable, ISoftDelete
{
    // Información personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    
    // Información del empleado
    public string? EmployeeId { get; set; }
    public string? Department { get; set; } // Reception, Housekeeping, Maintenance, Management
    public string? Position { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    
    // Preferencias
    public string? Language { get; set; } = "es";
    public string? TimeZone { get; set; } = "UTC";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    
    // Auditoría
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Actividad
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public bool IsActive { get; set; } = true;
    // Debe cambiar la contraseña en el próximo inicio de sesión (primer acceso / registro)
    public bool MustChangePassword { get; set; }
    
    // Refresh tokens para JWT
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Autenticación en dos pasos (TOTP)
    // TwoFactorEnabled ya lo aporta Identity.
    // RecoveryCodes: códigos de recuperación (SHA-256, separados por ':').
    public string? RecoveryCodes { get; set; }
    
    // [DEPRECADO Fase 0] Propiedad (hotel) del usuario — modelo "un hotel por dueño".
    // El scope real ahora viene de PropertyAssignment (por propiedad) y
    // OrganizationMember (por organización/tenant). Se conserva como fallback de
    // transición en auth y se elimina al migrar los guards y el registro.
    public Guid? HotelId { get; set; }
    
    // Navigation properties
    public ICollection<MaintenanceTicket> ReportedTickets { get; set; } = new List<MaintenanceTicket>();
    
    // Propiedades calculadas
    public string FullName => $"{FirstName} {LastName}";
    
    public bool IsEmployed => HireDate.HasValue && 
                             (!TerminationDate.HasValue || TerminationDate.Value > DateTime.UtcNow);
    
    public TimeSpan? Tenure => HireDate.HasValue 
        ? (DateTime.UtcNow - HireDate.Value) 
        : null;
    
    // Métodos de negocio
    public void RecordLogin(string ipAddress)
    {
        LastLoginAt = DateTime.UtcNow;
        LastLoginIp = ipAddress;
    }
    
    public void Terminate(string reason)
    {
        TerminationDate = DateTime.UtcNow;
        IsActive = false;
        Notes = $"[TERMINADO] {reason} - {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
    }
    
    public void Reactivate()
    {
        TerminationDate = null;
        IsActive = true;
        Notes = $"[REACTIVADO] {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
    }
    
    public bool HasPermission(string permission)
    {
        // Esto se integraría con el sistema de roles y permisos
        // Por ahora, devolvemos true para usuarios activos
        return IsActive && IsEmployed;
    }
    
    public bool CanAccessDepartment(string department)
    {
        if (!IsActive || !IsEmployed) return false;
        
        // Lógica de acceso por departamento
        return Department switch
        {
            "Management" => true, // Management tiene acceso a todo
            "Reception" => department is "Reception" or "Housekeeping" or "Maintenance",
            "Housekeeping" => department is "Housekeeping",
            "Maintenance" => department is "Maintenance",
            _ => false
        };
    }
    
    public string GetRoleDisplayName()
    {
        return Department switch
        {
            "Management" => "Gerencia",
            "Reception" => "Recepción",
            "Housekeeping" => "Housekeeping",
            "Maintenance" => "Mantenimiento",
            _ => "Usuario"
        };
    }
    
    public string GetStatusBadgeColor()
    {
        if (!IsActive) return "gray";
        if (!IsEmployed) return "red";
        if (TerminationDate.HasValue) return "yellow";
        return "green";
    }
    
    public string GetStatusDescription()
    {
        if (!IsActive) return "Inactivo";
        if (!IsEmployed) return "No empleado";
        if (TerminationDate.HasValue) return "En proceso de terminación";
        return "Activo";
    }
    
    // Campo adicional para notas
    public string? Notes { get; set; }
}