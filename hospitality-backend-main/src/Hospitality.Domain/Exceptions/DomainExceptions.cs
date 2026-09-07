namespace Hospitality.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message)
    {
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object id) 
        : base($"{entityName} with id {id} was not found.")
    {
    }
}

public class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}

public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}