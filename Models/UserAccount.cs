namespace study_hub_reservation_system.Models;

public class UserAccount
{
    public required string Username { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public required string PasswordHash { get; set; }
}
