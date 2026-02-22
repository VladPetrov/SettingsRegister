namespace BackOfficeSmall.Application.Exceptions;

public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, string entityIdentifier)
        : base($"{entityName} '{entityIdentifier}' was not found.")
    {
        EntityName = entityName;
        EntityIdentifier = entityIdentifier;
    }

    public string EntityName { get; }

    public string EntityIdentifier { get; }
}
