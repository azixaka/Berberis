using Berberis.Messaging;
using Berberis.Messaging.AspNetCore;

namespace Berberis.SampleApp;

public class ProcessesConsumer : BerberisConsumer<ProcessInfo>
{
    private readonly ILogger<ProcessesConsumer> _logger;

    public ProcessesConsumer(ILogger<ProcessesConsumer> logger) : base("processes.info")
    {
        _logger = logger;
    }
    
    protected override ValueTask Consume(Message<ProcessInfo> message)
    {
        _logger.LogInformation("Subscription [{SubId}] got Message [Id={MsgId}, Ms={CpuTime}]", Subscription.Id,
            message.Id, message.Body.CpuTimeMs);
        
        return ValueTask.CompletedTask;
    }
}