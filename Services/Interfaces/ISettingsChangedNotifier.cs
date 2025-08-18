using System;

namespace Mangaanya.Services
{
    public interface ISettingsChangedNotifier
    {
        event EventHandler<SettingsChangedEventArgs> SettingsChanged;
        void NotifySettingsChanged(string settingName);
    }

    public class SettingsChangedEventArgs : EventArgs
    {
        public string SettingName { get; }

        public SettingsChangedEventArgs(string settingName)
        {
            SettingName = settingName;
        }
    }
}
