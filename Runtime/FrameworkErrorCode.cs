namespace JulyGame
{
    public enum FrameworkErrorCode
    {
        Success = 0,

        // General (1xxx)
        Unknown = 1000,
        InvalidArgument = 1001,
        NullReference = 1002,
        Timeout = 1003,
        Cancelled = 1004,
        InvalidState = 1005,
        NotInitialized = 1006,
        AlreadyInitialized = 1007,
        NotSupported = 1008,

        // System (2xxx)
        SystemNotFound = 2000,
        SystemNotInitialized = 2001,
        SystemInitFailed = 2002,

        // Resource (4xxx)
        ResourceNotFound = 4000,
        ResourceLoadFailed = 4001,
        ResourceTypeMismatch = 4002,
        ResourceReleased = 4003,

        // Network (5xxx)
        NetworkConnectionFailed = 5000,
        NetworkDisconnected = 5001,
        NetworkRequestFailed = 5002,
        NetworkTimeout = 5003,

        // UI (6xxx)
        UINotFound = 6000,
        UIOpenFailed = 6001,
        UIPrefabLoadFailed = 6002,

        // Data (7xxx)
        SerializeFailed = 7000,
        DeserializeFailed = 7001,
        SaveFailed = 7002,
        LoadFailed = 7003,
        EncryptFailed = 7004,
        DecryptFailed = 7005,

        // Config (8xxx)
        ConfigNotFound = 8000,
        ConfigFormatError = 8001,
    }
}
