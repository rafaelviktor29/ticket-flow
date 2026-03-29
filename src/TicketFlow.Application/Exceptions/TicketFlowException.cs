namespace TicketFlow.Application.Exceptions;

public abstract class TicketFlowException : SystemException
{
    protected TicketFlowException(string message) : base(message) { }

    public abstract int GetStatusCode { get; }

    public abstract List<string> GetErrors();
}
