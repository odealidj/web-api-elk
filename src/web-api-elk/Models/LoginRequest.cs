using System.ComponentModel.DataAnnotations;

namespace web_api_elk.Models;

public class LoginRequest
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Password { get; set; } = null!;
}

