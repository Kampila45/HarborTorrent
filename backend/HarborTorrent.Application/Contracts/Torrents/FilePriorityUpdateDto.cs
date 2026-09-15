namespace HarborTorrent.Application.Contracts.Torrents;

public sealed record FilePriorityUpdateDto(
    int FileIndex,
    string Priority
);
