namespace Booster.Amqp.Bus.Accessor;

/// <summary>
///Eğer bir backround serviste kullanıyorsan accessor.EventBus i set etmeyi sakın unutma
/// </summary>
public interface IEventBusAccessor
{
    IEventBus? EventBus { get; set; }
}

/// <summary>
///Eğer bir backround serviste kullanıyorsan accessor.EventBus i set etmeyi sakın unutma
/// </summary>
public class EventBusAccessor : IEventBusAccessor
{
    private static readonly AsyncLocal<IEventBus?> _current = new();

    public IEventBus? EventBus
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
