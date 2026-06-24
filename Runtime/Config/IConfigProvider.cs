namespace JulyGame
{
    public interface IConfigProvider : IDataProvider
    {
        bool TryGetTable<T>(out T table) where T : class;
    }
}
