using Veld.Sim;

namespace Veld.Tests.Fakes;

public sealed class FakeWanted : IWantedState
{
    public int Level { get; set; }
    public int Clears { get; private set; }

    public void ClearWanted()
    {
        Level = 0;
        Clears++;
    }
}
