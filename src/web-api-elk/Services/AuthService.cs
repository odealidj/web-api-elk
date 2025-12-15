using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using web_api_elk.Models;
using web_api_elk.Repositories;

namespace web_api_elk.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepository, IPasswordHasher<User> passwordHasher, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? correlationId, CancellationToken cancellationToken = default)
    {
        var result = new AuthResult
        {
            Success = false,
            Message = "Registration failed",
            Username = request.Username
        };

        // Validate password strength
        if (!IsValidPassword(request.Password, out var passwordError))
        {
            result.Message = passwordError;
            _logger.LogWarning("User registration failed for {Username}. Reason: {Reason}. CorrelationId: {CorrelationId}",
                request.Username, passwordError, correlationId);
            return result;
        }

        // Check duplicate username
        if (await _userRepository.UsernameExistsAsync(request.Username, cancellationToken))
        {
            result.Message = "Username already exists";
            _logger.LogWarning("User registration failed. Username {Username} already exists. CorrelationId: {CorrelationId}",
                request.Username, correlationId);
            return result;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        result.Success = true;
        result.Message = "User registered successfully";
        result.UserId = user.Id;

        _logger.LogInformation("User registration succeeded for {Username} with Id {UserId}. CorrelationId: {CorrelationId}",
            user.Username, user.Id, correlationId);

        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? correlationId, CancellationToken cancellationToken = default)
    {
        var result = new AuthResult
        {
            Success = false,
            Message = "Invalid username or password",
            Username = request.Username
        };

        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Login failed. User {Username} not found. CorrelationId: {CorrelationId}",
                request.Username, correlationId);
            return result;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed. Invalid password for {Username}. CorrelationId: {CorrelationId}",
                request.Username, correlationId);
            return result;
        }

        // Optionally handle PasswordVerificationResult.SuccessRehashNeeded in future

        result.Success = true;
        result.Message = "Login successful";
        result.UserId = user.Id;

        _logger.LogInformation("Login succeeded for {Username} with Id {UserId}. CorrelationId: {CorrelationId}",
            user.Username, user.Id, correlationId);

        return result;
    }

    private static bool IsValidPassword(string password, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            error = "Password must be at least 8 characters long.";
            return false;
        }

        if (!password.Any(char.IsUpper))
        {
            error = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!password.Any(char.IsLower))
        {
            error = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (!password.Any(char.IsDigit))
        {
            error = "Password must contain at least one digit.";
            return false;
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            error = "Password must contain at least one symbol.";
            return false;
        }

        return true;
    }
}
