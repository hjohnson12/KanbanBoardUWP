﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Autofac;
using KanbanTasker.ViewModels;
using KanbanTasker.Services;

namespace KanbanTasker.Views
{
    public sealed partial class SettingsView : ContentDialog
    {
        public SettingsViewModel ViewModel => (SettingsViewModel)DataContext;
       
        public SettingsView()
        {
            this.InitializeComponent();

            DataContext = new SettingsViewModel(App.container.Resolve<IAppNotificationService>());
        }
        
        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        /// <summary>
        /// Displays a message using the InAppNotification in MainView. Can be called from any thread.
        /// </summary>
        private async Task DisplayMessageAsync(string message)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                   () =>
                   {
                       var frame = (Frame)Window.Current.Content;
                       (frame.Content as MainView).KanbanInAppNotification.Show(message, 5000);
                   });
        }
    }
}