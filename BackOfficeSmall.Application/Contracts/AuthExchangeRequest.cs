namespace BackOfficeSmall.Application.Contracts;

public sealed record AuthExchangeRequest(string UpstreamToken)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UpstreamToken))
        {
            throw new Exceptions.ValidationException("UpstreamToken is required.");
        }
    }
}
