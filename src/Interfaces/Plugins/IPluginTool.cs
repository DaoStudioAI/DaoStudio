using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces.Plugins
{
    public interface IPluginTool : IDisposable
    {
        //this function will be called per session
        Task GetSessionFunctionsAsync(List<FunctionWithDescription> toolcallFunctions,IHostPerson? person, IHostSession? hostSession);


        Task<byte[]?> CloseSessionAsync(IHostSession hostSession);
    }
}
