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

	private void BuildSeatGrid()
	{
		SeatCanvas.Children.Clear();
		_seatButtons.Clear();

		AddFloorPlanDecor();

		foreach (var seat in SeatLayout)
		{
			var button = new Button
			{
				Content = seat.SeatId,
				Tag = seat.SeatId,
				Width = 60,
				Height = 54,
				MinHeight = 54,
				Padding = new Thickness(0),
				FontWeight = FontWeight.SemiBold,
				FontSize = 14,
				HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
				VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			button.Click += SeatButton_Click;
			_seatButtons[seat.SeatId] = button;

			Canvas.SetLeft(button, seat.Left * SeatPlanScale);
			Canvas.SetTop(button, seat.Top * SeatPlanScale);
			SeatCanvas.Children.Add(button);
		}
	}

	private void AddFloorPlanDecor()
	{
		var cashierBox = new Border
		{
			Width = 270,
			Height = 120,
			CornerRadius = new CornerRadius(16),
			BorderThickness = new Thickness(2),
			BorderBrush = new SolidColorBrush(Color.Parse("#8E8E93")),
			Background = new SolidColorBrush(Color.Parse("#F5F5F7")),
			Child = new TextBlock
			{
				Text = "CASHIER",
				FontSize = 32,
				FontWeight = FontWeight.Bold,
				Foreground = new SolidColorBrush(Color.Parse("#3A3A3C")),
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			}
		};

		Canvas.SetLeft(cashierBox, 290 * SeatPlanScale);
		Canvas.SetTop(cashierBox, 65 * SeatPlanScale);
		SeatCanvas.Children.Add(cashierBox);

		var doorwayIndicator = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				new Border
				{
					Width = 210,
					Height = 12,
					CornerRadius = new CornerRadius(6),
					Background = new SolidColorBrush(Color.Parse("#C7C7CC"))
				},
				new TextBlock
				{
					Text = "DOORWAY",
					FontSize = 17,
					FontWeight = FontWeight.SemiBold,
					Foreground = new SolidColorBrush(Color.Parse("#3A3A3C")),
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
				}
			}
		};

		Canvas.SetLeft(doorwayIndicator, 320 * SeatPlanScale);
		Canvas.SetTop(doorwayIndicator, (210 * SeatPlanScale) + 120);
		SeatCanvas.Children.Add(doorwayIndicator);

		var guide = new TextBlock
		{
			Text = "Layout follows the in-store floor plan.",
			FontSize = 16,
			Foreground = new SolidColorBrush(Color.Parse("#6E6E73"))
		};

		Canvas.SetLeft(guide, 20 * SeatPlanScale);
		Canvas.SetTop(guide, 20 * SeatPlanScale);
		SeatCanvas.Children.Add(guide);
	}

	}
}