using System.Net;

namespace TicketFlow.Application.Exceptions;

public class NotFoundException : TicketFlowException
{
    public NotFoundException(string message) : base(message) { }

    public override int GetStatusCode => (int)HttpStatusCode.NotFound;

    public override List<string> GetErrors() => [Message];
}
