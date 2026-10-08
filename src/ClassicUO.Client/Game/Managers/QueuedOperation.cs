using System;
using System.Collections.Generic;
using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    internal sealed class QueuedOperation
    {
        internal readonly string Name;
        internal int Total, Sent, Skipped;
        internal bool SubmissionComplete, Verifying, Cancelled, Verified;
        internal string Detail = "Preparing requests";
        internal string Result;
        internal Action CancelSource;
        internal bool Finished => Cancelled || SubmissionComplete && !Verifying && Sent + Skipped >= Total;
        internal QueuedOperation(string name) { Name = name; }
        internal void Cancel()
        {
            if (Finished) return;
            Cancelled = true; Verifying = false;
            Detail = "Cancelled unsent work; sent requests cannot be undone";
            CancelSource?.Invoke();
        }
        internal string Summary => Cancelled ? $"Sent {Sent}/{Total} — " + Detail :
            $"Sent {Sent}/{Total}" + (Skipped > 0 ? $"; skipped {Skipped}" : "") +
            (!SubmissionComplete ? "; collecting selection" : "") + " — " +
            (Verified ? "Supplies verified" : Finished ? Result ?? "Requests finished; server acceptance is not tracked" : Detail);
    }

    internal static class QueuedOperations
    {
        internal static readonly List<QueuedOperation> Recent = new();
        internal static QueuedOperation Begin(string name, Action cancelSource = null)
        {
            if (Recent.Count >= 12) Recent.RemoveAll(operation => operation.Finished);
            var operation = new QueuedOperation(name) { CancelSource = cancelSource };
            Recent.Add(operation);
            if (World.InGame && UIManager.GetGump<QueuedOperationsGump>() == null)
                UIManager.Add(new QueuedOperationsGump());
            return operation;
        }
        internal static void Reset()
        {
            foreach (var operation in Recent.ToArray()) operation.Cancel();
            Recent.Clear();
        }
    }
}
