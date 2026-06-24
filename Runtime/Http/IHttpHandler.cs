using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyGame
{
    public interface IHttpHandler
    {
        void OnError(int code, string msg);
        void OnKicked();
        void OnBlockingChanged(bool isBlocking);
        UniTask<bool> OnReLoginRequired(CancellationToken ct);
        UniTask<bool> OnRetryExceeded();
    }
}
