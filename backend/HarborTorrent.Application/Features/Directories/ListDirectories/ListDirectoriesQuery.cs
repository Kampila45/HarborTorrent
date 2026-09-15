using HarborTorrent.Application.Contracts.Directories;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Directories.ListDirectories;

public record ListDirectoriesQuery(string? Path) : IRequest<Result<IReadOnlyCollection<DirectoryItemDto>>>;
