using System.Collections.Generic;
using Godot;
using Veld.Core;
using Xunit;

namespace Veld.Tests.Core;

public class NotifierTests
{
    private readonly List<string> _log = new();
    private readonly Notifier _notifier;

    public NotifierTests() => _notifier = new Notifier(_log.Add);

    [Fact]
    public void Notify_logs_the_line_and_raises_Notified_with_the_message()
    {
        var heard = new List<string>();
        _notifier.Notified += heard.Add;
        INotifier port = _notifier;

        port.Notify("Game saved.");

        Assert.Equal(new[] { "[notify] Game saved." }, _log);
        Assert.Equal(new[] { "Game saved." }, heard);
    }

    [Fact]
    public void Coins_raises_CoinsDropped_at_the_spot_and_logs_nothing()
    {
        var dropped = new List<Vector3>();
        _notifier.CoinsDropped += dropped.Add;

        _notifier.Coins(new Vector3(28f, 0f, -700f));

        Assert.Equal(new[] { new Vector3(28f, 0f, -700f) }, dropped);
        Assert.Empty(_log);
    }
}
