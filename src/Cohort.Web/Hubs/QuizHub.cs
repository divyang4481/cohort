using Microsoft.AspNetCore.SignalR;

namespace Cohort.Web.Hubs;

public sealed class QuizHub : Hub
{
    public Task JoinQuiz(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            throw new HubException("joinCode is required");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(joinCode));
    }

    public static string GroupName(string joinCode) => $"quiz:{joinCode.Trim().ToUpperInvariant()}";
}
