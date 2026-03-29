using System.Net;

namespace TicketFlow.Application.Exceptions;

public class ConflictException : TicketFlowException
{
    public ConflictException(string message) : base(message) { }

    public override int GetStatusCode => (int)HttpStatusCode.Conflict;

    public override List<string> GetErrors() => [Message];
}
