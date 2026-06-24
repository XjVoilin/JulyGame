using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyGame
{
    public interface ILocalizationSystem : ISupportMultipleSource<ILocalizationDataProvider>
    {
        string CurrentLanguage { get; }
        string DefaultLanguage { get; }
        IReadOnlyList<string> SupportedLanguages { get; }
        UniTask LoadCurrentLanguageAsync(CancellationToken ct = default);
        UniTask<bool> SetLanguageAsync(string languageCode, CancellationToken ct = default);
        UniTask<bool> LoadLanguageAsync(string languageCode, CancellationToken ct = default);
        bool IsLanguageLoaded(string languageCode);
        void UnloadLanguage(string languageCode);
        string Get(string key, string defaultValue = null);
        string GetFormat(string key, params object[] args);
        bool HasKey(string key);
        IReadOnlyList<string> GetAllKeys(string languageCode = null);
    }
}
