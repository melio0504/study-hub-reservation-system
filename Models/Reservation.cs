using System;

namespace study_hub_reservation_system.Models;

public class Reservation
{
    public required string ReservationId { get; set; }
    public required string Username { get; set; }
    public required string SeatId { get; set; }
    public required DateOnly ReservationDate { get; set; }
    public required int StartHour { get; set; }
    public required int DurationHours { get; set; }
    public required decimal HourlyRate { get; set; }
    public required decimal TotalCost { get; set; }
    public required string PaymentMethod { get; set; }
}
