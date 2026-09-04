using System.Security.Cryptography;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using GraphUser = Microsoft.Graph.Models.User;

namespace LCC_CMS_Api.Services;

/// <summary>
/// Creates and deletes Entra member users via Microsoft Graph (client credentials).
/// Application permission required: User.ReadWrite.All (admin consent).
/// Does not send welcome mail (FR-1.6 is out of scope).
/// </summary>
public sealed class GraphEntraUserProvisioner : IEntraUserProvisioner
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GraphEntraUserProvisioner> _logger;

    public GraphEntraUserProvisioner(
        IConfiguration configuration,
        ILogger<GraphEntraUserProvisioner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProvisionedEntraAccount> CreateStudentAccountAsync(
        string displayName,
        string mailNickname,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var domain = (_configuration["Graph:Domain"] ?? "lccb.ac.pg").Trim().TrimStart('@');
        var nickname = SanitizeMailNickname(mailNickname);
        var upn = $"{nickname}@{domain}";

        var graphUser = new GraphUser
        {
            AccountEnabled = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? nickname : displayName.Trim(),
            MailNickname = nickname,
            UserPrincipalName = upn,
            PasswordProfile = new PasswordProfile
            {
                ForceChangePasswordNextSignIn = true,
                Password = GenerateTemporaryPassword(),
            },
        };

        try
        {
            var created = await client.Users.PostAsync(graphUser, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(created?.Id))
            {
                throw new EntraProvisioningException("Entra did not return an object id for the new student account.");
            }

            return new ProvisionedEntraAccount(created.Id, created.UserPrincipalName ?? upn);
        }
        catch (EntraProvisioningException)
        {
            throw;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 409)
        {
            throw new EntraProvisioningException(
                $"An Entra account already exists for {upn}.",
                ex,
                StatusCodes.Status409Conflict);
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Graph create user failed for {Upn}. Code={Code}", upn, ex.Error?.Code);
            throw new EntraProvisioningException(
                "Could not create the Entra ID student account.",
                ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Graph create user failed for {Upn}.", upn);
            throw new EntraProvisioningException(
                "Could not create the Entra ID student account.",
                ex);
        }
    }

    public async Task DeleteAccountAsync(string objectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return;

        var client = CreateClient();
        try
        {
            await client.Users[objectId].DeleteAsync(cancellationToken: cancellationToken);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Already gone — compensation succeeded.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Graph compensating delete failed for {ObjectId}.", objectId);
            throw new EntraProvisioningException(
                "The student record was not saved, but the Entra account could not be removed automatically.",
                ex);
        }
    }

    private GraphServiceClient CreateClient()
    {
        var tenantId = _configuration["Graph:TenantId"];
        var clientId = _configuration["Graph:ClientId"];
        var clientSecret = _configuration["Graph:ClientSecret"];
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new EntraProvisioningException(
                "Entra provisioning is not configured (Graph:TenantId, ClientId, ClientSecret).",
                StatusCodes.Status503ServiceUnavailable);
        }

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    private static string SanitizeMailNickname(string mailNickname)
    {
        var raw = (mailNickname ?? "").Trim().ToLowerInvariant();
        var chars = raw.Where(c => char.IsLetterOrDigit(c)).ToArray();
        if (chars.Length == 0)
        {
            throw new EntraProvisioningException("A mail nickname is required to create the Entra account.");
        }

        return new string(chars);
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@$?";
        var all = upper + lower + digits + symbols;
        Span<char> buffer = stackalloc char[16];
        buffer[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buffer[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buffer[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        buffer[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var i = 4; i < buffer.Length; i++)
        {
            buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        return new string(buffer);
    }
}
