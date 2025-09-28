using Microsoft.AspNetCore.SignalR;
using FantasyFootballStatTracker.Models;
using FantasyFootballStatTracker.Services;

namespace FantasyFootballStatTracker.Hubs
{
    public class DraftHub : Hub
    {
        private readonly IDraftStateService _draftStateService;
        private readonly ILogger<DraftHub> _logger;

        public DraftHub(IDraftStateService draftStateService, ILogger<DraftHub> logger)
        {
            _draftStateService = draftStateService;
            _logger = logger;
        }

        public async Task JoinDraft(string draftId, int ownerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, draftId);
            _logger.LogInformation("Owner {OwnerId} joined draft {DraftId}", ownerId, draftId);
            
            // Notify others that someone joined
            await Clients.OthersInGroup(draftId).SendAsync("OwnerJoined", ownerId);
        }

        public async Task LeaveDraft(string draftId, int ownerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, draftId);
            _logger.LogInformation("Owner {OwnerId} left draft {DraftId}", ownerId, draftId);
            
            // Notify others that someone left
            await Clients.OthersInGroup(draftId).SendAsync("OwnerLeft", ownerId);
        }

        public async Task NotifyPickMade(string draftId, DraftEvent draftEvent)
        {
            // Broadcast the pick to all clients in the draft
            await Clients.Group(draftId).SendAsync("PickMade", draftEvent);
        }

        public async Task NotifyDraftComplete(string draftId)
        {
            await Clients.Group(draftId).SendAsync("DraftComplete");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Handle cleanup if needed
            await base.OnDisconnectedAsync(exception);
        }
    }
}