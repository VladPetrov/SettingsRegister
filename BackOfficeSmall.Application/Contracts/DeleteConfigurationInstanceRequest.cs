using BackOfficeSmall.Application.Exceptions;

namespace BackOfficeSmall.Application.Contracts;

public sealed record DeleteConfigurationInstanceRequest(string DeletedBy)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeletedBy))
        {
            throw new ValidationException("DeletedBy is required.");
        }
    }
}
