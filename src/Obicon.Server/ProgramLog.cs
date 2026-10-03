internal static partial class ProgramLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Obicon Server v{Version} starting")]
    public static partial void LogServerStarting(ILogger logger, string version);
}
