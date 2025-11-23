using Grpc.Core;
using CSharpServer.Protos;

namespace CSharpServer.Services;

public class DataTransferServiceImpl : DataTransferService.DataTransferServiceBase
{
    private readonly ILogger<DataTransferServiceImpl> _logger;

    public DataTransferServiceImpl(ILogger<DataTransferServiceImpl> logger)
    {
        _logger = logger;
    }

    public override Task<TransferResponse> SendContacts(ContactList request, ServerCallContext context)
    {
        _logger.LogInformation("Received {Count} contacts", request.Contacts.Count);
        foreach (var contact in request.Contacts)
        {
            _logger.LogInformation("Contact: {Name}, {PhoneNumber}", contact.Name, contact.PhoneNumber);
        }

        return Task.FromResult(new TransferResponse
        {
            Success = true,
            Message = $"Successfully received {request.Contacts.Count} contacts."
        });
    }

    public override Task<TransferResponse> SendCallLogs(CallLogList request, ServerCallContext context)
    {
        _logger.LogInformation("Received {Count} call logs", request.Logs.Count);
        foreach (var log in request.Logs)
        {
            _logger.LogInformation("Log: {Number}, {Type}, {Date}, {Duration}", log.Number, log.Type, log.Date, log.Duration);
        }

        return Task.FromResult(new TransferResponse
        {
            Success = true,
            Message = $"Successfully received {request.Logs.Count} call logs."
        });
    }
}
