﻿using Autofac;
using KanbanTasker.Services;
using KanbanTasker.ViewModels;

namespace KanbanTasker
{
    public class AutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<BoardViewModel>();
            builder.RegisterType<MainViewModel>();
            builder.RegisterType<SettingsViewModel>();

            builder.RegisterType<AppNotificationService>()
                .As<IAppNotificationService>()
                .SingleInstance();

            builder.RegisterType<DialogService>()
                .As<IDialogService>()
                .SingleInstance();

            builder.RegisterType<ToastService>()
                .As<IToastService>()
                .SingleInstance();
        }
    }
}