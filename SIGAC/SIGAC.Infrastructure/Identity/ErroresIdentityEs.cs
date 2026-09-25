using Microsoft.AspNetCore.Identity;
using SIGAC.Domain;

namespace SIGAC.Infrastructure.Identity
{
    // Mensajes de error de Identity en español y en lenguaje simple (requisito
    // del documento de visión: nada de textos técnicos para el usuario). Se
    // registra con AddErrorDescriber en Program.cs; UsuariosService los concatena
    // cuando un IdentityResult falla.
    public class ErroresIdentityEs : IdentityErrorDescriber
    {
        public override IdentityError DefaultError() => new()
        {
            Code = nameof(DefaultError),
            Description = "Ocurrió un error inesperado. Intente de nuevo."
        };

        public override IdentityError ConcurrencyFailure() => new()
        {
            Code = nameof(ConcurrencyFailure),
            Description = "Otra persona modificó este usuario al mismo tiempo. Recargue la página e intente de nuevo."
        };

        public override IdentityError PasswordMismatch() => new()
        {
            Code = nameof(PasswordMismatch),
            Description = "La contraseña actual no es correcta."
        };

        public override IdentityError InvalidToken() => new()
        {
            Code = nameof(InvalidToken),
            Description = "El código de verificación no es válido o venció."
        };

        public override IdentityError RecoveryCodeRedemptionFailed() => new()
        {
            Code = nameof(RecoveryCodeRedemptionFailed),
            Description = "El código de recuperación no es válido."
        };

        public override IdentityError LoginAlreadyAssociated() => new()
        {
            Code = nameof(LoginAlreadyAssociated),
            Description = "Ya existe un usuario con ese inicio de sesión."
        };

        public override IdentityError InvalidUserName(string? userName) => new()
        {
            Code = nameof(InvalidUserName),
            Description = $"El correo '{userName}' no es válido como nombre de usuario."
        };

        public override IdentityError InvalidEmail(string? email) => new()
        {
            Code = nameof(InvalidEmail),
            Description = $"El correo '{email}' no es válido."
        };

        public override IdentityError DuplicateUserName(string userName) => new()
        {
            Code = nameof(DuplicateUserName),
            Description = $"Ya existe un usuario con el correo '{userName}'."
        };

        public override IdentityError DuplicateEmail(string email) => new()
        {
            Code = nameof(DuplicateEmail),
            Description = $"Ya existe un usuario con el correo '{email}'."
        };

        public override IdentityError InvalidRoleName(string? role) => new()
        {
            Code = nameof(InvalidRoleName),
            Description = $"El rol '{role}' no es válido."
        };

        public override IdentityError DuplicateRoleName(string role) => new()
        {
            Code = nameof(DuplicateRoleName),
            Description = $"Ya existe el rol '{role}'."
        };

        public override IdentityError UserAlreadyHasPassword() => new()
        {
            Code = nameof(UserAlreadyHasPassword),
            Description = "El usuario ya tiene una contraseña."
        };

        public override IdentityError UserLockoutNotEnabled() => new()
        {
            Code = nameof(UserLockoutNotEnabled),
            Description = "El bloqueo no está habilitado para este usuario."
        };

        public override IdentityError UserAlreadyInRole(string role) => new()
        {
            Code = nameof(UserAlreadyInRole),
            Description = $"El usuario ya tiene el rol '{role}'."
        };

        public override IdentityError UserNotInRole(string role) => new()
        {
            Code = nameof(UserNotInRole),
            Description = $"El usuario no tiene el rol '{role}'."
        };

        public override IdentityError PasswordTooShort(int length) => new()
        {
            Code = nameof(PasswordTooShort),
            Description = $"La contraseña debe tener al menos {length} caracteres."
        };

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
        {
            Code = nameof(PasswordRequiresUniqueChars),
            Description = $"La contraseña debe tener al menos {uniqueChars} caracteres distintos."
        };

        public override IdentityError PasswordRequiresNonAlphanumeric() => new()
        {
            Code = nameof(PasswordRequiresNonAlphanumeric),
            Description = "La contraseña debe tener al menos un símbolo (por ejemplo: ! . # $ %)."
        };

        public override IdentityError PasswordRequiresDigit() => new()
        {
            Code = nameof(PasswordRequiresDigit),
            Description = "La contraseña debe tener al menos un número."
        };

        public override IdentityError PasswordRequiresLower() => new()
        {
            Code = nameof(PasswordRequiresLower),
            Description = "La contraseña debe tener al menos una letra minúscula."
        };

        public override IdentityError PasswordRequiresUpper() => new()
        {
            Code = nameof(PasswordRequiresUpper),
            Description = "La contraseña debe tener al menos una letra mayúscula."
        };
    }
}
