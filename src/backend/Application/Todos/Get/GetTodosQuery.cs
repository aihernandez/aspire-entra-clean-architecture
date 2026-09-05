using Application.Abstractions.Messaging;
using Application.Todos;

namespace Application.Todos.Get;

public sealed record GetTodosQuery(Guid UserId) : IQuery<List<TodoResponse>>;
