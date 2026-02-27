using Foundation;
using ObjCRuntime;

namespace MauiAppIntentsSample.Binding
{
	/// <summary>
	/// Data transfer object for passing task data between Swift and C#.
	/// Maps to the Swift BridgeTaskItem class.
	/// </summary>
	[BaseType(typeof(NSObject))]
	interface BridgeTaskItem
	{
		[Export("id")]
		string Id { get; set; }

		[Export("title")]
		string Title { get; set; }

		[Export("notes")]
		string Notes { get; set; }

		[Export("priorityRawValue")]
		nint PriorityRawValue { get; set; }

		[Export("categoryRawValue")]
		nint CategoryRawValue { get; set; }

		[NullAllowed, Export("dueDate")]
		NSDate DueDate { get; set; }

		[Export("estimatedMinutes")]
		nint EstimatedMinutes { get; set; }

		[Export("isCompleted")]
		bool IsCompleted { get; set; }

		[Export("createdAt")]
		NSDate CreatedAt { get; set; }

		[Export("initWithId:title:notes:priorityRawValue:categoryRawValue:dueDate:estimatedMinutes:isCompleted:createdAt:")]
		NativeHandle Constructor(string id, string title, string notes,
			nint priorityRawValue, nint categoryRawValue,
			[NullAllowed] NSDate dueDate, nint estimatedMinutes,
			bool isCompleted, NSDate createdAt);
	}

	/// <summary>
	/// Protocol that C# implements to provide task data to Swift App Intents.
	/// The MAUI app creates a class inheriting from TaskDataProvider and registers it.
	/// </summary>
	[Protocol, Model]
	[BaseType(typeof(NSObject))]
	interface TaskDataProvider
	{
		[Abstract, Export("getAllTasks")]
		BridgeTaskItem[] GetAllTasks();

		[Abstract, Export("getTaskWithId:")]
		[return: NullAllowed]
		BridgeTaskItem GetTask(string id);

		[Abstract, Export("createTaskWithTitle:priorityRawValue:categoryRawValue:dueDate:estimatedMinutes:notes:")]
		[return: NullAllowed]
		BridgeTaskItem CreateTask(string title, nint priorityRawValue, nint categoryRawValue,
			[NullAllowed] NSDate dueDate, nint estimatedMinutes, string notes);

		[Abstract, Export("completeTaskWithId:")]
		bool CompleteTask(string id);

		[Abstract, Export("searchTasksWithQuery:")]
		BridgeTaskItem[] SearchTasks(string query);

		[Abstract, Export("getTasksByFilterWithCategoryRawValue:priorityRawValue:showCompleted:")]
		BridgeTaskItem[] GetTasksByFilter(nint categoryRawValue, nint priorityRawValue, bool showCompleted);

		[Abstract, Export("setDueDate:forTaskWithId:")]
		bool SetDueDate(NSDate date, string taskId);
	}

	/// <summary>
	/// Singleton manager that holds the TaskDataProvider.
	/// Swift App Intents use this to access C# business logic.
	/// </summary>
	[BaseType(typeof(NSObject))]
	[DisableDefaultCtor]
	interface TaskBridgeManager
	{
		[Static, Export("shared")]
		TaskBridgeManager Shared { get; }

		[NullAllowed, Export("provider", ArgumentSemantic.Weak)]
		TaskDataProvider Provider { get; set; }
	}

	/// <summary>
	/// Bridge for donating App Intents to the system when users perform actions in the MAUI UI.
	/// This helps the system learn user patterns and proactively suggest intents.
	/// </summary>
	[BaseType(typeof(NSObject))]
	[DisableDefaultCtor]
	interface IntentDonationBridge
	{
		[Static, Export("shared")]
		IntentDonationBridge Shared { get; }

		[Export("donateCreateTaskWithTitle:priorityRawValue:categoryRawValue:dueDate:estimatedMinutes:notes:")]
		void DonateCreateTask(string title, nint priorityRawValue, nint categoryRawValue,
			[NullAllowed] NSDate dueDate, nint estimatedMinutes, string notes);

		[Export("donateCompleteTaskWithTaskId:taskTitle:")]
		void DonateCompleteTask(string taskId, string taskTitle);

		[Export("donateOpenTaskWithTaskId:taskTitle:")]
		void DonateOpenTask(string taskId, string taskTitle);

		[Export("donateSetDueDate:taskId:taskTitle:")]
		void DonateSetDueDate(NSDate date, string taskId, string taskTitle);

		[Export("donateSearchTasksWithQuery:")]
		void DonateSearchTasks(string query);

		[Export("deleteTaskDonationsWithTaskId:")]
		void DeleteTaskDonations(string taskId);
	}
}
