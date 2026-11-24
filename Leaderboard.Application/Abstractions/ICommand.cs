using MediatR;

namespace Leaderboard.Application.Abstractions;

/// <summary>Marks a request as a command that runs inside the transaction boundary.</summary>
public interface ICommand : IRequest;

public interface ICommand<out TResponse> : ICommand, IRequest<TResponse>;
