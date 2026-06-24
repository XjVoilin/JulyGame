namespace JulyGame
{
    public interface ISupportMultipleSource<T> where T : IDataProvider
    {
        T MainProvider { get; }
        T AdditionalProvider { get; }

        void SetMainProvider(T provider);
        void SetAdditionalProvider(T provider);
        void UnsetAdditionalProvider(T provider);
    }
}