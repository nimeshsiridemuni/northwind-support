import { useCallback, useState, type FormEvent } from "react";
import { toErrorMessage } from "../../../api/client";
import { ticketsApi } from "../../../api/tickets";
import { ErrorMessage } from "../../../components/ErrorMessage";
import { useAsyncData } from "../../../hooks/useAsyncData";
import { formatDateTime } from "../../../lib/format";
import type { TicketDetail } from "../../../types/api";

interface EscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
  const [reason, setReason] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [success, setSuccess] = useState<string>();

  const loadHistory = useCallback(
    () => ticketsApi.getEscalations(ticket.id),
    [ticket.id],
  );

  const history = useAsyncData(
    loadHistory,
    "Could not load escalation history.",
  );

  const reasonValid = reason.trim().length >= 5 && reason.trim().length <= 500;

  const blockedReason =
    ticket.status === "Resolved" || ticket.status === "Closed"
      ? "Resolved or closed tickets cannot be escalated."
      : ticket.priority === "Critical"
        ? "This ticket is already Critical."
        : undefined;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (pending || blockedReason) {
      return;
    }

    if (!reasonValid) {
      setError("Enter a reason of 5–500 characters.");
      return;
    }

    setPending(true);
    setError(undefined);
    setSuccess(undefined);

    try {
      const updated = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
      });

      onUpdated(updated);
      setReason("");
      setSuccess("Ticket escalated successfully.");

      await history.reload();
    } catch (caught) {
      setError(toErrorMessage(caught, "Could not escalate the ticket."));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="card">
      <h2>Escalation</h2>

      <p>
        Escalation raises priority by one level and sets a new SLA deadline from
        the current time.
      </p>

      <p>The history records your signed-in agent account automatically.</p>

      {blockedReason && <p>{blockedReason}</p>}

      <form
        onSubmit={(event) => void submit(event)}
        noValidate
        aria-busy={pending}
      >
        <div className="field">
          <label className="field__label" htmlFor="escalation-reason">
            Escalation reason
          </label>

          <textarea
            id="escalation-reason"
            value={reason}
            rows={3}
            maxLength={500}
            required
            disabled={pending || Boolean(blockedReason)}
            aria-describedby="escalation-reason-help"
            onChange={(event) => setReason(event.target.value)}
          />

          <p id="escalation-reason-help" className="field__hint">
            Explain why this needs higher priority. Use 5–500 characters.
          </p>
        </div>

        <button
          type="submit"
          className="button"
          disabled={pending || Boolean(blockedReason) || !reasonValid}
        >
          {pending ? "Escalating..." : "Escalate ticket"}
        </button>
      </form>

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      {success && <p role="status">{success}</p>}

      <h3>Escalation history</h3>

      {history.isLoading && <p role="status">Loading history...</p>}

      {history.error && (
        <ErrorMessage
          message={history.error}
          onRetry={() => void history.reload()}
        />
      )}

      {history.data?.length === 0 && <p>No escalations recorded.</p>}

      {history.data?.map((entry) => (
        <article key={entry.id}>
          <h4>
            {entry.fromPriority} → {entry.toPriority}
          </h4>

          <p>{entry.reason}</p>

          <p>
            By {entry.escalatedBy} at {formatDateTime(entry.escalatedAtUtc)}
          </p>

          <dl className="summary">
            <div>
              <dt>Previous agent</dt>
              <dd>
                {entry.fromAgentId === null
                  ? "Unassigned"
                  : `Agent #${entry.fromAgentId}`}
              </dd>
            </div>

            <div>
              <dt>New agent</dt>
              <dd>
                {entry.toAgentId === null
                  ? "Unassigned"
                  : `Agent #${entry.toAgentId}`}
              </dd>
            </div>

            <div>
              <dt>Previous deadline</dt>
              <dd>{formatDateTime(entry.fromDueAtUtc)}</dd>
            </div>

            <div>
              <dt>New deadline</dt>
              <dd>{formatDateTime(entry.toDueAtUtc)}</dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  );
}
