namespace web_api_elk.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

