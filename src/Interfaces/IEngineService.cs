namespace DaoStudio.Interfaces
{
    public interface IEngineService
    {
        Task<IEngine> CreateEngineAsync(IPerson person);
    }
}
