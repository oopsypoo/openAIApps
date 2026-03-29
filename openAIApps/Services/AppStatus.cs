using System;
using System.Threading;
using System.Windows.Threading;

namespace openAIApps.Services
{
    /// <summary>
    /// App-wide status dispatcher with scoped operations.
    /// Keeps nested operations safe via a busy counter.
    /// </summary>
    public sealed class AppStatus
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _setStatusText;
        private int _busyCounter;

        public AppStatus(Dispatcher dispatcher, Action<string> setStatusText)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _setStatusText = setStatusText ?? throw new ArgumentNullException(nameof(setStatusText));
        }

        public bool IsBusy => _busyCounter > 0;

        public void Set(string text)
        {
            if (_dispatcher.CheckAccess())
                _setStatusText(text ?? string.Empty);
            else
                _dispatcher.BeginInvoke(() => _setStatusText(text ?? string.Empty));
        }

        public IDisposable Operation(string text)
        {
            Interlocked.Increment(ref _busyCounter);
            Set(text);
            return new Scope(this);
        }

        private void End()
        {
            if (_busyCounter > 0)
                Interlocked.Decrement(ref _busyCounter);

            // Optional default idle text:
            if (_busyCounter == 0)
                Set("Ready...");
        }

        private sealed class Scope : IDisposable
        {
            private AppStatus _owner;
            public Scope(AppStatus owner) => _owner = owner;
            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                owner?.End();
            }
        }
    }
}