using FantasyFootballStatTracker.Models;

namespace FantasyFootballStatTracker.Services
{
    public interface IDraftStateService
    {
        Task<DraftState?> GetDraftStateAsync(string draftId);
        Task SaveDraftStateAsync(DraftState draftState);
        Task<bool> DraftExistsAsync(string draftId);
        Task DeleteDraftAsync(string draftId);
        Task<string> CreateNewDraftAsync(int week, List<Owner> owners, int firstPickOwnerId);
        Task AddDraftedPlayerAsync(string draftId, DraftedPlayer player);
        Task<DraftState> GetActiveDraftAsync();
    }
}