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

    public bool TryCreateReservation(Reservation newReservation, out string errorMessage)
    {
        errorMessage = string.Empty;

        var reservations = LoadReservations();
        var hasConflict = reservations.Any(existing =>
            existing.SeatId == newReservation.SeatId
            && existing.ReservationDate == newReservation.ReservationDate
            && TimesOverlap(
                existing.StartHour,
                existing.DurationHours,
                newReservation.StartHour,
                newReservation.DurationHours));

        if (hasConflict)
        {
            errorMessage = "Selected seat is already reserved for that time range.";
            return false;
        }

        reservations.Add(newReservation);
        SaveReservations(reservations);
        return true;
    }

    private static bool TimesOverlap(int startA, int durationA, int startB, int durationB)
    {
        var endA = startA + durationA;
        var endB = startB + durationB;
        return startA < endB && startB < endA;
    }

    private List<UserAccount> LoadUsers()
    {
        if (!File.Exists(_usersPath))
        {
            return new List<UserAccount>();
        }

        var text = File.ReadAllText(_usersPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<UserAccount>();
        }

        return JsonSerializer.Deserialize<List<UserAccount>>(text, _jsonOptions)
               ?? new List<UserAccount>();
    }

    private List<Reservation> LoadReservations()
    {
        if (!File.Exists(_reservationsPath))
        {
            return new List<Reservation>();
        }

        var text = File.ReadAllText(_reservationsPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<Reservation>();
        }

        return JsonSerializer.Deserialize<List<Reservation>>(text, _jsonOptions)
               ?? new List<Reservation>();
    }

    private void SaveUsers(List<UserAccount> users)
    {
        File.WriteAllText(_usersPath, JsonSerializer.Serialize(users, _jsonOptions));
    }
}
