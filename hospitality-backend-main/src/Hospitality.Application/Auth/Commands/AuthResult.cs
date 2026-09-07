namespace Hospitality.Application.Auth.Commands;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public List<AuthError> Errors { get; set; } = new List<AuthError>();

    public static AuthResult Success() => new AuthResult { Succeeded = true };

    public static AuthResult Failure(params string[] errors)
    {
        return new AuthResult
        {
            Errors = errors.Select(e => new AuthError { Description = e }).ToList()
        };
    }
}

public class AuthError
{
    public string Description { get; set; } = string.Empty;
}