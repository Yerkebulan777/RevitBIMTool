namespace CommonUtils
{
    public static class LoggerFactory
    {
        /// <summary>
        /// Создает логгер для конкретного типа
        /// </summary>
        public static IModuleLogger CreateLogger<T>(string revitFilePath)
        {
            return ModuleLogger.Create<T>(revitFilePath);
        }
    }
}