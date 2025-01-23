using System;

namespace Sefirah.Common.Utils;

public class SerilogWrapperLogger(Serilog.ILogger logger) : ILogger
{
    private Serilog.ILogger _logger = logger;
    public Serilog.ILogger Logger
        => _logger ?? throw new InvalidOperationException("Logger is not initialized.");

    public void Debug(string message) => _logger.Debug(message);
    public void Debug(string message, params object[] args) => _logger.Debug(message, args);

    public void Info(string message) => _logger.Information(message);
    public void Info(string message, params object[] args) => _logger.Information(message, args);

    public void Warn(string message) => _logger.Warning(message);
    public void Warn(string message, params object[] args) => _logger.Warning(message, args);

    public void Error(string message) => _logger.Error(message);
    public void Error(string message, params object[] args) => _logger.Error(message, args);
    public void Error(Exception exception) => _logger.Error(exception, exception.Message);
    public void Error(string message, Exception exception) => _logger.Error(exception, message);

    public void Fatal(string message) => _logger.Fatal(message);
    public void Fatal(string message, params object[] args) => _logger.Fatal(message, args);
    public void Fatal(Exception exception) => _logger.Fatal(exception, exception.Message);
    public void Fatal(string message, Exception exception) => _logger.Fatal(exception, message);
}
