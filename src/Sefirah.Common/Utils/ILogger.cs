using System;

namespace Sefirah.Common.Utils;
public interface ILogger
{
    void Debug(string message);
    void Debug(string message, params object[] args);

    void Info(string message);
    void Info(string message, params object[] args);

    void Warn(string message);
    void Warn(string message, params object[] args);

    void Error(string message);
    void Error(string message, params object[] args);
    void Error(Exception ex);
    void Error(string message, Exception ex);

    void Fatal(string message);
    void Fatal(string message, params object[] args);
    void Fatal(Exception ex);
    void Fatal(string message, Exception ex);
}
