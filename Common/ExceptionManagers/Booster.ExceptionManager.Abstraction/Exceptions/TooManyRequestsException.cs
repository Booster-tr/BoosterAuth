namespace Booster.ExceptionManager.Abstraction.Exceptions;

public class TooManyRequestsException(string messageKey) : BusinessException(messageKey);
