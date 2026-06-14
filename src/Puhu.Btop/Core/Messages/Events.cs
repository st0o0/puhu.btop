namespace Puhu.Btop.Core.Messages;

public sealed record ActionSuccess(string Message);

public sealed record ActionFailure(string Error);
