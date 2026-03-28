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
	private const double BaseSeatPlanScale = 1.25;
	private const double BaseCanvasWidth = 1450;
	private const double BaseCanvasHeight = 680;
	private const double CanvasBottomScrollBuffer = 56;
	private const double MinSeatPlanFitScale = 0.65;
	private const double MaxSeatPlanFitScale = 1.08;
	private const int OpeningHour = 8;
	private const int LastStartHour = 20;
	private const int ClosingHour = 22;
	private const int MaxReservationHours = 12;
	private const int MaxSelectableSeats = 5;
	private const decimal UsdToPhpRate = 56m;

	private readonly AppDataStore _dataStore = new();
	private readonly Dictionary<string, Button> _seatButtons = new();
	private readonly Dictionary<string, decimal> _seatRates = new();
	private double _seatPlanFitScale = 1.0;
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
		Opened += MainWindow_Opened;
		SeatPlanScrollViewer.SizeChanged += SeatPlanScrollViewer_SizeChanged;

		InitializeSeatRates();
		InitializeReservationInputs();
		BuildSeatGrid();
		RefreshReservationViews();
	}

	private void MainWindow_Opened(object? sender, EventArgs e)
	{
		UpdateSeatPlanScaleAndRefresh();
	}

	private void SeatPlanScrollViewer_SizeChanged(object? sender, SizeChangedEventArgs e)
	{
		UpdateSeatPlanScaleAndRefresh();
	}

	private void UpdateSeatPlanScaleAndRefresh()
	{
		if (SeatPlanScrollViewer.Bounds.Width <= 0 || SeatPlanScrollViewer.Bounds.Height <= 0)
		{
			return;
		}

		var widthScale = SeatPlanScrollViewer.Bounds.Width / BaseCanvasWidth;
		var heightScale = SeatPlanScrollViewer.Bounds.Height / BaseCanvasHeight;
		var fitScale = Math.Clamp(Math.Min(widthScale, heightScale), MinSeatPlanFitScale, MaxSeatPlanFitScale);

		if (Math.Abs(_seatPlanFitScale - fitScale) < 0.03)
		{
			return;
		}

		_seatPlanFitScale = fitScale;
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

	private void BuildSeatGrid()
	{
		SeatCanvas.Children.Clear();
		_seatButtons.Clear();

		var seatPlanScale = BaseSeatPlanScale * _seatPlanFitScale;
		var seatButtonWidth = Math.Max(40, 60 * _seatPlanFitScale);
		var seatButtonHeight = Math.Max(36, 54 * _seatPlanFitScale);
		var seatFontSize = Math.Max(11, 14 * _seatPlanFitScale);

		SeatCanvas.Width = BaseCanvasWidth * _seatPlanFitScale;
		SeatCanvas.Height = (BaseCanvasHeight * _seatPlanFitScale) + CanvasBottomScrollBuffer;

		AddFloorPlanDecor();

		foreach (var seat in SeatLayout)
		{
			var button = new Button
			{
				Content = seat.SeatId,
				Tag = seat.SeatId,
				Width = seatButtonWidth,
				Height = seatButtonHeight,
				MinHeight = seatButtonHeight,
				Padding = new Thickness(0),
				FontWeight = FontWeight.SemiBold,
				FontSize = seatFontSize,
				HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
				VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			button.Click += SeatButton_Click;
			_seatButtons[seat.SeatId] = button;

			Canvas.SetLeft(button, seat.Left * seatPlanScale);
			Canvas.SetTop(button, seat.Top * seatPlanScale);
			SeatCanvas.Children.Add(button);
		}
	}

	private void AddFloorPlanDecor()
	{
		var seatPlanScale = BaseSeatPlanScale * _seatPlanFitScale;
		var decorScale = _seatPlanFitScale;

		var cashierBox = new Border
		{
			Width = 270 * decorScale,
			Height = 120 * decorScale,
			CornerRadius = new CornerRadius(16 * decorScale),
			BorderThickness = new Thickness(2),
			BorderBrush = new SolidColorBrush(Color.Parse("#8E8E93")),
			Background = new SolidColorBrush(Color.Parse("#F5F5F7")),
			Child = new TextBlock
			{
				Text = "CASHIER",
				FontSize = Math.Max(14, 32 * decorScale),
				FontWeight = FontWeight.Bold,
				Foreground = new SolidColorBrush(Color.Parse("#3A3A3C")),
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			}
		};

		Canvas.SetLeft(cashierBox, 290 * seatPlanScale);
		Canvas.SetTop(cashierBox, 65 * seatPlanScale);
		SeatCanvas.Children.Add(cashierBox);

		var doorwayIndicator = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new Border
				{
					Width = 210 * decorScale,
					Height = Math.Max(8, 12 * decorScale),
					CornerRadius = new CornerRadius(6 * decorScale),
					Background = new SolidColorBrush(Color.Parse("#C7C7CC"))
				},
				new TextBlock
				{
					Text = "DOORWAY",
					FontSize = Math.Max(11, 17 * decorScale),
					FontWeight = FontWeight.SemiBold,
					Foreground = new SolidColorBrush(Color.Parse("#3A3A3C")),
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
				}
			}
		};

		Canvas.SetLeft(doorwayIndicator, 320 * seatPlanScale);
		Canvas.SetTop(doorwayIndicator, (210 * seatPlanScale) + (120 * decorScale));
		SeatCanvas.Children.Add(doorwayIndicator);

		var guide = new TextBlock
		{
			Text = "Layout follows the in-store floor plan.",
			FontSize = Math.Max(10, 16 * decorScale),
			Foreground = new SolidColorBrush(Color.Parse("#6E6E73"))
		};

		Canvas.SetLeft(guide, 20 * seatPlanScale);
		Canvas.SetTop(guide, 20 * seatPlanScale);
		SeatCanvas.Children.Add(guide);
	}

	private static IReadOnlyList<SeatPlacement> CreateSeatLayout()
	{
		var seats = new List<SeatPlacement>();

		// Left upper area: two 4-seat rows plus one 4-seat row near the corridor.
		AddSeatBlock(seats, "LA", startIndex: 1, startX: 35, startY: 120, rows: 2, columns: 4, rate: ConvertUsdToPhp(4.00m));
		AddSeatBlock(seats, "LB", startIndex: 1, startX: 35, startY: 280, rows: 1, columns: 4, rate: ConvertUsdToPhp(4.00m));

		// Right upper area: two full rows and one short row beside cashier.
		AddSeatBlock(seats, "UA", startIndex: 1, startX: 570, startY: 100, rows: 2, columns: 4, rate: ConvertUsdToPhp(4.00m));
		AddSeatBlock(seats, "UB", startIndex: 1, startX: 686, startY: 210, rows: 1, columns: 2, rate: ConvertUsdToPhp(4.00m));

		// Lower-right wing: matches the provided layout (left vertical line, three 2x3 blocks, bottom row).
		AddSeatBlock(seats, "LW", startIndex: 1, startX: 500, startY: 318, rows: 4, columns: 1, rate: ConvertUsdToPhp(3.00m), rowStep: 64);
		AddSeatBlock(seats, "RA", startIndex: 1, startX: 620, startY: 285, rows: 3, columns: 2, rate: ConvertUsdToPhp(3.00m), columnStep: 62, rowStep: 62);
		AddSeatBlock(seats, "RB", startIndex: 1, startX: 800, startY: 285, rows: 3, columns: 2, rate: ConvertUsdToPhp(3.00m), columnStep: 62, rowStep: 62);
		AddSeatBlock(seats, "RC", startIndex: 1, startX: 980, startY: 285, rows: 3, columns: 2, rate: ConvertUsdToPhp(3.00m), columnStep: 62, rowStep: 62);
		AddSeatBlock(seats, "BR", startIndex: 1, startX: 560, startY: 506, rows: 1, columns: 9, rate: ConvertUsdToPhp(3.00m), columnStep: 62);

		return seats;
	}

	private static void AddSeatBlock(
		List<SeatPlacement> seats,
		string prefix,
		int startIndex,
		double startX,
		double startY,
		int rows,
		int columns,
		decimal rate,
		double columnStep = 58,
		double rowStep = 58)
	{
		var index = startIndex;

		for (var row = 0; row < rows; row++)
		{
			for (var col = 0; col < columns; col++)
			{
				var seatId = $"{prefix}{index:00}";
				seats.Add(new SeatPlacement(
					seatId,
					startX + (col * columnStep),
					startY + (row * rowStep),
					rate));
				index++;
			}
		}

	}

	private sealed class SeatPlacement
	{
		public SeatPlacement(string seatId, double left, double top, decimal hourlyRate)
		{
			SeatId = seatId;
			Left = left;
			Top = top;
			HourlyRate = hourlyRate;
		}

		public string SeatId { get; }
		public double Left { get; }
		public double Top { get; }
		public decimal HourlyRate { get; }
	}

	private void LoginButton_Click(object? sender, RoutedEventArgs e)
	{
		var username = (LoginUsernameTextBox.Text ?? string.Empty).Trim();
		var password = LoginPasswordTextBox.Text ?? string.Empty;

		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			LoginMessageTextBlock.Text = "Please provide both username and password.";
			return;
		}

		if (!_dataStore.ValidateLogin(username, password))
		{
			LoginMessageTextBlock.Text = "Invalid username or password.";
			return;
		}

		LoginMessageTextBlock.Text = string.Empty;
		_currentUser = username;
		EnterBookingPanel();
	}

	private void SignupButton_Click(object? sender, RoutedEventArgs e)
	{
		var username = (SignupUsernameTextBox.Text ?? string.Empty).Trim();
		var email = (SignupEmailTextBox.Text ?? string.Empty).Trim();
		var password = SignupPasswordTextBox.Text ?? string.Empty;
		var confirm = SignupConfirmPasswordTextBox.Text ?? string.Empty;

		SignupMessageTextBlock.Foreground = ErrorBrush;

		if (username.Length < 3)
		{
			SignupMessageTextBlock.Text = "Username must be at least 3 characters long.";
			return;
		}

		if (password.Length < 6)
		{
			SignupMessageTextBlock.Text = "Password must be at least 6 characters long.";
			return;
		}

		if (!IsEmailAddressValid(email))
		{
			SignupMessageTextBlock.Text = "Please provide a valid email address.";
			return;
		}

		if (password != confirm)
		{
			SignupMessageTextBlock.Text = "Passwords do not match.";
			return;
		}

		if (!_dataStore.SignUp(username, email, password, out var errorMessage))
		{
			SignupMessageTextBlock.Text = errorMessage;
			return;
		}

		SignupMessageTextBlock.Foreground = SuccessBrush;
		SignupMessageTextBlock.Text = "Account created. You can now log in.";

		SignupUsernameTextBox.Text = string.Empty;
		SignupEmailTextBox.Text = string.Empty;
		SignupPasswordTextBox.Text = string.Empty;
		SignupConfirmPasswordTextBox.Text = string.Empty;
	}

	private void LogoutButton_Click(object? sender, RoutedEventArgs e)
	{
		_currentUser = null;
		_selectedSeats.Clear();

		AuthPanel.IsVisible = true;
		BookingPanel.IsVisible = false;

		ClearAuthInputs();
		RefreshReservationViews();
	}

	private void EnterBookingPanel()
	{
		WelcomeTextBlock.Text = $"Welcome, {_currentUser}. Reserve your seat below.";

		AuthPanel.IsVisible = false;
		BookingPanel.IsVisible = true;

		_selectedSeats.Clear();
		RefreshReservationViews();
	}

	private void ClearAuthInputs()
	{
		LoginUsernameTextBox.Text = string.Empty;
		LoginPasswordTextBox.Text = string.Empty;
		SignupUsernameTextBox.Text = string.Empty;
		SignupEmailTextBox.Text = string.Empty;
		SignupPasswordTextBox.Text = string.Empty;
		SignupConfirmPasswordTextBox.Text = string.Empty;
		LoginMessageTextBlock.Text = string.Empty;
		SignupMessageTextBlock.Text = string.Empty;
		SignupMessageTextBlock.Foreground = ErrorBrush;
	}

	private static bool IsEmailAddressValid(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return false;
		}

		var atIndex = email.IndexOf('@');
		if (atIndex <= 0 || atIndex != email.LastIndexOf('@'))
		{
			return false;
		}

		var dotAfterAt = email.IndexOf('.', atIndex + 2);
		return dotAfterAt > atIndex + 1 && dotAfterAt < email.Length - 1;
	}

	private void SeatButton_Click(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not string seatId)
		{
			return;
		}

		if (_selectedSeats.Contains(seatId))
		{
			_selectedSeats.Remove(seatId);
			RefreshReservationViews();
			return;
		}

		if (_selectedSeats.Count >= MaxSelectableSeats)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = $"You can only select up to {MaxSelectableSeats} seats per reservation.";
			return;
		}

		if (TryGetSelectedReservationSlot(out var date, out var startHour, out _, out var durationHours))
		{
			var reservations = _dataStore.GetReservationsForDate(date);
			var isUnavailable = reservations.Any(r =>
				r.SeatId == seatId
				&& TimesOverlap(r.StartHour, r.DurationHours, startHour, durationHours));

			if (isUnavailable)
			{
				SeatStatusTextBlock.Foreground = ErrorBrush;
				SeatStatusTextBlock.Text = $"Seat {seatId} is unavailable for the selected slot.";
				return;
			}
		}

		_selectedSeats.Add(seatId);
		RefreshReservationViews();
	}

	private void ReservationDatePicker_SelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
	{
		RefreshReservationViews();
	}

	private void ReservationInputs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender == StartHourComboBox)
		{
			UpdateEndTimeOptions();
		}

		RefreshReservationViews();
	}

	private async void ReserveButton_Click(object? sender, RoutedEventArgs e)
	{
		if (_currentUser is null)
		{
			SeatStatusTextBlock.Text = "Please log in first.";
			return;
		}

		if (!_selectedSeats.Any())
		{
			SeatStatusTextBlock.Text = "Select at least one seat from the seat plan first.";
			return;
		}

		if (_selectedSeats.Count > MaxSelectableSeats)
		{
			SeatStatusTextBlock.Text = $"You can only reserve up to {MaxSelectableSeats} seats at once.";
			return;
		}

		if (!TryGetSelectedReservationSlot(out var date, out var startHour, out _, out var durationHours))
		{
			SeatStatusTextBlock.Text = "Please choose a valid date, start time, and end time.";
			return;
		}

		var unavailableSeats = GetUnavailableSelectedSeats(date, startHour, durationHours);
		if (unavailableSeats.Any())
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = $"These selected seats are unavailable: {string.Join(", ", unavailableSeats)}.";
			return;
		}

		var selectedSeatIds = _selectedSeats.OrderBy(seatId => seatId).ToList();
		var totalCost = selectedSeatIds.Sum(seatId =>
		{
			var rate = GetSeatRate(seatId);
			return CalculateTotalCost(rate, durationHours, date);
		});

		var paymentDetails = await ShowPaymentDialogAsync(totalCost);
		if (paymentDetails is null)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Payment canceled. Reservation not saved.";
			return;
		}

		foreach (var seatId in selectedSeatIds)
		{
			var hourlyRate = GetSeatRate(seatId);
			var reservation = new Reservation
			{
				ReservationId = Guid.NewGuid().ToString("N"),
				Username = _currentUser,
				SeatId = seatId,
				ReservationDate = date,
				StartHour = startHour,
				// Stored as duration for conflict checks and data compatibility.
				DurationHours = durationHours,
				HourlyRate = hourlyRate,
				TotalCost = CalculateTotalCost(hourlyRate, durationHours, date),
				PaymentMethod = paymentDetails.Method
			};

			if (!_dataStore.TryCreateReservation(reservation, out var errorMessage))
			{
				SeatStatusTextBlock.Foreground = ErrorBrush;
				SeatStatusTextBlock.Text = errorMessage;
				RefreshReservationViews();
				return;
			}
		}

		SeatStatusTextBlock.Foreground = SuccessBrush;
		SeatStatusTextBlock.Text = $"Reservation confirmed for {selectedSeatIds.Count} seat(s) on {date:MMMM dd, yyyy}.";
		_selectedSeats.Clear();
		RefreshReservationViews();

		await ShowInfoDialogAsync(
			"Reservation Successful",
			"Reserved Successfully. Please check your email for receipt.");
	}

	private void RefreshReservationViews()
	{
		var selectedDate = GetSelectedDate();
		var reservations = _dataStore.GetReservationsForDate(selectedDate);

		UpdateSeatButtons(reservations);
		UpdateReservationList(reservations);
		UpdateMyBookingLists();
		UpdateDetailPanel();
	}

	private void UpdateSeatButtons(IReadOnlyList<Reservation> dayReservations)
	{
		TryGetSelectedReservationSlot(out _, out var startHour, out _, out var durationHours);

		foreach (var (seatId, button) in _seatButtons)
		{
			var hasConflict = dayReservations.Any(r =>
				r.SeatId == seatId
				&& TimesOverlap(r.StartHour, r.DurationHours, startHour, durationHours));

			var background = hasConflict ? ReservedSeatBrush : AvailableSeatBrush;
			var foreground = SeatTextBrush;

			if (_selectedSeats.Contains(seatId))
			{
				background = SelectedSeatBrush;
				foreground = SelectedSeatTextBrush;
			}

			button.Background = background;
			button.Foreground = foreground;
		}
	}

	private void UpdateReservationList(IReadOnlyList<Reservation> dayReservations)
	{
		var items = dayReservations
			.OrderBy(r => r.SeatId)
			.ThenBy(r => r.StartHour)
			.Select(r =>
				$"Seat {r.SeatId} | {r.StartHour:00}:00-{r.StartHour + r.DurationHours:00}:00 | {r.Username} | {r.PaymentMethod} | PHP {r.TotalCost:F2}")
			.ToList();

		ReservationsListBox.ItemsSource = items.Any()
			? items
			: new List<string> { "No reservations yet for this date." };
	}

	private void UpdateMyBookingLists()
	{
		if (string.IsNullOrWhiteSpace(_currentUser))
		{
			var signedOutMessage = new List<string> { "Log in to view your bookings." };
			MyUpcomingBookingsListBox.ItemsSource = signedOutMessage;
			MyPastBookingsListBox.ItemsSource = signedOutMessage;
			return;
		}

		var now = DateTime.Now;
		var userReservations = _dataStore.GetReservationsForUser(_currentUser);

		var upcomingItems = userReservations
			.Where(reservation => GetReservationEndDateTime(reservation) > now)
			.OrderBy(reservation => reservation.ReservationDate)
			.ThenBy(reservation => reservation.StartHour)
			.Select(FormatUserBookingItem)
			.ToList();

		var pastItems = userReservations
			.Where(reservation => GetReservationEndDateTime(reservation) <= now)
			.OrderByDescending(reservation => reservation.ReservationDate)
			.ThenByDescending(reservation => reservation.StartHour)
			.Select(FormatUserBookingItem)
			.ToList();

		MyUpcomingBookingsListBox.ItemsSource = upcomingItems.Any()
			? upcomingItems
			: new List<string> { "No upcoming bookings." };

		MyPastBookingsListBox.ItemsSource = pastItems.Any()
			? pastItems
			: new List<string> { "No past bookings yet." };
	}

	private static string FormatUserBookingItem(Reservation reservation)
	{
		var endHour = reservation.StartHour + reservation.DurationHours;
		return $"{reservation.ReservationDate:MMM dd, yyyy} | {reservation.StartHour:00}:00-{endHour:00}:00 | Seat {reservation.SeatId} | {reservation.PaymentMethod} | PHP {reservation.TotalCost:F2}";
	}

	private static DateTime GetReservationEndDateTime(Reservation reservation)
	{
		var startDateTime = reservation.ReservationDate.ToDateTime(new TimeOnly(reservation.StartHour, 0));
		return startDateTime.AddHours(reservation.DurationHours);
	}

	private void UpdateDetailPanel()
	{
		var selectedSeatIds = _selectedSeats.OrderBy(seatId => seatId).ToList();
		SelectedSeatTextBlock.Text = selectedSeatIds.Any()
			? string.Join(", ", selectedSeatIds)
			: "None";

		if (!selectedSeatIds.Any())
		{
			SeatRateTextBlock.Text = "-";
			CostSummaryTextBlock.Text = $"Select up to {MaxSelectableSeats} seats to see pricing details.";
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "No seats selected.";
			return;
		}

		var date = GetSelectedDate();
		var hasSlot = TryGetSelectedReservationSlot(out _, out var startHour, out var endHour, out var durationHours);
		var hourlyRates = selectedSeatIds.Select(GetSeatRate).ToList();
		var minRate = hourlyRates.Min();
		var maxRate = hourlyRates.Max();
		var total = selectedSeatIds.Sum(seatId => CalculateTotalCost(GetSeatRate(seatId), durationHours, date));
		var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

		SeatRateTextBlock.Text = minRate == maxRate
			? $"PHP {minRate:F2}/hour per seat"
			: $"PHP {minRate:F2} - PHP {maxRate:F2}/hour per seat";
		CostSummaryTextBlock.Text = hasSlot
			? $"{selectedSeatIds.Count} seat(s) | {startHour:00}:00-{endHour:00}:00 ({durationHours} hour(s)) {(isWeekend ? "+ weekend surcharge" : string.Empty)} | Total PHP {total:F2}"
			: "Choose a valid date and time to calculate cost.";

		var reservations = _dataStore.GetReservationsForDate(date);
		var occupiedSeats = hasSlot
			? selectedSeatIds
				.Where(seatId => reservations.Any(r =>
					r.SeatId == seatId
					&& TimesOverlap(r.StartHour, r.DurationHours, startHour, durationHours)))
				.ToList()
			: new List<string>();

		if (occupiedSeats.Any())
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = $"Unavailable for this slot: {string.Join(", ", occupiedSeats)}.";
		}
		else
		{
			SeatStatusTextBlock.Foreground = SuccessBrush;
			SeatStatusTextBlock.Text = $"{selectedSeatIds.Count} selected seat(s) are available for this slot.";
		}
	}

	private List<string> GetUnavailableSelectedSeats(DateOnly date, int startHour, int durationHours)
	{
		var reservations = _dataStore.GetReservationsForDate(date);
		return _selectedSeats
			.Where(seatId => reservations.Any(r =>
				r.SeatId == seatId
				&& TimesOverlap(r.StartHour, r.DurationHours, startHour, durationHours)))
			.OrderBy(seatId => seatId)
			.ToList();
	}

	private async Task<PaymentDetails?> ShowPaymentDialogAsync(decimal totalAmount)
	{
		var paymentMethodComboBox = new ComboBox
		{
			ItemsSource = new List<string> { "Credit Card", "GCash", "Maya" },
			SelectedIndex = 0,
			Width = 360
		};

		var accountNameTextBox = new TextBox
		{
			Watermark = "Account Name",
			Width = 360
		};

		var accountNumberTextBox = new TextBox
		{
			Watermark = "Card Number (16 digits)",
			Width = 360
		};

		var secondaryDetailTextBox = new TextBox
		{
			Watermark = "CVV (3 digits)",
			Width = 360
		};

		var errorTextBlock = new TextBlock
		{
			Foreground = ErrorBrush,
			TextWrapping = Avalonia.Media.TextWrapping.Wrap,
			Width = 360
		};

		paymentMethodComboBox.SelectionChanged += (_, _) =>
		{
			if (paymentMethodComboBox.SelectedItem is not string selectedMethod)
			{
				return;
			}

			if (selectedMethod == "Credit Card")
			{
				accountNumberTextBox.Watermark = "Card Number (13-19 digits)";
				secondaryDetailTextBox.Watermark = "CVV (3-4 digits)";
			}
			else
			{
				accountNumberTextBox.Watermark = "Mobile Number";
				secondaryDetailTextBox.Watermark = "Reference Number (optional)";
			}

			errorTextBlock.Text = string.Empty;
		};

		var payButton = new Button
		{
			Content = "Pay Now",
			Background = new SolidColorBrush(Color.Parse("#0A84FF")),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.Parse("#0A84FF")),
			Padding = new Thickness(16, 10)
		};

		var cancelButton = new Button
		{
			Content = "Cancel",
			Padding = new Thickness(16, 10)
		};

		var dialog = new Window
		{
			Title = "Payment",
			Width = 560,
			Height = 560,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Content = new Border
			{
				Padding = new Thickness(28),
				Child = new StackPanel
				{
					Spacing = 12,
					Children =
					{
						new TextBlock
						{
							Text = "Complete Payment",
							FontSize = 22,
							FontWeight = FontWeight.Bold
						},
						new TextBlock
						{
							Text = $"Total Amount: PHP {totalAmount:F2}",
							FontWeight = FontWeight.SemiBold
						},
						new TextBlock { Text = "Payment Method" },
						paymentMethodComboBox,
						new TextBlock { Text = "Account Name" },
						accountNameTextBox,
						new TextBlock { Text = "Account Number" },
						accountNumberTextBox,
						secondaryDetailTextBox,
						errorTextBlock,
						new StackPanel
						{
							Orientation = Avalonia.Layout.Orientation.Horizontal,
							Spacing = 10,
							HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
							Children = { cancelButton, payButton }
						}
					}
				}
			}
		};

		var completionSource = new TaskCompletionSource<PaymentDetails?>();

		cancelButton.Click += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(null);
			}

			dialog.Close();
		};

		payButton.Click += (_, _) =>
		{
			if (paymentMethodComboBox.SelectedItem is not string method)
			{
				errorTextBlock.Text = "Please choose a payment method.";
				return;
			}

			var accountName = (accountNameTextBox.Text ?? string.Empty).Trim();
			var accountNumber = (accountNumberTextBox.Text ?? string.Empty).Trim();
			var secondaryDetail = (secondaryDetailTextBox.Text ?? string.Empty).Trim();

			if (string.IsNullOrWhiteSpace(accountName))
			{
				errorTextBlock.Text = "Account name is required.";
				return;
			}

			if (string.IsNullOrWhiteSpace(accountNumber))
			{
				errorTextBlock.Text = "Account number is required.";
				return;
			}

			if (!IsPaymentInputValid(method, accountNumber, secondaryDetail, out var validationError))
			{
				errorTextBlock.Text = validationError;
				return;
			}

			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(new PaymentDetails(method, accountName, accountNumber, secondaryDetail));
			}

			dialog.Close();
		};

		dialog.Closed += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(null);
			}
		};

		await dialog.ShowDialog(this);
		return await completionSource.Task;
	}

	private static bool IsPaymentInputValid(string method, string accountNumber, string secondaryDetail, out string error)
	{
		error = string.Empty;
		var numberDigits = new string(accountNumber.Where(char.IsDigit).ToArray());

		if (method == "Credit Card")
		{
			if (numberDigits.Length < 13 || numberDigits.Length > 19)
			{
				error = "Card number must be between 13 and 19 digits.";
				return false;
			}

			var cvvDigits = new string((secondaryDetail ?? string.Empty).Where(char.IsDigit).ToArray());
			if (cvvDigits.Length < 3 || cvvDigits.Length > 4)
			{
				error = "CVV must be 3 or 4 digits.";
				return false;
			}

			return true;
		}

		if (numberDigits.Length < 10 || numberDigits.Length > 13)
		{
			error = "Mobile number must be between 10 and 13 digits.";
			return false;
		}

		return true;
	}

	private async Task ShowInfoDialogAsync(string title, string message)
	{
		var okButton = new Button
		{
			Content = "OK",
			Padding = new Thickness(16, 10),
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
		};

		var dialog = new Window
		{
			Title = title,
			Width = 420,
			Height = 220,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Content = new Border
			{
				Padding = new Thickness(20),
				Child = new StackPanel
				{
					Spacing = 14,
					Children =
					{
						new TextBlock
						{
							Text = message,
							TextWrapping = Avalonia.Media.TextWrapping.Wrap,
							HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
						},
						okButton
					}
				}
			}
		};

		okButton.Click += (_, _) => dialog.Close();

		await dialog.ShowDialog(this);
	}

	private sealed record PaymentDetails(string Method, string AccountName, string AccountNumber, string SecondaryDetail);

	private bool TryGetSelectedReservationSlot(out DateOnly date, out int startHour, out int endHour, out int durationHours)
	{
		date = GetSelectedDate();
		startHour = OpeningHour;
		endHour = OpeningHour + 1;
		durationHours = 1;

		if (StartHourComboBox.SelectedItem is not string startText
			|| EndHourComboBox.SelectedItem is not string endText)
		{
			return false;
		}

		if (!int.TryParse(startText[..2], out startHour))
		{
			return false;
		}

		if (!int.TryParse(endText[..2], out endHour))
		{
			return false;
		}

		durationHours = endHour - startHour;

		if (durationHours < 1 || durationHours > MaxReservationHours)
		{
			return false;
		}

		return true;
	}

	private void UpdateEndTimeOptions()
	{
		if (StartHourComboBox.SelectedItem is not string startText
			|| !int.TryParse(startText[..2], out var startHour))
		{
			EndHourComboBox.ItemsSource = new List<string>();
			return;
		}

		var latestEndHour = Math.Min(startHour + MaxReservationHours, ClosingHour);
		var options = Enumerable.Range(startHour + 1, latestEndHour - startHour)
			.Select(hour => $"{hour:00}:00")
			.ToList();

		var previousSelection = EndHourComboBox.SelectedItem as string;
		EndHourComboBox.ItemsSource = options;

		if (previousSelection is not null && options.Contains(previousSelection))
		{
			EndHourComboBox.SelectedItem = previousSelection;
		}
		else
		{
			EndHourComboBox.SelectedIndex = 0;
		}
	}

	private DateOnly GetSelectedDate()
	{
		var selectedDate = ReservationDatePicker.SelectedDate?.DateTime.Date ?? DateTime.Today;
		return DateOnly.FromDateTime(selectedDate);
	}

	private decimal GetSeatRate(string seatId)
	{
		return _seatRates.GetValueOrDefault(seatId, ConvertUsdToPhp(3.00m));
	}

	private static decimal ConvertUsdToPhp(decimal usdAmount)
	{
		return Math.Round(usdAmount * UsdToPhpRate, 2);
	}

	private static decimal CalculateTotalCost(decimal hourlyRate, int duration, DateOnly date)
	{
		var baseCost = hourlyRate * duration;
		var surcharge = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.15m : 0m;
		return Math.Round(baseCost * (1 + surcharge), 2);
	}

	private static bool TimesOverlap(int startA, int durationA, int startB, int durationB)
	{
		var endA = startA + durationA;
		var endB = startB + durationB;
		return startA < endB && startB < endA;
	}
}