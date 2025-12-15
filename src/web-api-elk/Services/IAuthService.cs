using web_api_elk.Models;

namespace web_api_elk.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? correlationId, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, string? correlationId, CancellationToken cancellationToken = default);
}

