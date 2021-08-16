﻿using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using KanbanTasker.Base;
using KanbanTasker.Services;
using KanbanTasker.Helpers.Microsoft_Graph;
using KanbanTasker.Helpers.Microsoft_Graph.Authentication;

namespace KanbanTasker.ViewModels
{
    public class SettingsViewModel : Observable
    {
        private readonly IAppNotificationService _appNotificationService;
        private const int NOTIFICATION_DURATION = 3000;
        public const string DataFilename = "ktdatabase.db";
        public const string BackupFolderName = "Kanban Tasker";
        private readonly AuthenticationProvider authProvider;
        private string _welcomeText;
        private bool _isSignoutEnabled;
        private bool _isProgressRingActive = false;
        private bool _isBackupPopupOpen;
        private bool _isSignoutPopupOpen;
        private bool _isRestorePopupOpen;

        public User CurrentUser { get; set; }

        public ICommand BackupDatabaseCommand { get; set; }
        public ICommand RestoreDatabaseCommand { get; set; }
        public ICommand SignoutUserCommand { get; set; }

        public SettingsViewModel(IAppNotificationService appNotificationService)
        {
            BackupDatabaseCommand = new RelayCommand(BackupToOneDrive, () => true);
            RestoreDatabaseCommand = new RelayCommand(RestoreFromOneDrive, () => true);
            SignoutUserCommand = new RelayCommand(SignOut, () => IsSignoutEnabled);

            _appNotificationService = appNotificationService;

            authProvider = App.GetAuthenticationProvider();

            if (App.CurrentUser != null)
            {
                WelcomeText = "Welcome " + App.CurrentUser.GivenName;
                IsSignoutEnabled = true;
            }
            else
            {
                WelcomeText = "Welcome, please select an option below and sign in when prompted";
                IsSignoutEnabled = false;
            }
        }

        public string WelcomeText
        {
            get => _welcomeText;
            set => SetProperty(ref _welcomeText, value);
        }

        public bool IsSignoutEnabled
        {
            get => _isSignoutEnabled;
            set => SetProperty(ref _isSignoutEnabled, value);
        }

        public bool IsProgressRingActive
        {
            get => _isProgressRingActive;
            set => SetProperty(ref _isProgressRingActive, value);
        }

        public bool IsBackupPopupOpen
        {
            get => _isBackupPopupOpen;
            set => SetProperty(ref _isBackupPopupOpen, value);
        }

        public bool IsSignoutPopupOpen
        {
            get => _isSignoutPopupOpen;
            set => SetProperty(ref _isSignoutPopupOpen, value);
        }

        public bool IsRestorePopupOpen
        {
            get => _isRestorePopupOpen;
            set => SetProperty(ref _isRestorePopupOpen, value);
        }

        /// <summary>
        /// Initiate backup of data to OneDrive.
        /// </summary>
        private async void BackupToOneDrive()
        {
            IsProgressRingActive = true;
            ClosePopups();

            try
            {
                // Request a token to sign in the user
                var accessToken = await authProvider.GetAccessToken();

                // Initialize Graph Client
                GraphServiceHelper.InitializeClient(authProvider);

                // Set current user (temp)
                App.CurrentUser = await GraphServiceHelper.GetMeAsync();

                // Find backupFolder in user's OneDrive, if it exists
                DriveItem backupFolder = await GraphServiceHelper.GetOneDriveFolderAsync("Kanban Tasker");

                // Create backup folder in OneDrive if not exists
                if (backupFolder == null)
                    backupFolder = await GraphServiceHelper.CreateNewOneDriveFolderAsync("Kanban Tasker");

                // Backup datafile (or overwrite)
                DriveItem uploadedFile = await GraphServiceHelper.UploadFileToOneDriveAsync(backupFolder.Id, DataFilename);

                DisplayNotificationMessage("Data backed up successfully");

                var displayName = await GraphServiceHelper.GetMyDisplayNameAsync();
                WelcomeText = "Welcome " + displayName;
                IsSignoutEnabled = true;
            }
            catch (ServiceException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // MS Graph Known Error 
                    // Users need to sign into OneDrive at least once
                    // https://docs.microsoft.com/en-us/graph/known-issues#files-onedrive

                    // Empty all cached accounts / data to allow user to rety
                    await authProvider.SignOut();

                    DisplayNotificationMessage("Error 401. Access Denied. Please make sure you've logged\ninto OneDrive and your email at least once then try again.");
                }
                else if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    DisplayNotificationMessage("Error 404. Resource requested is not available.");
                }
                else if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    DisplayNotificationMessage("Error 409. Error backing up, issue retrieving backup folder. Please try again.");
                }
                else if (ex.StatusCode == HttpStatusCode.BadGateway)
                {
                    DisplayNotificationMessage("Error 502. Bad Gateway.\nPlease check your internet connection and try again in a few.");
                }
                else if (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    DisplayNotificationMessage("Error 503. Service unavailable due to high load or maintenance.\nPlease try again in a few.");
                }
                else if (ex.IsMatch(GraphErrorCode.GeneralException.ToString()))
                {
                    DisplayNotificationMessage("General Exception. Please check your internet connection and try again in a few.");
                }
            }
            catch (MsalException msalex)
            {
                if (msalex.ErrorCode == MsalError.AuthenticationCanceledError)
                {
                    DisplayNotificationMessage(msalex.Message);
                }
                else if (msalex.ErrorCode == MsalError.InvalidGrantError)
                {
                    // invalid_grant ErrorCode comes from no consent
                    // for the correct scopes (todo: add interactive retry)
                    DisplayNotificationMessage("Invalid access scopes, please contact the developer.");
                }
            }
            catch (Exception ex)
            {
                DisplayNotificationMessage(ex.Message);
            }
            finally
            {
                IsProgressRingActive = false;
            }
        }

        /// <summary>
        /// Initiate restoration of data from OneDrive.
        /// <para>*Application restarts if finished successfully.</para>
        /// </summary>
        private async void RestoreFromOneDrive()
        {
            IsProgressRingActive = true;
            ClosePopups();

            try
            {
                // Request a token to sign in the user
                var accessToken = await authProvider.GetAccessToken();

                // Initialize Graph Client
                GraphServiceHelper.InitializeClient(authProvider);

                // Set current user (temp)
                App.CurrentUser = await GraphServiceHelper.GetMeAsync();

                // Find the backupFolder in OneDrive, if it exists
                var backupFolder = await GraphServiceHelper.GetOneDriveFolderAsync("Kanban Tasker");

                if (backupFolder != null)
                {
                    // Restore local data file using the backup file, if it exists
                    await GraphServiceHelper.RestoreFileFromOneDriveAsync(backupFolder.Id, "ktdatabase.db");

                    DisplayNotificationMessage("Data restored successfully");

                    var displayName = await GraphServiceHelper.GetMyDisplayNameAsync();
                    WelcomeText = "Welcome " + App.CurrentUser.GivenName;
                    IsSignoutEnabled = true;

                    // Restart app to make changes
                    await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync("");
                }
                else
                    DisplayNotificationMessage("No backup folder found to restore from.");
            }
            catch (ServiceException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // MS Graph Known Error 
                    // Users need to sign into OneDrive at least once
                    // https://docs.microsoft.com/en-us/graph/known-issues#files-onedrive

                    // Empty all cached accounts / data to allow user to rety
                    await authProvider.SignOut();

                    DisplayNotificationMessage("Error 401. Access Denied. Please make sure you've logged\ninto OneDrive and your email at least once then try again.");
                }
                else if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    DisplayNotificationMessage("Error 404. Resource requested is not available.");
                }
                else if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    DisplayNotificationMessage("Error 409. Error backing up, issue retrieving backup folder. Please try again.");
                }
                else if (ex.StatusCode == HttpStatusCode.BadGateway)
                {
                    DisplayNotificationMessage("Error 502. Bad Gateway.\nPlease check your internet connection and try again in a few.");
                }
                else if (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    DisplayNotificationMessage("Error 503. Service unavailable due to high load or maintenance.\nPlease try again in a few.");
                }
                else if (ex.IsMatch(GraphErrorCode.GeneralException.ToString()))
                {
                    DisplayNotificationMessage("General Exception. Please check your internet connection and try again in a few.");
                }
            }
            catch (MsalException msalex)
            {
                if (msalex.ErrorCode == MsalError.AuthenticationCanceledError)
                {
                    DisplayNotificationMessage(msalex.Message);
                }
                else if (msalex.ErrorCode == MsalError.InvalidGrantError)
                {
                    // invalid_grant comes from no consent to needed scopes
                    DisplayNotificationMessage("Invalid access scopes, please contact the developer.");
                }
            }
            catch (Exception ex)
            {
                DisplayNotificationMessage("Unexpected Error: " + ex.Message);
            }
            finally
            {
                IsProgressRingActive = false;
            }
        }

        private async void SignOut()
        {
            ClosePopups();

            try
            {
                await authProvider.SignOut();

                WelcomeText = "User has signed-out";
                IsSignoutEnabled = false;
                App.CurrentUser = null;
            }
            catch (MsalException ex)
            {
                DisplayNotificationMessage(ex.Message);
            }
        }

        /// <summary>
        /// Display a notification message to the user on the screen.
        /// </summary>
        /// <param name="message"></param>
        public void DisplayNotificationMessage(string message)
        {
            _appNotificationService.DisplayNotificationAsync(message, NOTIFICATION_DURATION);
        }

        public void ShowBackupPopup() => IsBackupPopupOpen = true;

        public void ShowRestorePopup() => IsRestorePopupOpen = true;

        public void ShowSignoutPopup() => IsSignoutPopupOpen = true;

        public void ClosePopups()
        {
            IsBackupPopupOpen = false;
            IsRestorePopupOpen = false;
        }
    }
}