using JulyArch;

namespace JulyGame
{
    public static class GameArch
    {
        public static ArchContext Context { get; private set; }

        public static void Create() => Context = new ArchContext();
    }
}
