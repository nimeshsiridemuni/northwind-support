import { useState } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../../api/client";
import { ticketsApi } from "../../../api/tickets";
import { ticketListItem } from "../../../test/fixtures";
import type { TicketDetail } from "../../../types/api";
import { EscalationPanel } from "./EscalationPanel";
import { TicketSummary } from "./TicketSummary";

vi.mock("../../../api/tickets", () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
    getEscalations: vi.fn(),
  },
}));

function makeTicket(): TicketDetail {
  const row = ticketListItem();

  return {
    ...row,
    description: "The invoice needs investigation.",
    customer: {
      ...row.customer,
      email: "customer@example.com",
      phone: null,
    },
    category: {
      ...row.category,
      requiresSpecialist: false,
    },
  };
}

function TicketView({
  onUpdated,
}: {
  onUpdated: (ticket: TicketDetail) => void;
}) {
  const [ticket, setTicket] = useState(makeTicket);

  return (
    <>
      <TicketSummary ticket={ticket} />
      <EscalationPanel
        ticket={ticket}
        onUpdated={(updated) => {
          onUpdated(updated);
          setTicket(updated);
        }}
      />
    </>
  );
}

function fillForm(reason = "The customer impact has increased.") {
  fireEvent.change(screen.getByLabelText("Escalation reason"), {
    target: { value: reason },
  });

  fireEvent.change(screen.getByLabelText("Escalated by"), {
    target: { value: "Development Agent" },
  });
}

describe("EscalationPanel", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
  });

  it("does not send a request when the reason is too short", async () => {
    render(<TicketView onUpdated={vi.fn()} />);
    await screen.findByText("No escalations recorded.");

    fillForm("four");

    const button = screen.getByRole("button", {
      name: "Escalate ticket",
    });

    expect(button).toBeDisabled();

    fireEvent.submit(button.closest("form")!);

    expect(ticketsApi.escalateTicket).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent("Enter a reason");
  });

  it("shows a 409 error and keeps the displayed ticket unchanged", async () => {
    const onUpdated = vi.fn();

    vi.mocked(ticketsApi.escalateTicket).mockRejectedValue(
      new ApiError(
        409,
        "The ticket is already Critical and cannot be escalated further.",
      ),
    );

    render(<TicketView onUpdated={onUpdated} />);
    await screen.findByText("No escalations recorded.");

    fillForm();

    fireEvent.click(screen.getByRole("button", { name: "Escalate ticket" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "already Critical",
    );

    expect(onUpdated).not.toHaveBeenCalled();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Sara Lindqvist")).toBeInTheDocument();
    expect(screen.getByText("Breached")).toBeInTheDocument();
    expect(ticketsApi.getEscalations).toHaveBeenCalledTimes(1);
  });

  it("updates the displayed ticket and reloads history after success", async () => {
    const onUpdated = vi.fn();

    const updated: TicketDetail = {
      ...makeTicket(),
      priority: "Critical",
      slaStatus: "WithinSla",
      dueAtUtc: "2026-10-11T12:00:00Z",
    };

    vi.mocked(ticketsApi.escalateTicket).mockResolvedValue(updated);

    render(<TicketView onUpdated={onUpdated} />);
    await screen.findByText("No escalations recorded.");

    fillForm();

    fireEvent.click(screen.getByRole("button", { name: "Escalate ticket" }));

    await waitFor(() => {
      expect(onUpdated).toHaveBeenCalledWith(updated);
      expect(ticketsApi.getEscalations).toHaveBeenCalledTimes(2);
    });

    expect(screen.getByText("Critical")).toBeInTheDocument();
    expect(screen.getByText("Within SLA")).toBeInTheDocument();
    expect(screen.getByLabelText("Escalation reason")).toHaveValue("");
  });
});
