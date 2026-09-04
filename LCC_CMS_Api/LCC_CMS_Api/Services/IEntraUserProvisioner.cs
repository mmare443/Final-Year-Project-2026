namespace LCC_CMS_Api.Services;

public sealed record ProvisionedEntraAccount(string ObjectId, string UserPrincipalName);

public interface IEntraUserProvisioner
{
    Task<ProvisionedEntraAccount> CreateStudentAccountAsync(
        string displayName,
        string mailNickname,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(string objectId, CancellationToken cancellationToken = default);
}

public sealed class EntraProvisioningException : Exception
{
    public EntraProvisioningException(string message, int statusCode = StatusCodes.Status502BadGateway)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public EntraProvisioningException(string message, Exception inner, int statusCode = StatusCodes.Status502BadGateway)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
