namespace Booster.ExceptionManager.Abstraction.Exceptions;

public class BusinessException(string messageKey) : Exception(messageKey)
{
}
