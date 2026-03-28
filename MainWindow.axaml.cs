using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
	private const int WeekdayOpeningHour = 13;
	private const int WeekendOpeningHour = 8;
	private const int ClosingHourNextDayAbsolute = 29;
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
	private ReceiptSnapshot? _latestReceipt;
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
		UpdateStartHourOptions();
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
		_latestReceipt = null;
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

		_latestReceipt = null;
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
		UpdateStartHourOptions();
		UpdateEndTimeOptions();
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
		var bookingCode = GenerateBookingCode(date);
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
				BookingCode = bookingCode,
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
		_latestReceipt = new ReceiptSnapshot(
			bookingCode,
			date,
			startHour,
			durationHours,
			selectedSeatIds,
			paymentDetails.Method,
			totalCost,
			DateTime.Now);
		_selectedSeats.Clear();
		RefreshReservationViews();

		await ShowInfoDialogAsync(
			"Reservation Successful",
			$"Reserved successfully. Your booking code is {bookingCode}.");
	}

	private void RefreshReservationViews()
	{
		var selectedDate = GetSelectedDate();
		var reservations = _dataStore.GetReservationsForDate(selectedDate);

		UpdateSeatButtons(reservations);
		UpdateReservationList(reservations);
		UpdateMyBookingLists();
		UpdateDetailPanel();
		UpdateReceiptPanel();
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
				$"{(string.IsNullOrWhiteSpace(r.BookingCode) ? "-" : r.BookingCode)} | Seat {r.SeatId} | {FormatReservationTimeRange(r.StartHour, r.DurationHours)} | {r.Username} | {r.PaymentMethod} | PHP {r.TotalCost:F2}")
			.ToList();

		ReservationsListBox.ItemsSource = items.Any()
			? items
			: new List<string> { "No reservations yet for this date." };
	}

	private void UpdateReceiptPanel()
	{
		if (_latestReceipt is null)
		{
			ReceiptPanel.IsVisible = false;
			ReceiptExportButton.IsEnabled = false;
			BookingCodeTextBlock.Text = string.Empty;
			ReceiptSummaryTextBlock.Text = string.Empty;
			ReceiptGeneratedOnTextBlock.Text = string.Empty;
			return;
		}

		ReceiptPanel.IsVisible = true;
		ReceiptExportButton.IsEnabled = true;
		BookingCodeTextBlock.Text = $"Booking Reference: {_latestReceipt.BookingCode}";
		ReceiptSummaryTextBlock.Text = BuildReceiptSummary(_latestReceipt);
		ReceiptGeneratedOnTextBlock.Text = $"Paid on {_latestReceipt.PaidAt:MMM dd, yyyy hh:mm tt}";
	}

	private async void ReceiptExportButton_Click(object? sender, RoutedEventArgs e)
	{
		if (_latestReceipt is null)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "No receipt available to export yet.";
			return;
		}

		var saveTarget = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "Export Receipt to PDF",
			SuggestedFileName = $"{_latestReceipt.BookingCode}.pdf",
			DefaultExtension = "pdf",
			ShowOverwritePrompt = true,
			FileTypeChoices = new List<FilePickerFileType>
			{
				new("PDF Document")
				{
					Patterns = new[] { "*.pdf" },
					MimeTypes = new[] { "application/pdf" }
				}
			}
		});

		if (saveTarget is null)
		{
			return;
		}

		try
		{
			var pdfBytes = BuildReceiptPdf(_latestReceipt, _currentUser ?? "Guest");
			await using (var output = await saveTarget.OpenWriteAsync())
			{
				await output.WriteAsync(pdfBytes, 0, pdfBytes.Length);
				await output.FlushAsync();
			}

			var exportedPath = saveTarget.TryGetLocalPath();
			var savedLocation = string.IsNullOrWhiteSpace(exportedPath)
				? saveTarget.Name
				: exportedPath;

			SeatStatusTextBlock.Foreground = SuccessBrush;
			SeatStatusTextBlock.Text = $"Receipt exported: {saveTarget.Name}";

			await ShowInfoDialogAsync(
				"Export Complete",
				$"Receipt PDF saved to:\n{savedLocation}");
		}
		catch (Exception ex)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Unable to export PDF receipt.";

			await ShowInfoDialogAsync(
				"Export Failed",
				$"Failed to export receipt PDF.\n{ex.Message}");
		}
	}

	private static string BuildReceiptSummary(ReceiptSnapshot receipt)
	{
		return $"Seats: {string.Join(", ", receipt.SeatIds)}\n"
			+ $"Date: {receipt.Date:MMMM dd, yyyy}\n"
			+ $"Time: {FormatReservationTimeRange(receipt.StartHour, receipt.DurationHours)}\n"
			+ $"Payment: {receipt.PaymentMethod}\n"
			+ $"Total Paid: PHP {receipt.TotalPaid:F2}";
	}

	private static byte[] BuildReceiptPdf(ReceiptSnapshot receipt, string username)
	{
		var lines = new List<string>
		{
			"JIT Study Hub and Coworking Space",
			"Payment Receipt",
			string.Empty,
			$"Booking Reference: {receipt.BookingCode}",
			$"Customer: {username}",
			$"Date: {receipt.Date:MMMM dd, yyyy}",
			$"Time: {FormatReservationTimeRange(receipt.StartHour, receipt.DurationHours)}",
			$"Seats: {string.Join(", ", receipt.SeatIds)}",
			$"Payment Method: {receipt.PaymentMethod}",
			$"Total Paid: PHP {receipt.TotalPaid:F2}",
			$"Generated: {receipt.PaidAt:MMM dd, yyyy hh:mm tt}"
		};

		var contentStream = BuildPdfContentStream(lines);
		var ascii = Encoding.ASCII;
		var contentLength = ascii.GetByteCount(contentStream);

		var objects = new List<string>
		{
			"1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
			"2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
			"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n",
			$"4 0 obj\n<< /Length {contentLength} >>\nstream\n{contentStream}endstream\nendobj\n",
			"5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
		};

		using var stream = new MemoryStream();

		void WriteText(string value)
		{
			var bytes = ascii.GetBytes(value);
			stream.Write(bytes, 0, bytes.Length);
		}

		WriteText("%PDF-1.4\n");

		var offsets = new List<long> { 0 };
		foreach (var obj in objects)
		{
			offsets.Add(stream.Position);
			WriteText(obj);
		}

		var xrefOffset = stream.Position;
		WriteText($"xref\n0 {objects.Count + 1}\n");
		WriteText("0000000000 65535 f \n");

		for (var i = 1; i < offsets.Count; i++)
		{
			WriteText($"{offsets[i]:D10} 00000 n \n");
		}

		WriteText($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
		WriteText($"startxref\n{xrefOffset}\n%%EOF");

		return stream.ToArray();
	}

	private static string BuildPdfContentStream(IReadOnlyList<string> lines)
	{
		var builder = new StringBuilder();
		builder.Append("BT\n");
		builder.Append("/F1 12 Tf\n");
		builder.Append("72 760 Td\n");

		for (var index = 0; index < lines.Count; index++)
		{
			if (index > 0)
			{
				builder.Append("0 -20 Td\n");
			}

			builder.Append('(');
			builder.Append(EscapePdfText(lines[index]));
			builder.Append(") Tj\n");
		}

		builder.Append("ET\n");
		return builder.ToString();
	}

	private static string EscapePdfText(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}

		return input
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("(", "\\(", StringComparison.Ordinal)
			.Replace(")", "\\)", StringComparison.Ordinal);
	}

	private static string GenerateBookingCode(DateOnly date)
	{
		var token = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
		return $"JIT-{date:yyyyMMdd}-{token}";
	}

	private void UpdateMyBookingLists()
	{
		if (string.IsNullOrWhiteSpace(_currentUser))
		{
			var signedOutMessage = new List<string> { "Log in to view your bookings." };
			MyUpcomingBookingsListBox.ItemsSource = signedOutMessage;
			MyPastBookingsListBox.ItemsSource = signedOutMessage;
			MyUpcomingBookingsListBox.SelectedItem = null;
			MyPastBookingsListBox.SelectedItem = null;
			return;
		}

		var now = DateTime.Now;
		var userReservations = _dataStore.GetReservationsForUser(_currentUser);

		var upcomingItems = userReservations
			.Where(reservation => GetReservationEndDateTime(reservation) > now)
			.OrderBy(reservation => reservation.ReservationDate)
			.ThenBy(reservation => reservation.StartHour)
			.Select(reservation => new UserBookingListItem(reservation, FormatUserBookingItem(reservation)))
			.ToList();

		var pastItems = userReservations
			.Where(reservation => GetReservationEndDateTime(reservation) <= now)
			.OrderByDescending(reservation => reservation.ReservationDate)
			.ThenByDescending(reservation => reservation.StartHour)
			.Select(reservation => new UserBookingListItem(reservation, FormatUserBookingItem(reservation)))
			.ToList();

		MyUpcomingBookingsListBox.ItemsSource = upcomingItems.Any()
			? upcomingItems
			: new List<string> { "No upcoming bookings." };

		MyPastBookingsListBox.ItemsSource = pastItems.Any()
			? pastItems
			: new List<string> { "No past bookings yet." };

		MyUpcomingBookingsListBox.SelectedItem = null;
		MyPastBookingsListBox.SelectedItem = null;
	}

	private static string FormatUserBookingItem(Reservation reservation)
	{
		var bookingCode = string.IsNullOrWhiteSpace(reservation.BookingCode) ? "-" : reservation.BookingCode;
		return $"{bookingCode} | {reservation.ReservationDate:MMM dd, yyyy} | {FormatReservationTimeRange(reservation.StartHour, reservation.DurationHours)} | Seat {reservation.SeatId} | {reservation.PaymentMethod} | PHP {reservation.TotalCost:F2}";
	}

	private static DateTime GetReservationEndDateTime(Reservation reservation)
	{
		var startDateTime = reservation.ReservationDate.ToDateTime(new TimeOnly(reservation.StartHour, 0));
		return startDateTime.AddHours(reservation.DurationHours);
	}

	private void MyBookingsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender == MyUpcomingBookingsListBox && MyUpcomingBookingsListBox.SelectedItem is not null)
		{
			MyPastBookingsListBox.SelectedItem = null;
		}

		if (sender == MyPastBookingsListBox && MyPastBookingsListBox.SelectedItem is not null)
		{
			MyUpcomingBookingsListBox.SelectedItem = null;
		}
	}

	private async void CancelBookingButton_Click(object? sender, RoutedEventArgs e)
	{
		if (!TryGetSelectedUserBooking(out var selectedBooking))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Select one booking from My Upcoming Bookings or My Past Bookings first.";
			return;
		}

		if (string.IsNullOrWhiteSpace(_currentUser))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Please log in first.";
			return;
		}

		if (GetReservationEndDateTime(selectedBooking.Reservation) <= DateTime.Now)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Only upcoming bookings can be canceled.";
			return;
		}

		var confirmed = await ShowConfirmationDialogAsync(
			"Confirm Cancellation",
			$"Cancel Seat {selectedBooking.Reservation.SeatId} on {selectedBooking.Reservation.ReservationDate:MMMM dd, yyyy} at {selectedBooking.Reservation.StartHour:00}:00?");

		if (!confirmed)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Cancellation was not completed.";
			return;
		}

		if (!_dataStore.TryCancelReservation(selectedBooking.Reservation.ReservationId, _currentUser, out var errorMessage))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = errorMessage;
			return;
		}

		SeatStatusTextBlock.Foreground = SuccessBrush;
		SeatStatusTextBlock.Text = $"Booking canceled for Seat {selectedBooking.Reservation.SeatId}.";
		RefreshReservationViews();
	}

	private async void RescheduleBookingButton_Click(object? sender, RoutedEventArgs e)
	{
		if (!TryGetSelectedUserBooking(out var selectedBooking))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Select one booking from My Upcoming Bookings or My Past Bookings first.";
			return;
		}

		if (string.IsNullOrWhiteSpace(_currentUser))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Please log in first.";
			return;
		}

		if (GetReservationEndDateTime(selectedBooking.Reservation) <= DateTime.Now)
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = "Past bookings cannot be rescheduled.";
			return;
		}

		var rescheduleDetails = await ShowRescheduleDialogAsync(selectedBooking.Reservation);
		if (rescheduleDetails is null)
		{
			return;
		}

		var recalculatedTotal = CalculateTotalCost(
			selectedBooking.Reservation.HourlyRate,
			rescheduleDetails.DurationHours,
			rescheduleDetails.Date);

		if (!_dataStore.TryRescheduleReservation(
				selectedBooking.Reservation.ReservationId,
				_currentUser,
				rescheduleDetails.Date,
				rescheduleDetails.StartHour,
				rescheduleDetails.DurationHours,
				recalculatedTotal,
				out var errorMessage))
		{
			SeatStatusTextBlock.Foreground = ErrorBrush;
			SeatStatusTextBlock.Text = errorMessage;
			RefreshReservationViews();
			return;
		}

		SeatStatusTextBlock.Foreground = SuccessBrush;
		SeatStatusTextBlock.Text =
			$"Booking rescheduled: Seat {selectedBooking.Reservation.SeatId} on {rescheduleDetails.Date:MMMM dd, yyyy} at {rescheduleDetails.StartHour:00}:00.";
		RefreshReservationViews();
	}

	private bool TryGetSelectedUserBooking(out UserBookingListItem selectedBooking)
	{
		if (MyUpcomingBookingsListBox.SelectedItem is UserBookingListItem upcoming)
		{
			selectedBooking = upcoming;
			return true;
		}

		if (MyPastBookingsListBox.SelectedItem is UserBookingListItem past)
		{
			selectedBooking = past;
			return true;
		}

		selectedBooking = null!;
		return false;
	}

	private async Task<RescheduleDetails?> ShowRescheduleDialogAsync(Reservation reservation)
	{
		var datePicker = new DatePicker
		{
			SelectedDate = reservation.ReservationDate.ToDateTime(TimeOnly.MinValue),
			Width = 420,
			FontSize = 17
		};

		var startHourComboBox = new ComboBox
		{
			Width = 420,
			FontSize = 17
		};

		var endHourComboBox = new ComboBox
		{
			Width = 420,
			FontSize = 17
		};

		void RefreshEndHourOptions()
		{
			if (startHourComboBox.SelectedItem is not string startText
				|| !TryParseHourValue(startText, out var selectedStartHour))
			{
				endHourComboBox.ItemsSource = new List<string>();
				endHourComboBox.SelectedItem = null;
				return;
			}

			var (_, closingHourAbsolute) = GetOperatingWindow(DateOnly.FromDateTime(datePicker.SelectedDate?.DateTime.Date ?? DateTime.Today));
			var options = Enumerable.Range(selectedStartHour + 1, closingHourAbsolute - selectedStartHour)
				.Select(FormatEndHourOption)
				.ToList();

			var preferredEndHour = selectedStartHour + reservation.DurationHours;
			var preferredSelection = FormatEndHourOption(Math.Min(preferredEndHour, closingHourAbsolute));

			endHourComboBox.ItemsSource = options;
			endHourComboBox.SelectedItem = options.Contains(preferredSelection)
				? preferredSelection
				: options.FirstOrDefault();
		}

		void RefreshStartHourOptions()
		{
			var selectedDate = DateOnly.FromDateTime(datePicker.SelectedDate?.DateTime.Date ?? DateTime.Today);
			var (openingHour, _) = GetOperatingWindow(selectedDate);
			var options = Enumerable.Range(openingHour, 24 - openingHour)
				.Select(hour => $"{hour:00}:00")
				.ToList();

			var preferredStart = $"{reservation.StartHour:00}:00";
			startHourComboBox.ItemsSource = options;
			startHourComboBox.SelectedItem = options.Contains(preferredStart)
				? preferredStart
				: options.FirstOrDefault();
		}

		RefreshStartHourOptions();
		RefreshEndHourOptions();
		startHourComboBox.SelectionChanged += (_, _) => RefreshEndHourOptions();
		datePicker.SelectedDateChanged += (_, _) =>
		{
			RefreshStartHourOptions();
			RefreshEndHourOptions();
		};

		var errorTextBlock = new TextBlock
		{
			Foreground = ErrorBrush,
			TextWrapping = TextWrapping.Wrap,
			Width = 500,
			FontSize = 16
		};

		var saveButton = new Button
		{
			Content = "Save Changes",
			Classes = { "primary" }
		};

		var cancelButton = new Button
		{
			Content = "Cancel",
			Classes = { "secondary" }
		};

		var dialog = new Window
		{
			Title = "Reschedule Booking",
			Width = 680,
			Height = 560,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Content = new Border
			{
				Padding = new Thickness(30),
				Child = new StackPanel
				{
					Spacing = 12,
					Children =
					{
						new TextBlock
						{
							Text = $"Seat {reservation.SeatId} | Current: {reservation.ReservationDate:MMM dd, yyyy} {FormatReservationTimeRange(reservation.StartHour, reservation.DurationHours)}",
							FontWeight = FontWeight.SemiBold,
							TextWrapping = TextWrapping.Wrap,
							FontSize = 19
						},
						new TextBlock { Text = "New Date", FontSize = 17, FontWeight = FontWeight.SemiBold },
						datePicker,
						new TextBlock { Text = "New Start Time", FontSize = 17, FontWeight = FontWeight.SemiBold },
						startHourComboBox,
						new TextBlock { Text = "New End Time", FontSize = 17, FontWeight = FontWeight.SemiBold },
						endHourComboBox,
						errorTextBlock,
						new StackPanel
						{
							Orientation = Avalonia.Layout.Orientation.Horizontal,
							Spacing = 10,
							HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
							Children = { cancelButton, saveButton }
						}
					}
				}
			}
		};

		var completionSource = new TaskCompletionSource<RescheduleDetails?>();

		cancelButton.Click += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(null);
			}

			dialog.Close();
		};

		saveButton.Click += (_, _) =>
		{
			if (datePicker.SelectedDate is null)
			{
				errorTextBlock.Text = "Please choose a date.";
				return;
			}

			if (startHourComboBox.SelectedItem is not string startText
				|| endHourComboBox.SelectedItem is not string endText)
			{
				errorTextBlock.Text = "Please choose valid start and end time values.";
				return;
			}

			if (!TryParseHourValue(startText, out var selectedStartHour)
				|| !TryParseHourValue(endText, out var selectedEndHour))
			{
				errorTextBlock.Text = "Unable to parse selected times.";
				return;
			}

			var selectedDuration = selectedEndHour - selectedStartHour;
			if (selectedDuration <= 0)
			{
				selectedDuration += 24;
			}

			var selectedDate = DateOnly.FromDateTime(datePicker.SelectedDate.Value.DateTime.Date);
			if (!IsReservationSlotValid(selectedDate, selectedStartHour, selectedDuration, out var validationMessage))
			{
				errorTextBlock.Text = validationMessage;
				return;
			}

			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(new RescheduleDetails(selectedDate, selectedStartHour, selectedDuration));
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

	private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
	{
		var yesButton = new Button
		{
			Content = "Yes, Cancel",
			Classes = { "primary" },
			FontSize = 16,
			Padding = new Thickness(18, 12)
		};

		var noButton = new Button
		{
			Content = "No",
			Classes = { "secondary" },
			FontSize = 16,
			Padding = new Thickness(18, 12)
		};

		var dialog = new Window
		{
			Title = title,
			Width = 500,
			Height = 260,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Content = new Border
			{
				Padding = new Thickness(22),
				Child = new StackPanel
				{
					Spacing = 14,
					Children =
					{
						new TextBlock
						{
							Text = message,
							TextWrapping = TextWrapping.Wrap,
							FontSize = 20,
							FontWeight = FontWeight.SemiBold
						},
						new StackPanel
						{
							Orientation = Avalonia.Layout.Orientation.Horizontal,
							Spacing = 10,
							HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
							Children = { noButton, yesButton }
						}
					}
				}
			}
		};

		var completionSource = new TaskCompletionSource<bool>();

		noButton.Click += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(false);
			}

			dialog.Close();
		};

		yesButton.Click += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(true);
			}

			dialog.Close();
		};

		dialog.Closed += (_, _) =>
		{
			if (!completionSource.Task.IsCompleted)
			{
				completionSource.SetResult(false);
			}
		};

		await dialog.ShowDialog(this);
		return await completionSource.Task;
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
		var hasSlot = TryGetSelectedReservationSlot(out _, out var startHour, out _, out var durationHours);
		var timeRange = FormatReservationTimeRange(startHour, durationHours);
		var hourlyRates = selectedSeatIds.Select(GetSeatRate).ToList();
		var minRate = hourlyRates.Min();
		var maxRate = hourlyRates.Max();
		var total = selectedSeatIds.Sum(seatId => CalculateTotalCost(GetSeatRate(seatId), durationHours, date));
		var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

		SeatRateTextBlock.Text = minRate == maxRate
			? $"PHP {minRate:F2}/hour per seat"
			: $"PHP {minRate:F2} - PHP {maxRate:F2}/hour per seat";
		CostSummaryTextBlock.Text = hasSlot
			? $"{selectedSeatIds.Count} seat(s) | {timeRange} ({durationHours} hour(s)) {(isWeekend ? "+ weekend surcharge" : string.Empty)} | Total PHP {total:F2}"
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

	private sealed record ReceiptSnapshot(
		string BookingCode,
		DateOnly Date,
		int StartHour,
		int DurationHours,
		IReadOnlyList<string> SeatIds,
		string PaymentMethod,
		decimal TotalPaid,
		DateTime PaidAt);

	private sealed record PaymentDetails(string Method, string AccountName, string AccountNumber, string SecondaryDetail);
	private sealed record RescheduleDetails(DateOnly Date, int StartHour, int DurationHours);

	private sealed class UserBookingListItem
	{
		public UserBookingListItem(Reservation reservation, string displayText)
		{
			Reservation = reservation;
			DisplayText = displayText;
		}

		public Reservation Reservation { get; }
		public string DisplayText { get; }

		public override string ToString()
		{
			return DisplayText;
		}
	}

	private bool TryGetSelectedReservationSlot(out DateOnly date, out int startHour, out int endHour, out int durationHours)
	{
		date = GetSelectedDate();
		var (openingHour, _) = GetOperatingWindow(date);
		startHour = openingHour;
		endHour = (openingHour + 1) % 24;
		durationHours = 1;

		if (StartHourComboBox.SelectedItem is not string startText
			|| EndHourComboBox.SelectedItem is not string endText)
		{
			return false;
		}

		if (!TryParseHourValue(startText, out startHour))
		{
			return false;
		}

		if (!TryParseHourValue(endText, out endHour))
		{
			return false;
		}

		durationHours = endHour - startHour;
		if (durationHours <= 0)
		{
			durationHours += 24;
		}

		if (!IsReservationSlotValid(date, startHour, durationHours, out _))
		{
			return false;
		}

		return true;
	}

	private void UpdateStartHourOptions()
	{
		var date = GetSelectedDate();
		var (openingHour, _) = GetOperatingWindow(date);
		var options = Enumerable.Range(openingHour, 24 - openingHour)
			.Select(hour => $"{hour:00}:00")
			.ToList();

		var previousSelection = StartHourComboBox.SelectedItem as string;
		StartHourComboBox.ItemsSource = options;

		if (previousSelection is not null && options.Contains(previousSelection))
		{
			StartHourComboBox.SelectedItem = previousSelection;
		}
		else
		{
			StartHourComboBox.SelectedIndex = 0;
		}
	}

	private void UpdateEndTimeOptions()
	{
		if (StartHourComboBox.SelectedItem is not string startText
			|| !TryParseHourValue(startText, out var startHour))
		{
			EndHourComboBox.ItemsSource = new List<string>();
			return;
		}

		var (_, closingHourAbsolute) = GetOperatingWindow(GetSelectedDate());
		var options = Enumerable.Range(startHour + 1, closingHourAbsolute - startHour)
			.Select(FormatEndHourOption)
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

	private static (int OpeningHour, int ClosingHourAbsolute) GetOperatingWindow(DateOnly date)
	{
		var openingHour = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
			? WeekendOpeningHour
			: WeekdayOpeningHour;

		return (openingHour, ClosingHourNextDayAbsolute);
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

		var (openingHour, closingHourAbsolute) = GetOperatingWindow(date);
		if (startHour < openingHour)
		{
			errorMessage = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
				? "Weekend bookings can start from 08:00 onward."
				: "Weekday bookings can start from 13:00 onward.";
			return false;
		}

		if ((startHour + durationHours) > closingHourAbsolute)
		{
			errorMessage = "Selected end time exceeds the 05:00 closing time.";
			return false;
		}

		var startDateTime = date.ToDateTime(new TimeOnly(startHour, 0));
		if (startDateTime < DateTime.Now)
		{
			errorMessage = "Selected reservation start time is in the past.";
			return false;
		}

		return true;
	}

	private static bool TryParseHourValue(string hourText, out int hour)
	{
		hour = 0;

		if (string.IsNullOrWhiteSpace(hourText) || hourText.Length < 2)
		{
			return false;
		}

		return int.TryParse(hourText[..2], out hour);
	}

	private static string FormatEndHourOption(int absoluteHour)
	{
		var hour = absoluteHour % 24;
		var isNextDay = absoluteHour >= 24;
		return isNextDay ? $"{hour:00}:00 (+1d)" : $"{hour:00}:00";
	}

	private static string FormatReservationTimeRange(int startHour, int durationHours)
	{
		var endAbsoluteHour = startHour + durationHours;
		var endHour = endAbsoluteHour % 24;
		var suffix = endAbsoluteHour >= 24 ? " (+1d)" : string.Empty;
		return $"{startHour:00}:00-{endHour:00}:00{suffix}";
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