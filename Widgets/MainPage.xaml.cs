using MauiAppleWidgets.Services;

namespace MauiAppleWidgets;

public partial class MainPage : ContentPage
{
	private readonly IWidgetDataService _widgetService;

	public MainPage(IWidgetDataService widgetService)
	{
		InitializeComponent();
		_widgetService = widgetService;
		BindingContext = this;
	}

	private int _counter;
	public int Counter
	{
		get => _counter;
		set
		{
			if (_counter == value) return;
			_counter = value;
			OnPropertyChanged();
		}
	}

	private string _statusMessage = string.Empty;
	public string StatusMessage
	{
		get => _statusMessage;
		set
		{
			_statusMessage = value;
			OnPropertyChanged();
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		LoadIncomingWidgetData();
	}

	/// <summary>Called when the app resumes from background.</summary>
	public void OnResumed()
	{
		LoadIncomingWidgetData();
	}

	/// <summary>Called when the app is opened via a deep link from the widget.</summary>
	public void OnResumedByUrl(int incomingCounter)
	{
		Counter = incomingCounter;
		StatusMessage = "Updated via widget tap";
		SyncOutgoingData();
	}

	private void LoadIncomingWidgetData()
	{
		var incoming = _widgetService.ReadDataFromWidget();
		if (incoming != null)
		{
			Counter = incoming.Counter;
			StatusMessage = $"Updated by widget: {incoming.Message}";
			_widgetService.ClearWidgetIncomingData();
			SyncOutgoingData();
		}
		else
		{
			StatusMessage = string.Empty;
		}
	}

	private void OnAddClicked(object? sender, EventArgs e)
	{
		Counter++;
		StatusMessage = string.Empty;
		SyncOutgoingData();
		_widgetService.RefreshWidget(WidgetConstants.WidgetKind);
	}

	private void OnSubtractClicked(object? sender, EventArgs e)
	{
		Counter--;
		StatusMessage = string.Empty;
		SyncOutgoingData();
		_widgetService.RefreshWidget(WidgetConstants.WidgetKind);
	}

	private void SyncOutgoingData()
	{
		_widgetService.SendDataToWidget(new WidgetData
		{
			Counter = Counter,
			Title = "MauiAppleWidgets",
			Message = "Sent from app",
			UpdatedAt = DateTime.UtcNow.ToString("o")
		});
	}
}
