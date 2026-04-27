using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace SANJET.Core.Services
{
    /// <summary>
    /// LibVLC 全局初始化和管理服務
    /// 在應用啟動時預先初始化 LibVLC 編解碼器，避免首次串流延遲
    /// </summary>
    public interface ILibVLCInitializationService
    {
        /// <summary>
        /// 預熱 LibVLC 編解碼器，減少首次串流啟動延遲
        /// </summary>
        Task PreWarmAsync();

        /// <summary>
        /// 獲取初始化好的 LibVLC 實例
        /// </summary>
        LibVLC GetLibVLC();

        /// <summary>
        /// 清理資源
        /// </summary>
        void Dispose();
    }

    public class LibVLCInitializationService : ILibVLCInitializationService
    {
        private readonly ILogger<LibVLCInitializationService> _logger;
        private LibVLC? _libVLC;
        private MediaPlayer? _preWarmPlayer;
        private bool _isPreWarmed = false;

        public LibVLCInitializationService(ILogger<LibVLCInitializationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 預熱 LibVLC 編解碼器
        /// 在後台線程上初始化 LibVLC 和播放器，加載 RTSP 編解碼器
        /// </summary>
        public async Task PreWarmAsync()
        {
            if (_isPreWarmed)
            {
                _logger.LogInformation("LibVLC 已預熱，跳過重複預熱");
                return;
            }

            try
            {
                _logger.LogInformation("開始預熱 LibVLC 編解碼器...");

                // 在後台線程上執行初始化
                await Task.Run(() =>
                {
                    try
                    {
                        // 初始化 LibVLC Core
                        try
                        {
                            LibVLCSharp.Shared.Core.Initialize();
                            _logger.LogInformation("LibVLC Core 已初始化");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "LibVLC Core 初始化已執行或已初始化，繼續...");
                        }

                        // 創建 LibVLC 實例
                        _libVLC = new LibVLC(
                            "--rtsp-tcp",
                            "--network-caching=300",
                            "--avcodec-threads=0"  // 自動選擇最佳執行緒數
                        );
                        _logger.LogInformation("LibVLC 實例已創建");

                        // 創建播放器用於預熱
                        _preWarmPlayer = new MediaPlayer(_libVLC);
                        _logger.LogInformation("MediaPlayer 已創建");

                        // 嘗試播放一個虛擬媒體以加載編解碼器
                        try
                        {
                            var dummyUri = new Uri("file:///dev/null");
                            var dummyMedia = new Media(_libVLC, dummyUri);
                            _preWarmPlayer.Play(dummyMedia);
                            System.Threading.Thread.Sleep(500);  // 等待編解碼器加載
                            _preWarmPlayer.Stop();
                            System.Threading.Thread.Sleep(100);
                            dummyMedia.Dispose();
                            _logger.LogInformation("虛擬媒體播放完成，編解碼器已加載");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "虛擬媒體播放失敗（預期行為），但編解碼器已初始化");
                        }

                        _isPreWarmed = true;
                        _logger.LogInformation("LibVLC 預熱完成，後續 RTSP 串流連接將快速啟動");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "LibVLC 預熱過程中發生錯誤");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LibVLC 預熱失敗，應用將繼續運行，但首次串流可能較慢");
            }
        }

        /// <summary>
        /// 獲取預初始化的 LibVLC 實例
        /// 如果尚未預熱，則動態創建一個新實例
        /// </summary>
        public LibVLC GetLibVLC()
        {
            if (_libVLC != null)
            {
                return _libVLC;
            }

            // 後備方案：如果尚未預熱，立即創建
            _logger.LogWarning("LibVLC 尚未預熱，創建新實例...");

            try
            {
                LibVLCSharp.Shared.Core.Initialize();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LibVLC Core 初始化已執行或已初始化，繼續...");
            }

            _libVLC = new LibVLC(
                "--rtsp-tcp",
                "--network-caching=300",
                "--avcodec-threads=0"
            );

            return _libVLC;
        }

        /// <summary>
        /// 清理資源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _preWarmPlayer?.Dispose();
                _preWarmPlayer = null;

                _libVLC?.Dispose();
                _libVLC = null;

                _logger.LogInformation("LibVLC 資源已清理");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理 LibVLC 資源時發生錯誤");
            }
        }
    }
}
