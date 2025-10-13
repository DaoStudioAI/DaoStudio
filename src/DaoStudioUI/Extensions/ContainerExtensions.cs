using DryIoc;
using DaoStudioUI.ViewModels;
using DaoStudioUI.Services;
using DaoStudio.Common.Plugins;

namespace DaoStudioUI.Extensions
{
    public static class ContainerExtensions
    {
        public static void RegisterUIServices(this Container container)
        {
            // Register Main ViewModels - all are in DaoStudioUI.ViewModels namespace
            container.Register<MainWindowViewModel>(Reuse.Transient);
            container.Register<HomeViewModel>(Reuse.Transient);
            container.Register<SettingsViewModel>(Reuse.Transient);
            container.Register<ToolsViewModel>(Reuse.Transient);
            container.Register<PeopleViewModel>(Reuse.Transient);
            
            // Register Chat ViewModels
            container.Register<ChatViewModel>(Reuse.Transient);
            container.Register<ToolsPanelViewModel>(Reuse.Transient);
            container.Register<UsageTabViewModel>(Reuse.Transient);
            container.Register<ToolCallMessageDialogViewModel>(Reuse.Transient);
            
            // Register People ViewModels
            container.Register<AddPersonViewModel>(Reuse.Transient);
            container.Register<ToolSelectionViewModel>(Reuse.Transient);
            
            // Register Services
            container.Register<UpdateService>(Reuse.Singleton);
            container.Register<TrayIconService>(Reuse.Singleton);
        }
    }
}