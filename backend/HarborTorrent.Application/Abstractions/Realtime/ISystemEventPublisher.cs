using System.Threading.Tasks;

namespace HarborTorrent.Application.Abstractions.Realtime;

public interface ISystemEventPublisher
{
    Task BroadcastUpdateProgressAsync(int progressPercentage);
    Task BroadcastUpdateCompleteAsync();
}
