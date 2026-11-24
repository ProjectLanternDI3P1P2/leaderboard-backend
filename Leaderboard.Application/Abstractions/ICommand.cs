using MediatR;

namespace Leaderboard.Application.Abstractions;

/// <summary>Marks a request as a command: it runs inside the command transaction boundary.</summary>
public interface ITransactionalRequest;

public interface ICommand : IRequest, ITransactionalRequest;

public interface ICommand<out TResponse> : IRequest<TResponse>, ITransactionalRequest;
