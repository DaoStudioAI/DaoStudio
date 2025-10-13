using Avalonia.Controls;
using DaoStudio.Interfaces.Plugins;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces
{
    public class PluginInfo
    {
        public string? StaticId;
        public string? Version;
        public string? DisplayName;
    }
    public class PlugToolInfo
    {
        public long InstanceId;
        public string? DisplayName;
        public string? Description;
        public string? Config;
        public bool SupportConfigWindow = false; //if true, the plugin supports a popup window for configuration
        public bool HasMultipleInstances = false; //if true, there are multiple tool instances for this plugin
        public byte[]? Status;
    }
    public interface IPluginConfigAvalonia
    {
        Task<PlugToolInfo> ConfigInstance(Window win, PlugToolInfo plugInstanceInfo);
    }

    //implement IPluginConfigAvalonia if the plugin want to support config UI
    public interface IPluginFactory
    {
        PluginInfo GetPluginInfo();

        Task SetHost(IHost host);

        #region tool config instance management.
        //CreateInstanceAsync,ConfigInstance,DeleteInstanceAsync will be called independently
        Task<PlugToolInfo> CreateToolConfigAsync(long instanceid);
        Task DeleteToolConfigAsync(PlugToolInfo plugInstanceInfo);
        #endregion

        Task<IPluginTool> CreatePluginToolAsync(PlugToolInfo plugInstanceInfo);
    }

    //Helper functions like CreateFunctionsFromToolMethods
    public static partial class IPluginExtensions;
}
