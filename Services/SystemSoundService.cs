using System.Media;
using Microsoft.Extensions.Logging;

namespace Mangaanya.Services
{
    public interface ISystemSoundService
    {
        void PlayCompletionSound();
        void PlaySettingsAppliedSound();
        void PlayErrorSound();
    }

    public class SystemSoundService : ISystemSoundService
    {
        private readonly ILogger<SystemSoundService> _logger;

        public SystemSoundService(ILogger<SystemSoundService> logger)
        {
            _logger = logger;
        }

        public void PlayCompletionSound()
        {
            try
            {
                SystemSounds.Asterisk.Play();
                _logger.LogDebug("処理完了音を再生しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "処理完了音の再生に失敗しました");
            }
        }

        public void PlaySettingsAppliedSound()
        {
            try
            {
                _logger.LogInformation("設定適用音の再生を開始します");
                SystemSounds.Exclamation.Play();
                _logger.LogInformation("設定適用音を再生しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "設定適用音の再生に失敗しました");
            }
        }

        public void PlayErrorSound()
        {
            try
            {
                SystemSounds.Hand.Play();
                _logger.LogDebug("エラー音を再生しました");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "エラー音の再生に失敗しました");
            }
        }
    }
}