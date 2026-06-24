using System;

namespace JulyGame
{
    public readonly struct FrameworkResult
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public FrameworkErrorCode ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }

        private FrameworkResult(bool isSuccess, FrameworkErrorCode errorCode, string message, Exception exception)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            Message = message;
            Exception = exception;
        }

        public static FrameworkResult Success() => new(true, FrameworkErrorCode.Success, null, null);

        public static FrameworkResult Failure(FrameworkErrorCode errorCode, string message = null)
            => new(false, errorCode, message ?? errorCode.ToString(), null);

        public static FrameworkResult Failure(FrameworkErrorCode errorCode, Exception exception)
            => new(false, errorCode, exception?.Message ?? errorCode.ToString(), exception);

        public static implicit operator bool(FrameworkResult result) => result.IsSuccess;

        public override string ToString()
            => IsSuccess ? "Success" : $"Failure({ErrorCode}): {Message}";
    }

    public readonly struct FrameworkResult<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T Value { get; }
        public FrameworkErrorCode ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }

        private FrameworkResult(bool isSuccess, T value, FrameworkErrorCode errorCode, string message,
            Exception exception)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorCode = errorCode;
            Message = message;
            Exception = exception;
        }

        public static FrameworkResult<T> Success(T value)
            => new(true, value, FrameworkErrorCode.Success, null, null);

        public static FrameworkResult<T> Failure(FrameworkErrorCode errorCode, string message = null,
            Exception exception = null)
            => new(false, default, errorCode, message ?? errorCode.ToString(), exception);

        public T GetValueOrDefault(T defaultValue = default)
            => IsSuccess ? Value : defaultValue;

        public static implicit operator bool(FrameworkResult<T> result) => result.IsSuccess;

        public override string ToString()
            => IsSuccess ? $"Success: {Value}" : $"Failure({ErrorCode}): {Message}";
    }
}
