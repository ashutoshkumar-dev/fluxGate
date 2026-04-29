using Gateway.Core.DTOs;

namespace Gateway.Core.Interfaces;

public interface ISeqLogsService
{
    Task<LogPageDto> QueryLogsAsync(LogQueryDto query, CancellationToken ct = default);
}
