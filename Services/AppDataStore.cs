using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using study_hub_reservation_system.Models;

namespace study_hub_reservation_system.Services;

public class AppDataStore
{
    private readonly string _usersPath;
    private readonly string _reservationsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public AppDataStore()
    {
        var dataDir = ResolveDataDirectory();

        Directory.CreateDirectory(dataDir);

        _usersPath = Path.Combine(dataDir, "users.json");
        _reservationsPath = Path.Combine(dataDir, "reservations.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    private static string ResolveDataDirectory()
    {
        // Prefer a project-local Database folder by walking up to the directory
        // containing the .csproj file.
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var hasProjectFile = current.GetFiles("*.csproj").Any();
            if (hasProjectFile)
            {
                return Path.Combine(current.FullName, "Database");
            }

            current = current.Parent;
        }

        // Fallback to the process working directory if project root wasn't found.
        return Path.Combine(Directory.GetCurrentDirectory(), "Database");
    }

    public bool SignUp(string username, string emailAddress, string password, out string errorMessage)
    {
        errorMessage = string.Empty;
        var users = LoadUsers();

        if (users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = "Username already exists.";
            return false;
        }

        if (users.Any(u => string.Equals(u.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = "Email address is already registered.";
            return false;
        }

        users.Add(new UserAccount
        {
            Username = username,
            EmailAddress = emailAddress,
            PasswordHash = HashPassword(password)
        });

        SaveUsers(users);
        return true;
    }

    public bool ValidateLogin(string username, string password)
    {
        var users = LoadUsers();
        var hash = HashPassword(password);

        return users.Any(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)
            && u.PasswordHash == hash);
    }

    public IReadOnlyList<Reservation> GetReservations()
    {
        return LoadReservations();
    }

    public IReadOnlyList<Reservation> GetReservationsForDate(DateOnly date)
    {
        return LoadReservations()
            .Where(r => r.ReservationDate == date)
            .OrderBy(r => r.StartHour)
            .ToList();
    }
}
