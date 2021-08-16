﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using Windows.UI.Xaml;
using Syncfusion.UI.Xaml.Kanban;
using LeaderAnalytics.AdaptiveClient;
using KanbanTasker.Base;
using KanbanTasker.Model;
using KanbanTasker.Extensions;
using KanbanTasker.Helpers;
using KanbanTasker.Models;
using KanbanTasker.Services;

namespace KanbanTasker.ViewModels
{
    public class BoardViewModel : Observable
    {
        private const int NOTIFICATION_DURATION = 3000;
        private const string DEFAULT_COLOR_KEY = "Low";
        private const string DEFAULT_REMINDER_TIME = "None";
        private readonly IAppNotificationService _appNotificationService;
        private IAdaptiveClient<IServiceManifest> DataProvider;
        private IToastService _toastService;
        private PresentationBoard _board;
        private PresentationTask _currentTask;
        private ObservableCollection<string> _suggestedTagsCollection;
        List<ColumnDTO> columnNames;
        private string _paneTitle;
        private bool _isPaneOpen;
        private string _currentCategory;
        private bool _isPointerEntered = false;
        private bool _isEditingTask;
        private bool _isProgressRingActive = false;
        private bool _isPassedDue = false;
        private string _ColFiveName;
        private string _ColThreeName;
        private string _ColTwoName;
        private string _ColFourName;
        private string _ColOneName;
        private string _NewColName;
        private DispatcherTimer _dateCheckTimer;
        private ObservableCollection<string> _colorKeys;
        private ObservableCollection<string> _reminderTimes;
        private int _columnMaxTaskLimit;
        private int _columnOneMax;
        private int _columnTwoMax;
        private int _columnThreeMax;
        private int _columnFourMax;
        private int _columnFiveMax;
        private int _newColumnMax;

        public ICommand NewTaskCommand { get; set; }
        public ICommand EditColumnCommand { get; set; }
        public ICommand EditTaskCommand { get; set; }
        public ICommand SaveTaskCommand { get; set; }
        public ICommand DeleteTaskCommand { get; set; }
        public ICommand DeleteTagCommand { get; set; }
        public ICommand CancelEditCommand { get; set; }
        public ICommand RemoveScheduledNotificationCommand { get; set; }

        /// <summary>
        /// Initializes the commands and tasks for the current board.
        /// </summary>
        public BoardViewModel(
            PresentationBoard board,
            IAdaptiveClient<IServiceManifest> dataProvider,
            IAppNotificationService appNotificationService,
            IToastService toastService)
        {
            Board = board;
            DataProvider = dataProvider;
            _appNotificationService = appNotificationService;
            _toastService = toastService;

            CurrentTask = new PresentationTask(new TaskDTO());
            NewTaskCommand = new RelayCommand<ColumnTag>(NewTask, () => true);
            EditTaskCommand = new RelayCommand<int>(EditTask, () => true);
            SaveTaskCommand = new RelayCommand(SaveTask, () => true);
            DeleteTaskCommand = new RelayCommand<int>(DeleteTask, () => PaneTitle.Equals("Edit Task") || PaneTitle.Equals(""));
            DeleteTagCommand = new RelayCommand<string>(DeleteTag, () => true);
            CancelEditCommand = new RelayCommand(CancelEdit, () => true);
            RemoveScheduledNotificationCommand = new RelayCommand(RemoveScheduledNotfication, () => true);

            ColorKeys = new ObservableCollection<string>
            {
                "Low", "Medium", "High"
            };

            ReminderTimes = new ObservableCollection<string>
            {
                "None",
                "At Time of Due Date",
                "5 Minutes Before",
                "10 Minutes Before",
                "15 Minutes Before",
                "1 Hour Before",
                "2 Hours Before",
                "1 Day Before",
                "2 Days Before"
            };

            PaneTitle = "New Task";

            ConfigureBoardColumns();
        }

        /// <summary>
        /// An instance of the current board
        /// </summary>
        public PresentationBoard Board
        {
            get => _board;
            set => SetProperty(ref _board, value);
        }

        /// <summary>
        /// An instance of the current task
        /// </summary>
        public PresentationTask CurrentTask
        {
            get => _currentTask;
            set => SetProperty(ref _currentTask, value);
        }

        public ObservableCollection<string> SuggestedTagsCollection
        {
            get => _suggestedTagsCollection;
            set => SetProperty(ref _suggestedTagsCollection, value);
        }

        /// <summary>
        /// Flag for enabling a progress ring when true
        /// </summary>
        public bool IsProgressRingActive
        {
            get => _isProgressRingActive;
            set => SetProperty(ref _isProgressRingActive, value);
        }

        /// <summary>
        /// The color keys for a task
        /// </summary>
        public ObservableCollection<string> ColorKeys
        {
            get => _colorKeys;
            set => SetProperty(ref _colorKeys, value);
        }

        /// <summary>
        /// The possible reminder times for a task 
        /// </summary>
        public ObservableCollection<string> ReminderTimes
        {
            get => _reminderTimes;
            set => SetProperty(ref _reminderTimes, value);
        }

        
         /// <summary>
        /// Determines if pointer entered
        /// inside of a card
        /// </summary>
        public bool IsPointerEntered
        {
            get => _isPointerEntered;
            set => SetProperty(ref _isPointerEntered, value);
        }

        /// <summary>
        /// Flag for if a task is currently being edited
        /// </summary>
        public bool IsEditingTask
        {
            get => _isEditingTask;
            set => SetProperty(ref _isEditingTask, value);
        }

        /// <summary>
        /// The category to be displayed on the edit/new task pane
        /// </summary>
        public string CurrentCategory
        {
            get => _currentCategory;
            set => SetProperty(ref _currentCategory, value);
        }

        /// <summary>
        /// Gets or sets the selected item of the color key combo box. 
        /// Updates the current tasks color key.
        /// </summary>
        public string SelectedColorKey
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentTask.ColorKey))
                    return DEFAULT_COLOR_KEY;
                else
                    return CurrentTask.ColorKey;
            }
            set
            {
                CurrentTask.ColorKey = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the selected item of the reminder time combo box.
        /// Updates the current tasks reminder time.
        /// </summary>
        public string SelectedReminderTime
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentTask.ReminderTime))
                    return DEFAULT_REMINDER_TIME;
                else
                    return CurrentTask.ReminderTime;
            }
            set
            {
                CurrentTask.ReminderTime = value;
                OnPropertyChanged();
            }
        }
     

        /// <summary>
        /// Flag for determining if the current task is passed due. 
        /// </summary>
        public bool IsPassedDue
        {
            get => _isPassedDue;
            set => SetProperty(ref _isPassedDue, value);
        }

        /// <summary>
        /// Used to make a copy of the current task to revert changes when cancelling editing
        /// </summary>
        public PresentationTask OriginalTask { get; set; }

        public string ColOneName
        {
            get { return _ColOneName; }
            set
            {
                _ColOneName = value;
                OnPropertyChanged();
            }
        }

        public string ColTwoName
        {
            get { return _ColTwoName; }
            set
            {
                _ColTwoName = value;
                OnPropertyChanged();
            }
        }
        public string ColThreeName
        {
            get { return _ColThreeName; }
            set
            {
                _ColThreeName = value;
                OnPropertyChanged();
            }
        }
        public string ColFourName
        {
            get { return _ColFourName; }
            set
            {
                _ColFourName = value;
                OnPropertyChanged();
            }
        }
        public string ColFiveName
        {
            get { return _ColFiveName; }
            set
            {
                _ColFiveName = value;
                OnPropertyChanged();
            }
        }

        public string NewColumnName
        {
            get { return _NewColName; }
            set
            {
                _NewColName = value;
                OnPropertyChanged();
            }
        }

        public int ColumnMaxTaskLimit
        {
            get => _columnMaxTaskLimit;
            set => SetProperty(ref _columnMaxTaskLimit, value);
        }

        public int ColumnOneMax
        {
            get => _columnOneMax;
            set => SetProperty(ref _columnOneMax, value);
        }

        public int ColumnTwoMax
        {
            get => _columnTwoMax;
            set => SetProperty(ref _columnTwoMax, value);
        }

        public int ColumnThreeMax
        {
            get => _columnThreeMax;
            set => SetProperty(ref _columnThreeMax, value);
        }

        public int ColumnFourMax
        {
            get => _columnFourMax;
            set => SetProperty(ref _columnFourMax, value);
        }

        public int ColumnFiveMax
        {
            get => _columnFiveMax;
            set => SetProperty(ref _columnFiveMax, value);
        }

        public int NewColumnMax
        {
            get => _newColumnMax;
            set => SetProperty(ref _newColumnMax, value);
        }

        public string PaneTitle
        {
            get => _paneTitle;
            set => SetProperty(ref _paneTitle, value);
        }

        public bool IsPaneOpen
        {
            get => _isPaneOpen;
            set => SetProperty(ref _isPaneOpen, value);
        }

        /// <summary>
        /// Prepares a task to be created in the given column
        /// and initializes <see cref="CurrentTask"/>
        /// </summary>
        /// <param name="tag"></param>
        public void NewTask(ColumnTag tag)
        {
            IsEditingTask = true;
            PaneTitle = "New Task";
            IsPaneOpen = true;
            
            var category = tag?.Header?.ToString();
            CurrentTask = new PresentationTask(new TaskDTO() { Category = category })
            {
                Board = Board,
                BoardId = Board.ID
            };

            SelectedColorKey = DEFAULT_COLOR_KEY;
            SelectedReminderTime = DEFAULT_REMINDER_TIME;
            OriginalTask = null; 
            InitializeSuggestedTags();
        }

        /// <summary>
        /// Prepares a task with the given ID for editing and initializes <see cref="CurrentTask"/>
        /// </summary>
        /// <param name="taskID"></param>
        public void EditTask(int taskID)
        {
            IsEditingTask = true;
            PaneTitle = "Edit Task";
            IsPaneOpen = true;
            CurrentTask = Board.Tasks.First(x => x.ID == taskID);
            SelectedColorKey = CurrentTask.ColorKey;
            SelectedReminderTime = CurrentTask.ReminderTime;
            InitializeSuggestedTags();
            InitializeDateInformation();

            // clone a copy of CurrentTask so we can restore if user cancels
            OriginalTask = new PresentationTask(CurrentTask.To_TaskDTO());
        }

        /// <summary>
        /// Saves the current task instance being created or edited and updates database.
        /// </summary>
        public void SaveTask()
        {
            IsEditingTask = false;
            PaneTitle = "";
            IsPaneOpen = false;

            _dateCheckTimer?.Stop();

            if (CurrentTask == null)
                return;

            TaskDTO taskDto = CurrentTask.To_TaskDTO();

            bool isNew = taskDto.Id == 0;
            if (isNew)
            {
                taskDto.ColumnIndex = Board.Tasks?.Where(x => x.Category == taskDto.Category).Count() ?? 0;
                taskDto.DateCreated = DateTime.Now.ToString();
            }
            taskDto.Id = DataProvider.Call(x => x.TaskServices.SaveTask(taskDto)).Entity.Id;

            if (isNew)
            {
                CurrentTask.ID = taskDto.Id;
                CurrentTask.ColumnIndex = taskDto.ColumnIndex;
                Board.Tasks.Add(CurrentTask);
            }

            if (IsReminderInformationNull())
            {
                PrepareAndScheduleToastNotification();
            }

            _appNotificationService.DisplayNotificationAsync("Task was saved successfully", NOTIFICATION_DURATION);
        }

        public bool IsReminderInformationNull()
        {
            return !string.IsNullOrEmpty(CurrentTask.DueDate) &&
                CurrentTask.TimeDue != null &&
                CurrentTask.ReminderTime != "None" &&
                CurrentTask.ReminderTime != "";
        }

        /// <summary>
        /// Deletes a task with the given ID from the list and updates the database.
        /// </summary>
        /// <param name="taskID"></param>
        public void DeleteTask(int taskID)
        {
            _dateCheckTimer?.Stop();

            PresentationTask task = Board.Tasks.First(x => x.ID == taskID);
            RowOpResult result = DataProvider.Call(x => x.TaskServices.DeleteTask(taskID));

            if (result.Success)
            {
                Board.Tasks.Remove(task);
                CurrentTask = Board.Tasks.LastOrDefault();
                _toastService.RemoveScheduledToast(taskID.ToString());

                int startIndex = task.ColumnIndex;

                // If we do not sort by ColumnIndex, the tasks in Board.Tasks will be in unsorted order when assigning startIndex 
                var sortedTasks = Board.Tasks
                    .Where(x => x.Category == task.Category && x.ColumnIndex > task.ColumnIndex)
                    .OrderBy(x => x.ColumnIndex);

                foreach (PresentationTask otherTask in sortedTasks)
                {
                    otherTask.ColumnIndex = startIndex++;
                    UpdateCardIndex(otherTask.ID, otherTask.ColumnIndex);
                }
                _appNotificationService.DisplayNotificationAsync("Task deleted from board successfully", NOTIFICATION_DURATION);
            }
            else
            {
                _appNotificationService.DisplayNotificationAsync("Task failed to be deleted. Please try again or restart the application.", NOTIFICATION_DURATION);
            }
        }

        /// <summary>
        /// Delete's the specified tag from the current task's tag collection
        /// </summary>
        /// <param name="tag"></param>
        public void DeleteTag(string tag)
        {
            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync(
                    "Tag failed to be deleted. CurrentTask is null. Please try again or restart the application.",
                    NOTIFICATION_DURATION);
                return;
            }
            CurrentTask.Tags.Remove(tag);

            _appNotificationService.DisplayNotificationAsync("Tag deleted successfully", NOTIFICATION_DURATION);
        }

        /// <summary>
        /// Cancels editing for the CurrentTask and reverts changes
        /// </summary>
        public void CancelEdit()
        {
            IsProgressRingActive = true;
            IsEditingTask = false;
            PaneTitle = "";
            IsPaneOpen = false;

            if (_dateCheckTimer != null)
                _dateCheckTimer.Stop();

            // Revert changes to Current Task from OriginalTask, a 
            // copy created when the EditTask command is called
            if (OriginalTask != null)
            {
                int index = Board.Tasks.IndexOf(CurrentTask);
                var tempTask = new PresentationTask(OriginalTask.To_TaskDTO());
                Board.Tasks[index] = tempTask;
                Board.Tasks = new ObservableCollection<PresentationTask>(
                    Board.Tasks.OrderBy(x => x.ColumnIndex));

                // Check if a toast notification was deleted
                if (OriginalTask.ReminderTime != DEFAULT_REMINDER_TIME)
                    PrepareAndScheduleToastNotification();
            }

            IsProgressRingActive = false;
        }

        /// <summary>
        /// Removes the scheuled notification for the current task, 
        /// uniqely identified by its tag. <br />
        /// In this case, the tag is the task's unique ID.
        /// </summary>
        private void RemoveScheduledNotfication()
        {
            _toastService.RemoveScheduledToast(CurrentTask.ID.ToString());
            CurrentTask.ReminderTime = DEFAULT_REMINDER_TIME;
        }

        /// <summary>
        /// Updates the column name in the database with its new name and renames
        /// each tasks category to the updated name.
        /// </summary>
        /// <param name="originalColName"></param>
        /// <param name="newColName"></param>
        public void EditColumn(string originalColName, string newColName, int newColMax)
        {
            // Update column
            if (columnNames.Count == 0)
                columnNames = DataProvider.Call(x => x.BoardServices.GetColumns(Board.ID));

            ColumnDTO columnDto = columnNames.Find(x => x.ColumnName.Equals(originalColName));
            columnDto.ColumnName = newColName;
            columnDto.MaxTaskLimit = newColMax;

            DataProvider.Call(x => x.BoardServices.SaveColumn(columnDto));

            // Update tasks category name to new column
            // Note: Items end up unordered when not calling new?
            var tasksCopy = Board.Tasks;
            foreach (PresentationTask task in tasksCopy)
            {
                if (task.Category == originalColName)
                {
                    task.Category = newColName;
                    DataProvider.Call(x => x.TaskServices.UpdateColumnName(task.ID, task.Category));
                }
            }
            Board.Tasks = new ObservableCollection<PresentationTask>(tasksCopy.OrderBy(x => x.ColumnIndex));
        }

        /// <summary>
        /// Inserts a tag to the current task's tag collection.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns>A bool containing whether the tag was successfully added or not.</returns>
        public bool AddTag(string tag)
        {
            bool result = false;

            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync("Tag failed to be added.  CurrentTask is null. Please try again or restart the application.", NOTIFICATION_DURATION);
                return result;
            }

            if (CurrentTask.Tags.Contains(tag))
                _appNotificationService.DisplayNotificationAsync("Tag already exists", 3000);
            else
            {
                CurrentTask.Tags.Add(tag);
                if (!Board.TagsCollection.Contains(tag))
                    Board.TagsCollection.Add(tag);

                SuggestedTagsCollection.Remove(tag);
                _appNotificationService.DisplayNotificationAsync($"Tag {tag} added successfully", 3000);
                result = true;
            }
            return result;
        }

        private void InitializeSuggestedTags()
        {
            // Removes tags from suggested list that are already on the tag, if any
            SuggestedTagsCollection = Board.TagsCollection;
            foreach (var tag in CurrentTask.Tags)
            {
                if (SuggestedTagsCollection.Contains(tag))
                {
                    SuggestedTagsCollection.Remove(tag);
                }
                else
                    SuggestedTagsCollection = Board.TagsCollection;
            }
        }

        private void InitializeDateInformation()
        {
            if (IsEditingTask)
            {
                StartDateCheckTimer();
                UpdateDateInformation();
            }
        }

        /// <summary>
        /// Schedules a toast notification using <see cref="ToastService"/> if the current task 
        /// has a selected due date, time due, reminder time when called.
        /// </summary>
        private void PrepareAndScheduleToastNotification()
        {
            // UWP TimePicker doesn't support Nullable values, defaults to a value either way
            var dueDate = CurrentTask.DueDate.ToNullableDateTimeOffset();
            var timeDue = CurrentTask.TimeDue.ToNullableDateTimeOffset();
            string reminderTime = CurrentTask.ReminderTime;

            if (reminderTime.Equals(DEFAULT_REMINDER_TIME))
                _toastService.RemoveScheduledToast(CurrentTask.ID.ToString());
            else
            {
                if (dueDate == null || timeDue == null)
                    return;

                // Combine due date and time due
                // ToastNotifications require a non-nullable DateTimeOffset
                var taskDueDate = new DateTimeOffset(
                   dueDate.Value.Year, dueDate.Value.Month, dueDate.Value.Day,
                   timeDue.Value.Hour, timeDue.Value.Minute, timeDue.Value.Second,
                   timeDue.Value.Offset
                );

                var scheduledTime = taskDueDate;
                switch (reminderTime)
                {
                    case "At Time of Due Date":
                        break;
                    case "5 Minutes Before":
                        scheduledTime = taskDueDate.AddMinutes(-5);
                        break;
                    case "10 Minutes Before":
                        scheduledTime = taskDueDate.AddMinutes(-10);
                        break;
                    case "15 Minutes Before":
                        scheduledTime = taskDueDate.AddMinutes(-15);
                        break;
                    case "1 Hour Before":
                        scheduledTime = taskDueDate.AddHours(-1);
                        break;
                    case "2 Hours Before":
                        scheduledTime = taskDueDate.AddHours(-2);
                        break;
                    case "1 Day Before":
                        scheduledTime = taskDueDate.AddDays(-1);
                        break;
                    case "2 Days Before":
                        scheduledTime = taskDueDate.AddDays(-2);
                        break;
                }

                _toastService.ScheduleToast(CurrentTask.To_TaskDTO(), scheduledTime, taskDueDate);
            }
        }

        private void StartDateCheckTimer()
        {
            _dateCheckTimer = new DispatcherTimer();
            _dateCheckTimer.Interval = TimeSpan.FromMinutes(1);
            _dateCheckTimer.Tick += Timer_Tick;
            _dateCheckTimer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            UpdateDateInformation();
        }

        private void UpdateDateInformation()
        {
            DateTimeOffset? dateCreated = CurrentTask.DateCreated.ToNullableDateTimeOffset();
            DateTimeOffset? today = DateTimeOffset.Now;
            DateTimeOffset? startDate = CurrentTask.StartDate.ToNullableDateTimeOffset();
            DateTimeOffset? finishDate = CurrentTask.FinishDate.ToNullableDateTimeOffset();

            IsPassedDue = CheckIfPastDue(); 

            // Update DaysWorkedOn
            if (startDate != null)
            {
                TimeSpan? timeSpan = today - startDate;

                if (timeSpan != null)
                    // Difference in days, hous, mins
                    CurrentTask.DaysWorkedOn = $"{timeSpan.Value.Days.ToString()} day(s)";
            }

            //  Update DaysSinceCreation
            if (dateCreated != null && today != null)
            {
                TimeSpan? timeSpan = today - dateCreated;

                if (timeSpan != null)
                {
                    // Difference in days, hours, mins
                    CurrentTask.DaysSinceCreation = string.Format(
                        "{0}d, {1}hrs, {2}min",
                        timeSpan.Value.Days.ToString(),
                        timeSpan.Value.Hours.ToString(),
                        timeSpan.Value.Minutes.ToString());
                }
            }
        }

        /// <summary>
        /// Sets the due date for the current task.
        /// </summary>
        /// <param name="dueDate"></param>
        public void SetDueDate(string dueDate)
        {
            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync(
                    "Failed to set due date.  CurrentTask is null. Please try again or restart the application.",
                    NOTIFICATION_DURATION);
                return;
            }

            CurrentTask.DueDate = dueDate;
            IsPassedDue = CheckIfPastDue();
        }

        /// <summary>
        /// Sets the start date for the current task.
        /// </summary>
        /// <param name="startDate"></param>
        public void SetStartDate(string startDate)
        {
            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync(
                    "Failed to set due date.  CurrentTask is null. Please try again or restart the application.",
                    NOTIFICATION_DURATION);
                return;
            }

            CurrentTask.StartDate = startDate;

            // Update DaysWorkedOn binding
            DateTimeOffset? today = DateTimeOffset.Now;
            
            if (startDate.ToNullableDateTimeOffset() == null) return;
            
            TimeSpan? ts = today - startDate.ToNullableDateTimeOffset();
            if (ts != null)
            {
                // Difference in days, hours, minutes
                CurrentTask.DaysWorkedOn = $"{ts.Value.Days.ToString()} day(s)";
            }
        }

        /// <summary>
        /// Sets the finish date for the current task.
        /// </summary>
        /// <param name="finishDate"></param>
        public void SetFinishDate(string finishDate)
        {
            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync(
                    "Failed to set due date.  CurrentTask is null. Please try again or restart the application.",
                    NOTIFICATION_DURATION);
                return;
            }

            CurrentTask.FinishDate = finishDate;

            // Determine new DaysWorkedOn value to update binding
            //DateTimeOffset? startDate = CurrentTask.StartDate.ToNullableDateTimeOffset();
            //if (finishDate != null && startDate != null)
            //{
            //    TimeSpan? ts = finishDate.ToNullableDateTimeOffset() - startDate;

            //    if (ts != null)
            //        // Difference in days, hours, minutes
            //        CurrentTask.DaysWorkedOn = String.Format("{0}d, {1}hrs, {2}min",
            //            ts.Value.Days.ToString(), ts.Value.Hours.ToString(), ts.Value.Minutes.ToString());
            //}
        }

        /// <summary>
        /// Sets the time due for the current task to be used for the toast notification.
        /// </summary>
        /// <param name="timeDue"></param>
        public void SetTimeDue(string timeDue)
        {
            if (CurrentTask == null)
            {
                _appNotificationService.DisplayNotificationAsync(
                    "Failed to set time due.  CurrentTask is null. Please try again or restart the application.",
                    NOTIFICATION_DURATION);
                return;
            }

            CurrentTask.TimeDue = timeDue;
            IsPassedDue = CheckIfPastDue();
        }

        /// <summary>
        /// Checks if the current tasks due date is past due and returns the result.
        /// </summary>
        /// <returns>A value indicating if the current task is past due</returns>
        public bool CheckIfPastDue()
        {
            var dueDate = CurrentTask.DueDate.ToNullableDateTimeOffset();
            if (dueDate == null) return false;

            var timeDue = CurrentTask.TimeDue.ToNullableDateTimeOffset();
            if (timeDue == null) return false;

            DateTimeOffset today = DateTimeOffset.Now;
            DateTimeOffset taskDueDate = new DateTimeOffset(
                dueDate.Value.Year, dueDate.Value.Month, dueDate.Value.Day,
                timeDue.Value.Hour, timeDue.Value.Minute, timeDue.Value.Second,
                timeDue.Value.Offset
            );

            return DateTimeOffset.Compare(taskDueDate, today) < 0;
        }

        /// <summary>
        /// Shows a local notification on the current board using message as the content.
        /// </summary>
        /// <param name="message"></param>
        public void ShowInAppNotification(string message)
        {
            _appNotificationService.DisplayNotificationAsync(message, NOTIFICATION_DURATION);
        }

        /// <summary>
        /// Updates the selected card category and column index after dragging it to
        /// a new column.
        /// </summary>
        /// <param name="targetCategory"></param>
        /// <param name="selectedCardModel"></param>
        /// <param name="targetIndex"></param>
        public void UpdateCardColumn(string targetCategory, PresentationTask selectedCardModel, int targetIndex)
        {
            TaskDTO task = selectedCardModel.To_TaskDTO();
            task.Category = targetCategory;
            task.ColumnIndex = targetIndex;

            DataProvider.Call(x => x.TaskServices.UpdateColumnData(task));
        }

        /// <summary>
        /// Updates a specific card index in the database when reordering after dragging a card.
        /// </summary>
        /// <param name="iD"></param>
        /// <param name="currentCardIndex"></param>
        internal void UpdateCardIndex(int iD, int currentCardIndex)
        {
            DataProvider.Call(x => x.TaskServices.UpdateCardIndex(iD, currentCardIndex));
        }

        /// <summary>
        /// Sets column names for default columns if none exist, or 
        /// retrieves columns from database if there are some.
        /// </summary>
        internal void ConfigureBoardColumns()
        {
            // Configure columns for board
            bool isNew = Board.ID == 0;
            columnNames = new List<ColumnDTO>();
            if (!isNew)
            {
                columnNames = DataProvider.Call(x => x.BoardServices.GetColumns(Board.ID));
            }

            NewColumnName = "";

            if (!isNew && columnNames.Count != 0)
            {
                ColumnDTO columnOne = columnNames.Find(x => x.Position == 0);
                ColOneName = columnOne.ColumnName;
                ColumnOneMax = columnOne.MaxTaskLimit;

                ColumnDTO columnTwo = columnNames.Find(x => x.Position == 1);
                ColTwoName = columnTwo.ColumnName;
                ColumnTwoMax = columnTwo.MaxTaskLimit;

                ColumnDTO columnThree = columnNames.Find(x => x.Position == 2);
                ColThreeName = columnThree.ColumnName;
                ColumnThreeMax = columnThree.MaxTaskLimit;

                ColumnDTO columnFour = columnNames.Find(x => x.Position == 3);
                ColFourName = columnFour.ColumnName;
                ColumnFourMax = columnFour.MaxTaskLimit;

                ColumnDTO columnFive = columnNames.Find(x => x.Position == 4);
                ColFiveName = columnFive.ColumnName;
                ColumnFiveMax = columnFive.MaxTaskLimit;
            }
            else
            {
                ColOneName = "Backlog";
                ColTwoName = "To Do";
                ColThreeName = "In Progress";
                ColFourName = "Review";
                ColFiveName = "Completed";
                ColumnOneMax = 10;
                ColumnTwoMax = 10;
                ColumnThreeMax = 10;
                ColumnFourMax = 10;
                ColumnFiveMax = 10;
            }
        }
    }
}