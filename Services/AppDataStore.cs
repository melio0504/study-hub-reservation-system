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
    private const int WeekdayOpeningHour = 13;
    private const int WeekendOpeningHour = 8;
    private const int ClosingHourNextDayAbsolute = 29;

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

    public IReadOnlyList<Reservation> GetReservationsForUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Array.Empty<Reservation>();
        }

        return LoadReservations()
            .Where(r => string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.StartHour)
            .ThenBy(r => r.SeatId)
            .ToList();
    }

    public bool TryCreateReservation(Reservation newReservation, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!IsReservationSlotValid(newReservation.ReservationDate, newReservation.StartHour, newReservation.DurationHours, out errorMessage))
        {
            return false;
        }

        if (GetReservationStart(newReservation.ReservationDate, newReservation.StartHour) < DateTime.Now)
        {
            errorMessage = "Selected reservation start time is in the past.";
            return false;
        }

        var reservations = LoadReservations();
        var hasConflict = reservations.Any(existing =>
            string.Equals(existing.SeatId, newReservation.SeatId, StringComparison.OrdinalIgnoreCase)
            && ReservationsOverlap(existing, newReservation.ReservationDate, newReservation.StartHour, newReservation.DurationHours));

        if (hasConflict)
        {
            errorMessage = "Selected seat is already reserved for that time range.";
            return false;
        }

        reservations.Add(newReservation);
        SaveReservations(reservations);
        return true;
    }

    public bool TryCancelReservation(string reservationId, string username, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(reservationId) || string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "Invalid reservation selection.";
            return false;
        }

        var reservations = LoadReservations();
        var reservation = reservations.FirstOrDefault(r =>
            string.Equals(r.ReservationId, reservationId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase));

        if (reservation is null)
        {
            errorMessage = "Reservation not found or you do not have permission to cancel it.";
            return false;
        }

        reservations.Remove(reservation);
        SaveReservations(reservations);
        return true;
    }

    public bool TryRescheduleReservation(
        string reservationId,
        string username,
        DateOnly newDate,
        int newStartHour,
        int newDurationHours,
        decimal newTotalCost,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(reservationId) || string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "Invalid reservation selection.";
            return false;
        }

        var reservations = LoadReservations();
        var reservation = reservations.FirstOrDefault(r =>
            string.Equals(r.ReservationId, reservationId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase));

        if (reservation is null)
        {
            errorMessage = "Reservation not found or you do not have permission to reschedule it.";
            return false;
        }

        if (!IsReservationSlotValid(newDate, newStartHour, newDurationHours, out errorMessage))
        {
            return false;
        }

        if (GetReservationStart(newDate, newStartHour) < DateTime.Now)
        {
            errorMessage = "Selected reservation start time is in the past.";
            return false;
        }

        var hasConflict = reservations.Any(existing =>
            !string.Equals(existing.ReservationId, reservation.ReservationId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.SeatId, reservation.SeatId, StringComparison.OrdinalIgnoreCase)
            && ReservationsOverlap(existing, newDate, newStartHour, newDurationHours));

        if (hasConflict)
        {
            errorMessage = "This seat is already reserved for the new time range.";
            return false;
        }

        reservation.ReservationDate = newDate;
        reservation.StartHour = newStartHour;
        reservation.DurationHours = newDurationHours;
        reservation.TotalCost = newTotalCost;

        SaveReservations(reservations);
        return true;
    }

    private static bool ReservationsOverlap(Reservation existing, DateOnly date, int startHour, int durationHours)
    {
        var existingStart = GetReservationStart(existing.ReservationDate, existing.StartHour);
        var existingEnd = existingStart.AddHours(existing.DurationHours);
        var candidateStart = GetReservationStart(date, startHour);
        var candidateEnd = candidateStart.AddHours(durationHours);

        return existingStart < candidateEnd && candidateStart < existingEnd;
    }

    private static DateTime GetReservationStart(DateOnly date, int startHour)
    {
        return date.ToDateTime(new TimeOnly(startHour, 0));
    }

    private static bool IsReservationSlotValid(DateOnly date, int startHour, int durationHours, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (startHour is < 0 or > 23)
        {
            errorMessage = "Start time must be between 00:00 and 23:00.";
            return false;
        }

        if (durationHours < 1)
        {
            errorMessage = "Reservation must be at least 1 hour.";
            return false;
        }

        var openingHour = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? WeekendOpeningHour
            : WeekdayOpeningHour;

        if (startHour < openingHour)
        {
            errorMessage = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? "Weekend bookings can start from 08:00 onward."
                : "Weekday bookings can start from 13:00 onward.";
            return false;
        }

        if ((startHour + durationHours) > ClosingHourNextDayAbsolute)
        {
            errorMessage = "Selected end time exceeds the 05:00 closing time.";
            return false;
        }

        return true;
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

    private void SaveReservations(List<Reservation> reservations)
    {
        File.WriteAllText(_reservationsPath, JsonSerializer.Serialize(reservations, _jsonOptions));
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
