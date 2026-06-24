namespace JulyGame
{
    public interface IConfigSystem : ISupportMultipleSource<IConfigProvider>
    {
        T GetTable<T>() where T : class;
        bool TryGetTable<T>(out T table) where T : class;
    }
}
