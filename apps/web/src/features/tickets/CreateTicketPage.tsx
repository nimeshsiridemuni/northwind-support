import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ErrorMessage } from "../../components/ErrorMessage";
import { Spinner } from "../../components/Spinner";
import { useReferenceData } from "../../hooks/useReferenceData";
import { formatDateTime } from "../../lib/format";
import type { TicketDetail } from "../../types/api";
import { CreateTicketForm } from "./components/CreateTicketForm";

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && (
        <Spinner label="Loading customers and categories" />
      )}

      {reference.error && (
        <ErrorMessage
          message={reference.error}
          onRetry={() => void reference.reload()}
        />
      )}

      {created && (
        <div className="card notice">
          <p role="status">
            Raised <strong>{created.reference}</strong> for{" "}
            {created.customer.name}.
          </p>

          <h2>Triage result</h2>

          <dl className="summary">
            <div>
              <dt>Priority</dt>
              <dd>
                {created.priority}
                {created.triage && (
                  <span className="summary__note">
                    {created.triage.priorityReason}
                  </span>
                )}
              </dd>
            </div>

            <div>
              <dt>SLA deadline</dt>
              <dd>
                {formatDateTime(created.dueAtUtc)}
                {created.triage && (
                  <span className="summary__note">
                    Window: {created.triage.slaWindowHours} hours.{" "}
                    {created.triage.slaReason}
                  </span>
                )}
              </dd>
            </div>

            <div>
              <dt>Assigned agent</dt>
              <dd>
                {created.assignedAgent?.fullName ?? "Unassigned"}
                {created.triage && (
                  <span className="summary__note">
                    {created.triage.assignmentReason}
                  </span>
                )}
              </dd>
            </div>
          </dl>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>

            <button
              type="button"
              className="button button--ghost"
              onClick={() => setCreated(undefined)}
            >
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
