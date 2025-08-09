using System;
using Microsoft.Extensions.Logging;

namespace Mangaanya.Services
{
    public class SettingsChangedNotifier : ISettingsChangedNotifier
    {
        private readonly ILogger<SettingsChangedNotifier> _logger;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public SettingsChangedNotifier(ILogger<SettingsChangedNotifier> logger)
        {
            _logger = logger;
        }

        public void NotifySettingsChanged(string settingName)
        {
            
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(settingName));
        }
    }
}
