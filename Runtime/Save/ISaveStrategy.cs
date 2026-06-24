namespace JulyGame
{
    public interface ISaveStrategy
    {
        bool ShouldSave(SaveContext context);
    }
}
