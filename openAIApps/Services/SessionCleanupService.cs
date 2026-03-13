using System.Threading.Tasks;

namespace openAIApps.Services
{
    public class SessionCleanupService
    {
        private readonly HistoryService _historyService;
        private readonly MediaStorageService _mediaStorageService;

        public SessionCleanupService(
            HistoryService historyService,
            MediaStorageService mediaStorageService)
        {
            _historyService = historyService;
            _mediaStorageService = mediaStorageService;
        }

        public async Task DeleteSessionAsync(int sessionId)
        {
            var mediaPaths = await _historyService.GetMediaPathsForSessionAsync(sessionId);
            _mediaStorageService.DeleteFiles(mediaPaths);
            await _historyService.DeleteSessionAsync(sessionId);
        }
    }
}