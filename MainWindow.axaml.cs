using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using study_hub_reservation_system.Models;
using study_hub_reservation_system.Services;

namespace study_hub_reservation_system;

public partial class MainWindow : Window
{
	private const double SeatPlanScale = 1.25;
	private const int OpeningHour = 8;
	private const int LastStartHour = 20;
	private const int ClosingHour = 22;
	private const int MaxReservationHours = 12;
	private const int MaxSelectableSeats = 5;
	private const decimal UsdToPhpRate = 56m;

	private readonly AppDataStore _dataStore = new();
	private readonly Dictionary<string, Button> _seatButtons = new();
	private readonly Dictionary<string, decimal> _seatRates = new();
	private static readonly IReadOnlyList<SeatPlacement> SeatLayout = CreateSeatLayout();
	private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF3B30"));
	private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34C759"));
	private static readonly IBrush AvailableSeatBrush = new SolidColorBrush(Color.Parse("#E5E5EA"));
	private static readonly IBrush ReservedSeatBrush = new SolidColorBrush(Color.Parse("#B0B0B7"));
	private static readonly IBrush SelectedSeatBrush = new SolidColorBrush(Color.Parse("#0A84FF"));
	private static readonly IBrush SeatTextBrush = new SolidColorBrush(Color.Parse("#1D1D1F"));
	private static readonly IBrush SelectedSeatTextBrush = Brushes.White;

	private string? _currentUser;
	private readonly HashSet<string> _selectedSeats = new(StringComparer.OrdinalIgnoreCase);

	public MainWindow()
	{
		InitializeComponent();

		InitializeSeatRates();
		InitializeReservationInputs();
		BuildSeatGrid();
		RefreshReservationViews();
	}

	private void InitializeSeatRates()
	{
		foreach (var seat in SeatLayout)
		{
			_seatRates[seat.SeatId] = seat.HourlyRate;
		}
	}

	private void InitializeReservationInputs()
	{
		ReservationDatePicker.SelectedDate = DateTimeOffset.Now.Date;

		StartHourComboBox.ItemsSource = Enumerable.Range(OpeningHour, (LastStartHour - OpeningHour) + 1)
			.Select(hour => $"{hour:00}:00")
			.ToList();
		StartHourComboBox.SelectedIndex = 0;

		UpdateEndTimeOptions();

	}
	}
}